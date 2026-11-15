using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using TEngine.Extensions.Tween;
using UnityEditor;
using UnityEngine;

namespace TEngine.Extensions.Editor.Tween
{
    [CustomPropertyDrawer(typeof(TweenAnimation))]
    internal sealed class TweenAnimationDrawer : PropertyDrawer
    {
        private sealed class State
        {
            internal TweenAnimationInspector Inspector;
            internal SerializedObject Data;
            internal Object[] Targets;
            internal float Width;
            internal EditorWindow Window;
            internal void Repaint() => TweenInspectorRepaint.Request(Window);
        }
        // 不持有 SerializedObject 的强根引用，Inspector 关闭后其字段状态可一起回收。
        private readonly ConditionalWeakTable<SerializedObject, Dictionary<string, State>> m_States =
            new ConditionalWeakTable<SerializedObject, Dictionary<string, State>>();
        private State GetState(SerializedProperty property)
        {
            var data = property.serializedObject;
            var targets = data.targetObjects;
            var states = m_States.GetOrCreateValue(data);
            states.TryGetValue(property.propertyPath, out var state);
            if (state == null || !SameTargets(state.Targets, targets))
            {
                state?.Inspector.Dispose();
                state = new State { Data = data, Targets = targets };
                state.Inspector = new TweenAnimationInspector(data, property.propertyPath, state.Repaint);
                states[property.propertyPath] = state;
            }
            return state;
        }

        private static bool SameTargets(Object[] previous, Object[] current)
        {
            if (previous.Length != current.Length) return false;
            for (var i = 0; i < previous.Length; i++) if (previous[i] != current[i]) return false;
            return true;
        }

        public override bool CanCacheInspectorGUI(SerializedProperty property) => false;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;
            var state = GetState(property);
            var width = state.Width > 0 ? state.Width : EditorGUIUtility.currentViewWidth - 40;
            return EditorGUIUtility.singleLineHeight + state.Inspector.GetHeight(width) + 4;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                var header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                property.isExpanded = EditorGUI.Foldout(header, property.isExpanded, label, true);
                if (property.isExpanded)
                {
                    var state = GetState(property);
                    state.Window = TweenInspectorRepaint.CurrentWindow;
                    var widthChanged = Mathf.Abs(state.Width - position.width) > .5f;
                    state.Width = position.width;
                    var content = new Rect(position.x, header.yMax + 4, position.width, Mathf.Max(1, position.height - header.height - 4));
                    state.Inspector.OnGUI(content);
                    if (Event.current.type == EventType.Repaint && widthChanged) state.Repaint();
                }
            }
            finally { EditorGUI.EndProperty(); }
        }
    }

    internal static class TweenInspectorRepaint
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        // Unity 2021 没有公开的“当前绘制窗口”API；集中适配，不依赖窗口焦点或鼠标位置。
        private static readonly PropertyInfo CurrentView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView")?.GetProperty("current", Flags);
        private static readonly PropertyInfo ActualView = typeof(EditorWindow).Assembly.GetType("UnityEditor.HostView")?.GetProperty("actualView", Flags);
        private static readonly PropertyInfo ClipRect = typeof(GUI).Assembly.GetType("UnityEngine.GUIClip")?.GetProperty("visibleRect", Flags);
        private static readonly HashSet<EditorWindow> Pending = new HashSet<EditorWindow>();
        private sealed class PreviewView
        {
            internal System.WeakReference<TweenAnimationInspector> Inspector;
            internal System.WeakReference<EditorWindow> Window;
        }
        private static readonly List<PreviewView> PreviewViews = new List<PreviewView>();
        private static readonly HashSet<EditorWindow> PreviewWindows = new HashSet<EditorWindow>();

        internal static void ObservePreview(TweenAnimationInspector inspector, EditorWindow window)
        {
            if (window == null || !inspector.RequiresConstantRepaint()) return;
            foreach (var view in PreviewViews)
                if (view.Inspector.TryGetTarget(out var existing) && ReferenceEquals(existing, inspector)
                    && view.Window.TryGetTarget(out var host) && host == window) return;
            PreviewViews.Add(new PreviewView
            {
                Inspector = new System.WeakReference<TweenAnimationInspector>(inspector),
                Window = new System.WeakReference<EditorWindow>(window)
            });
        }

        internal static void ForgetPreview(TweenAnimationInspector inspector)
        {
            PreviewViews.RemoveAll(view => !view.Inspector.TryGetTarget(out var existing) || ReferenceEquals(existing, inspector));
        }

        // 由预览 Update 驱动，不通过 delayCall 接续播放，也不扫描无关窗口。
        internal static void TickPreview()
        {
            PreviewWindows.Clear();
            for (var i = PreviewViews.Count - 1; i >= 0; i--)
            {
                var view = PreviewViews[i];
                if (!view.Inspector.TryGetTarget(out var inspector) || !view.Window.TryGetTarget(out var window) || window == null)
                { PreviewViews.RemoveAt(i); continue; }
                // 暂停/停止的第一轮仍刷新一次，显示最终进度后退出连续刷新。
                PreviewWindows.Add(window);
                if (!inspector.RequiresConstantRepaint()) PreviewViews.RemoveAt(i);
            }
            foreach (var window in PreviewWindows) window.Repaint();
            PreviewWindows.Clear();
        }
        internal static Rect VisibleRect => ClipRect != null ? (Rect)ClipRect.GetValue(null)
            : new Rect(-10000000, -10000000, 20000000, 20000000);
        internal static EditorWindow CurrentWindow
        {
            get
            {
                var view = CurrentView?.GetValue(null);
                return view != null && ActualView != null && ActualView.DeclaringType.IsInstanceOfType(view)
                    ? ActualView.GetValue(view) as EditorWindow : null;
            }
        }

        internal static void Request(EditorWindow window)
        {
            if (window == null || !Pending.Add(window)) return;
            if (Pending.Count == 1) EditorApplication.delayCall += Flush;
        }

        private static void Flush()
        {
            foreach (var window in Pending)
                if (window != null) window.Repaint();
            Pending.Clear();
        }
    }
}
