using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GameLogic;
using NUnit.Framework;
using TEngine.Editor.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TEngine.UIBinding.Tests
{
    [Category("UIBindingHotfix")]
    public sealed class UIBindingHotfixTests
    {
        private Scene _scene;
        private GameObject _root;
        private UIScriptGeneratorSetting _settings;
        private UIScriptGeneratorSetting _previousSettings;
        private IUIScriptGenerationAdapter _previousAdapter;
        private string _clipboard;
        private static readonly FieldInfo SettingsInstance = typeof(UIScriptGeneratorSetting).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            _clipboard = EditorGUIUtility.systemCopyBuffer;
            _previousSettings = (UIScriptGeneratorSetting)SettingsInstance.GetValue(null);
            _previousAdapter = UIScriptGeneration.Adapter;
            _settings = ScriptableObject.CreateInstance<UIScriptGeneratorSetting>();
            var serialized = new SerializedObject(_settings);
            serialized.FindProperty("useBindComponent").boolValue = true;
            serialized.FindProperty("_namespace").stringValue = "GameLogic";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SettingsInstance.SetValue(null, _settings);
            UIScriptGeneration.Adapter = new UIGenerationAdapter();
            _scene = EditorSceneManager.NewPreviewScene();
            _root = new GameObject("BindingWindow", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(_root, _scene);
            Child("m_btnAccept", typeof(Button));
            Child("m_imgIcon", typeof(Image));
        }

        private GameObject Child(string name, Type component)
        {
            var child = new GameObject(name, typeof(RectTransform), component);
            child.transform.SetParent(_root.transform, false);
            return child;
        }

        [TearDown]
        public void TearDown()
        {
            SettingsInstance.SetValue(null, _previousSettings);
            UIScriptGeneration.Adapter = _previousAdapter;
            EditorGUIUtility.systemCopyBuffer = _clipboard;
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void BindingAndEditorTools_HaveCorrectAssemblyBoundary()
        {
            Assert.That(typeof(UIBindComponent).Assembly.GetName().Name, Is.EqualTo("GameLogic"));
            Assert.That(typeof(UIBindComponentGenerator).Assembly.GetName().Name, Is.EqualTo("Assembly-CSharp-Editor"));
            Assert.That(typeof(UIComponentInspectorEditor).Assembly.GetName().Name, Is.EqualTo("Assembly-CSharp-Editor"));
            Assert.That(typeof(UnityEditorInspectorDrawHelper).Assembly.GetName().Name, Is.EqualTo("Assembly-CSharp-Editor"));
            Assert.That(typeof(UIScriptGeneration).Assembly.GetName().Name, Is.EqualTo("Assembly-CSharp-Editor"));
            foreach (var assembly in new[] { typeof(TEngine.Extensions.GameObjectPool.GameObjectPool).Assembly })
            {
                Assert.That(assembly.GetType("TEngine.Extensions.UI.UIBindComponent"), Is.Null);
                Assert.That(assembly.GetReferencedAssemblies().Any(a => a.Name == "GameLogic" || a.Name == "GameLogic.Editor"), Is.False);
            }
        }

        [Test]
        public void HtmlEditor_UsesOriginalDirectoryAndDefaultAssembly()
        {
            const string htmlAssembly = "Assembly-CSharp-Editor";
            Assert.That(HtmlBakerType.Assembly.GetName().Name, Is.EqualTo(htmlAssembly));
            Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/TEngine/Extension/HtmlToUGUI/Editor/HtmlToUGUIBaker.cs").GetClass(), Is.EqualTo(HtmlBakerType));
            var editorAssemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor);
            var extensions = editorAssemblies.Single(a => a.name == "TEngine.Extensions.Editor");
            Assert.That(extensions.assemblyReferences.Any(a => a.name == "TEngine.UI.Editor" || a.name == htmlAssembly), Is.False);
            Assert.That(extensions.sourceFiles.Any(p => p.Replace('\\', '/').EndsWith("/HtmlToUGUIBaker.cs")), Is.False);
            Assert.That(editorAssemblies.Any(a => a.name == "TEngine.HtmlToUGUI.Editor"), Is.False);
            var playerAssemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Player);
            Assert.That(playerAssemblies.Any(a => a.name == htmlAssembly), Is.False);
        }

        [Test]
        public void SharedTools_UseOriginalDirectoriesAndDefaultEditorAssembly()
        {
            var paths = new[]
            {
                "Assets/Editor/AtlasRefWindow/AtlasRefWindow.cs",
                "Assets/Editor/ReferenceFinder/AssetTreeView.cs",
                "Assets/Editor/TEngineSettingsProvider/TEngineSettingsProvider.cs"
            };
            var assemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor);
            foreach (var path in paths)
            {
                Assert.That(File.Exists(path), Is.True, path);
                var assembly = assemblies.Single(a => a.sourceFiles.Any(p => p.Replace('\\', '/').EndsWith(path)));
                Assert.That(assembly.name, Is.EqualTo("Assembly-CSharp-Editor"), path);
            }
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/TEngine/Extension/InputModule/InputSystem_Actions.inputactions"), Is.EqualTo("dc70d2c4f369241dd99afd7c451b813e"));
            Assert.That(assemblies.Any(a => a.name == "TEngine.UI.Editor" || a.name == "GameLogic.Editor" || a.name == "TEngine.UIBinding.Tests.Editor"), Is.False);
        }

        [Test]
        public void BindingMove_PreservesMonoScriptGuidAndSerializedFields()
        {
            const string path = "Assets/GameScripts/HotFix/GameLogic/Module/UIModule/UIBindComponent/UIBindComponent.cs";
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo("da5b4f8f74fed5b43b4b4c25dc3eac22"));
            Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>(path).GetClass(), Is.EqualTo(typeof(UIBindComponent)));
            var binding = _root.AddComponent<UIBindComponent>();
            var serialized = new SerializedObject(binding);
            foreach (var name in new[] { "m_components", "genCodePath", "impCodePath", "className", "isGenImpClass", "uiType" })
                Assert.That(serialized.FindProperty(name), Is.Not.Null, name);
        }

        [Test]
        public void OriginalEditorDirectories_PreserveSettingsAndToolIdentities()
        {
            var expected = new[]
            {
                new[] { "Assets/Editor/UIScriptGenerator/UIScriptGeneratorSetting.cs", "f8a0dfbcd6854965bb6ae78627009a53" },
                new[] { "Assets/Editor/UIScriptGenerator/UIScriptGeneratorSetting.asset", "4672d02bd95489744b57790ba2fb5586" },
                new[] { "Assets/Editor/UIScriptGenerator/UIBindComponentGenerator.cs", "bbd7fea6a5fe435bae9af9b858af8927" },
                new[] { "Assets/Editor/UIScriptGenerator/UIComponentInspectorEditor.cs", "8fd3705a12c240699b959fadf51252a1" },
                new[] { "Assets/Editor/TEngineSettingsProvider/UnityEditorInspectorDrawHelper.cs", "19439a6f179246fc8bf0c45e7e6558bf" },
            };
            foreach (var pair in expected) Assert.That(AssetDatabase.AssetPathToGUID(pair[0]), Is.EqualTo(pair[1]));
            Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>(expected[0][0]).GetClass(), Is.EqualTo(typeof(UIScriptGeneratorSetting)));
            Assert.That(AssetDatabase.LoadAssetAtPath<UIScriptGeneratorSetting>(expected[1][0]), Is.Not.Null);
            Assert.That(typeof(UIScriptGeneratorSettingsProvider).Assembly, Is.SameAs(typeof(UIScriptGenerator).Assembly));
            var playerAssemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Player);
            Assert.That(playerAssemblies.Any(a => a.name == "TEngine.UI.Editor"), Is.False);
        }

        [UnityTest]
        public IEnumerator SettingsPage_DrawsRulesAndSupportsReactivation()
        {
            var provider = UIScriptGeneratorSettingsProvider.CreateUIGeneratorSettingsProvider();
            var listField = typeof(UIScriptGeneratorSettingsProvider).GetField("m_reorderableList", BindingFlags.Static | BindingFlags.NonPublic);
            var host = ScriptableObject.CreateInstance<UISettingsTestWindow>();
            try
            {
                Assert.That(provider.settingsPath, Is.EqualTo("Project/TEngine/UISettings"));
                host.Provider = provider;
                host.position = new Rect(100, 100, 800, 800);
                host.ShowUtility();
                for (var activation = 0; activation < 2; activation++)
                {
                    provider.activateHandler("", host.rootVisualElement);
                    Assert.That(listField.GetValue(null), Is.Null);
                    host.Drawn = false;
                    var deadline = EditorApplication.timeSinceStartup + 5;
                    while (!host.Drawn && EditorApplication.timeSinceStartup < deadline)
                    {
                        host.Repaint();
                        yield return null;
                    }
                    Assert.That(host.Error, Is.Null);
                    Assert.That(host.Drawn, Is.True, "Settings GUI did not render");
                    var list = (UnityEditorInternal.ReorderableList)listField.GetValue(null);
                    Assert.That(list, Is.Not.Null, "Rules disappeared from the settings page");
                    Assert.That(list.serializedProperty.propertyPath, Is.EqualTo("uiScriptGenerateRule"));
                    Assert.That(list.count, Is.EqualTo(_settings.UIScriptGenerateRule.Count));
                    Assert.That(list.count, Is.GreaterThan(0));
                }
            }
            finally
            {
                host.Close();
                SettingsInstance.SetValue(null, _previousSettings);
                provider.activateHandler("", new UnityEngine.UIElements.VisualElement());
            }
        }

        [Test]
        public void HtmlNaming_ReadsProjectGeneratorRules()
        {
            _settings.UIScriptGenerateRule.Insert(0, new UIScriptGenerateRuler("custom_button_", UIComponentName.Button));
            var baker = ScriptableObject.CreateInstance(HtmlBakerType);
            try
            {
                var prefix = HtmlBakerType.GetMethod("GetScriptGeneratorPrefix", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(prefix.Invoke(baker, new object[] { UIComponentName.Button, "fallback" }), Is.EqualTo("custom_button_"));
            }
            finally { Object.DestroyImmediate(baker); }
        }

        private static Type HtmlBakerType => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp-Editor").GetType("HtmlToUGUI.HtmlToUGUIBaker", true);

        [Test]
        public void Rebind_PreservesOrderAndRuntimeAccessWithoutDuplicates()
        {
            Assert.That(UIBindComponentGenerator.GenerateUIComponentScript(_root.transform), Is.True);
            var binding = _root.GetComponent<UIBindComponent>();
            var button = _root.transform.GetChild(0).GetComponent<Button>();
            var image = _root.transform.GetChild(1).GetComponent<Image>();
            Assert.That(binding.GetComponent<Button>(0), Is.SameAs(button));
            Assert.That(binding.GetComponent<Image>(1), Is.SameAs(image));
            Assert.That(UIBindComponentGenerator.GenerateUIComponentScript(_root.transform), Is.True);
            Assert.That(new SerializedObject(binding).FindProperty("m_components").arraySize, Is.EqualTo(2));
            binding.Clear();
            Assert.That(new SerializedObject(binding).FindProperty("m_components").arraySize, Is.Zero);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ClipboardGeneration_UsesHotfixTypeAndIsRepeatable(bool widget, bool async)
        {
            _root.name = widget ? "_itemBinding" : "BindingWindow";
            Assert.That(UIBindComponentGenerator.GenerateCSharpScript(true, async, targetRoot: _root.transform), Is.True);
            var first = EditorGUIUtility.systemCopyBuffer;
            StringAssert.Contains("global::GameLogic.UIBindComponent", first);
            StringAssert.Contains("GetComponent<global::GameLogic.UIBindComponent>()", first);
            StringAssert.Contains(widget ? ": UIWidget" : ": UIWindow", first);
            StringAssert.Contains(async ? "UniTaskVoid" : "partial void", first);
            StringAssert.DoesNotContain("TEngine.Extensions.UI", first);
            Assert.That(UIBindComponentGenerator.GenerateCSharpScript(true, async, targetRoot: _root.transform), Is.True);
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo(first));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BindingOnlyClipboard_UsesHotfixType(bool async)
        {
            Assert.That(UIBindComponentGenerator.GenerateCSharpScript(false, async, targetRoot: _root.transform), Is.True);
            StringAssert.Contains("global::GameLogic.UIBindComponent", EditorGUIUtility.systemCopyBuffer);
            StringAssert.DoesNotContain("TEngine.Extensions.UI", EditorGUIUtility.systemCopyBuffer);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void FileGeneration_PreservesImplementationAndProducesCompilablePair(bool widget, bool async)
        {
            var name = "Binding" + (widget ? "Widget" : "Window") + (async ? "Async" : "Sync");
            var output = Path.GetFullPath(Path.Combine("Temp/UIBindingVerification", name));
            var implementation = Path.Combine(output, "Implementation");
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(implementation);
            var generated = Path.Combine(output, name + "_Gen.g.cs");
            var handwritten = Path.Combine(implementation, name + ".cs");
            if (File.Exists(generated)) File.SetAttributes(generated, FileAttributes.Normal);
            if (File.Exists(handwritten)) File.Delete(handwritten);
            try
            {
                Assert.That(UIBindComponentGenerator.GenerateCSharpScript(true, async, true, output, name,
                    widget ? "UIWidget" : "UIWindow", true, implementation, _root.transform), Is.True);
                var source = File.ReadAllText(generated);
                var imp = File.ReadAllText(handwritten);
                File.AppendAllText(handwritten, "\n// implementation must survive regeneration\n");
                var before = File.ReadAllText(handwritten);
                Assert.That(UIBindComponentGenerator.GenerateCSharpScript(true, async, true, output, name,
                    widget ? "UIWidget" : "UIWindow", true, implementation, _root.transform), Is.True);
                Assert.That(File.ReadAllText(generated), Is.EqualTo(source));
                Assert.That(File.ReadAllText(handwritten), Is.EqualTo(before));
                StringAssert.DoesNotContain("TEngine.Extensions.UI", source + imp);
                var combined = string.Join("\n", new[] { source, imp }.SelectMany(text => text.Split('\n'))
                    .Where(line => line.TrimStart().StartsWith("using ")).Distinct()) + "\n" +
                    string.Join("\n", source.Split('\n').Where(line => !line.TrimStart().StartsWith("using "))) + "\n" +
                    string.Join("\n", imp.Split('\n').Where(line => !line.TrimStart().StartsWith("using ")));
                File.WriteAllText(Path.Combine(output, "Combined.cs"), combined);
            }
            finally
            {
                if (File.Exists(generated)) File.SetAttributes(generated, FileAttributes.Normal);
            }
        }

        [Test]
        public void Inspector_PreservesExistingBindingMetadata()
        {
            var binding = _root.AddComponent<UIBindComponent>();
            var serialized = new SerializedObject(binding);
            serialized.FindProperty("className").stringValue = "ExistingWindow";
            serialized.FindProperty("genCodePath").stringValue = "Assets/Existing/Gen";
            serialized.FindProperty("impCodePath").stringValue = "Assets/Existing/Imp";
            serialized.FindProperty("uiType").stringValue = "UIWindow";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var editor = UnityEditor.Editor.CreateEditor(binding);
            try
            {
                Assert.That(editor, Is.TypeOf<UIComponentInspectorEditor>());
                serialized.Update();
                Assert.That(serialized.FindProperty("className").stringValue, Is.EqualTo("ExistingWindow"));
                Assert.That(serialized.FindProperty("genCodePath").stringValue, Is.EqualTo("Assets/Existing/Gen"));
                Assert.That(serialized.FindProperty("impCodePath").stringValue, Is.EqualTo("Assets/Existing/Imp"));
                Assert.That(serialized.FindProperty("uiType").stringValue, Is.EqualTo("UIWindow"));
            }
            finally { Object.DestroyImmediate(editor); }
        }

        [Test]
        public void Naming_UsesHotfixParentBinding()
        {
            _root.name = "InventoryUI";
            var parent = _root.AddComponent<UIBindComponent>();
            parent.uiType = "UIWindow";
            _settings.UIScriptGenerateRule.Insert(0, new UIScriptGenerateRuler("_item", UIComponentName.RectTransform, true));
            var child = Child("_itemEntry", typeof(UIBindComponent)).GetComponent<UIBindComponent>();
            var naming = typeof(UIGenerationAdapter).Assembly.GetType("TEngine.Editor.UI.UIBindingNaming");
            Assert.That(naming.GetMethod("GetClassName").Invoke(null, new object[] { child, true }), Is.EqualTo("InventoryEntryWidget"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HierarchyGeneration_RemainsIndependentOfBindingComponent(bool async)
        {
            var variables = new StringBuilder();
            var binding = new StringBuilder();
            var events = new StringBuilder();
            var callbacks = new StringBuilder();
            UIScriptGenerator.Ergodic(_root.transform, _root.transform, ref variables, ref binding, ref events, ref callbacks, async);
            StringAssert.Contains("FindChildComponent<Button>", binding.ToString());
            StringAssert.Contains("FindChildComponent<Image>", binding.ToString());
            StringAssert.DoesNotContain("UIBindComponent", binding.ToString());
            StringAssert.Contains(async ? "UniTaskVoid" : "private void", callbacks.ToString());
        }
    }

    internal sealed class UISettingsTestWindow : EditorWindow
    {
        public SettingsProvider Provider;
        public bool Drawn;
        public Exception Error;

        private void OnGUI()
        {
            if (Provider == null || Error != null) return;
            try
            {
                Provider.guiHandler("");
                if (Event.current.type == EventType.Repaint) Drawn = true;
            }
            catch (Exception error) { Error = error; }
        }
    }
}
