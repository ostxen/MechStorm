#if !UNITY_6000_3_OR_NEWER
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Pipeline.Editor
{
    /// <summary>Displays the Pipeline server toggle in the main toolbar.</summary>
    internal static class PipelineToolbar
    {
        private const string ElementName = "unity-pipeline-server-toggle";
        private static readonly Type s_ToolbarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");
        private static readonly FieldInfo s_RootField = s_ToolbarType?.GetField("m_Root", BindingFlags.Instance | BindingFlags.NonPublic);
        private static ScriptableObject s_Toolbar;
        private static VisualElement s_Root;
        private static IMGUIContainer s_Container;
        private static GUIContent s_Content;
        private static bool s_Running;
        private static int s_Port;
        private static double s_NextUpdate;

        internal static void Initialize()
        {
            if (Application.isBatchMode)
                return;
            // This is an internal Editor boundary. If Unity changes it, leave the server and menus usable.
            if (s_RootField == null)
            {
                WarnUnsupportedToolbar();
                return;
            }
            EditorApplication.update += OnUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
        }

        private static void OnUpdate()
        {
            if (EditorApplication.timeSinceStartup < s_NextUpdate)
                return;
            s_NextUpdate = EditorApplication.timeSinceStartup + 0.5;

            if (s_Toolbar == null)
            {
                var toolbars = Resources.FindObjectsOfTypeAll(s_ToolbarType);
                if (toolbars.Length == 0)
                    return;
                s_Toolbar = (ScriptableObject)toolbars[0];
            }

            var root = (VisualElement)s_RootField.GetValue(s_Toolbar);
            if (root == null)
                return; // Unity creates the visual tree after the toolbar object.
            if (root != s_Root || s_Container?.parent == null)
                Attach(root);

            var port = PipelineServerStartup.Server?.Port ?? 0;
            if (s_Running != PipelineServerStartup.IsServerRunning || s_Port != port)
                Refresh();
        }

        private static void Attach(VisualElement root)
        {
            var zone = root.Q("ToolbarZoneRightAlign");
            if (zone == null)
            {
                Shutdown();
                WarnUnsupportedToolbar();
                return;
            }

            s_Container?.RemoveFromHierarchy();
            root.Q(ElementName)?.RemoveFromHierarchy();
            s_Root = root;
            s_Container = new IMGUIContainer(OnGUI) { name = ElementName };
            s_Container.style.width = 150;
            s_Container.style.flexShrink = 0;
            s_Container.style.marginRight = 6;
            s_Container.style.alignSelf = Align.Center;
            zone.Insert(0, s_Container);
            Refresh();
        }

        internal static void Refresh()
        {
            if (s_Container == null)
                return;
            s_Running = PipelineServerStartup.IsServerRunning;
            s_Port = PipelineServerStartup.Server?.Port ?? 0;
            s_Content = PipelineServerStartup.CreateToolbarContent();
            s_Container.MarkDirtyRepaint();
        }

        private static void OnGUI()
        {
            var running = PipelineServerStartup.IsServerRunning;
            var requested = GUILayout.Toggle(running, s_Content, EditorStyles.toolbarButton);
            if (requested != running)
                PipelineServerStartup.SetToolbarServerRunning(requested);
        }

        private static void Shutdown()
        {
            EditorApplication.update -= OnUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            EditorApplication.quitting -= Shutdown;
            s_Container?.RemoveFromHierarchy();
            s_Container = null;
            s_Root = null;
            s_Toolbar = null;
        }

        private static void WarnUnsupportedToolbar()
        {
            Debug.LogWarning("Pipeline could not attach its toolbar toggle to this Editor. Use Window > Pipeline > Start Server / Stop Server instead.");
        }
    }
}
#endif
