using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor;
using Unity.Pipeline.Editor.Commands;
using Unity.Pipeline.Editor.Testing;
using Unity.Pipeline.Runtime.Commands;
using Unity.Pipeline.Threading;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Pipeline.Tests.Editor
{
    public class PipelineVersionCompatibilityTests
    {
        [Test]
        public void BuildSettingsReadNativeTargetIl2CppMode()
        {
            var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            var expected = PlayerSettings.GetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group));
            var settings = (Unity.Pipeline.Editor.Commands.Build.BuildSettingsResult)
                Unity.Pipeline.Editor.Commands.Build.BuildConfigCommands.GetSettings();
            Assert.That(settings.Il2CppCodeGeneration, Is.EqualTo(expected.ToString()));
        }

        [Test]
        public void BuildSettingsDryRunDoesNotWriteNativeIl2CppMode()
        {
            var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            var target = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            var before = PlayerSettings.GetIl2CppCodeGeneration(target);
            var requested = before == Il2CppCodeGeneration.OptimizeSpeed
                ? Il2CppCodeGeneration.OptimizeSize : Il2CppCodeGeneration.OptimizeSpeed;
            var result = (Unity.Pipeline.Editor.Commands.Build.SetBuildSettingsResult)
                Unity.Pipeline.Editor.Commands.Build.BuildConfigCommands.SetSettings(
                    new Unity.Pipeline.Editor.Commands.Build.SetBuildSettingsInput { Il2CppCodeGeneration = requested.ToString() },
                    dryRun: true);
            Assert.That(result.Success && result.DryRun, Is.True);
            Assert.That(result.Applied["il2cpp-code-generation"], Is.EqualTo(requested.ToString()));
            Assert.That(PlayerSettings.GetIl2CppCodeGeneration(target), Is.EqualTo(before));
        }
#if !UNITY_6000_0_OR_NEWER
        [TestCase(true)]
        [TestCase(false)]
        public void UnavailableBuildProfileIsRefusedWithoutQueueing(bool dryRun)
        {
            // Never authorize a real build, even if the unavailable-feature guard regresses.
            var status = typeof(Unity.Pipeline.Editor.Commands.Build.BuildCommand).GetMethod("Status");
            var before = status.Invoke(null, null).ToString();
            var result = Unity.Pipeline.Editor.Commands.Build.BuildCommand.Run(
                profileName: "Pipeline compatibility probe", confirm: false, dryRun: dryRun);
            Assert.That(result.GetType().GetProperty("code")?.GetValue(result), Is.EqualTo("feature_unavailable"));
            Assert.That(result.GetType().GetProperty("success")?.GetValue(result), Is.False);
            Assert.That(result.GetType().GetProperty("valid")?.GetValue(result), Is.False);
            Assert.That(status.Invoke(null, null).ToString(), Is.EqualTo(before));
        }
#endif

        [Test]
        public void PerformanceSnapshotRetainsRenderAndMemoryData()
        {
            var stats = Unity.Pipeline.Editor.Commands.Observability.PerformanceCommands.GetPerformance();
            Assert.That(stats.Render, Is.Not.Null);
            Assert.That(stats.Memory.TotalAllocatedBytes, Is.GreaterThan(0));
            Assert.That(stats.Memory.TotalReservedBytes, Is.GreaterThanOrEqualTo(stats.Memory.TotalAllocatedBytes));
            if (stats.FrameTiming.Available)
            {
                Assert.That(stats.FrameTiming.CpuMainThreadFrameTimeMs.HasValue, Is.True);
            }
        }

        [Test]
        public void ObjectQueryMatchesNativeUnsortedQuery()
        {
            var active = new GameObject("Pipeline active query probe");
            var inactive = new GameObject("Pipeline inactive query probe");
            try
            {
                inactive.SetActive(false);
                var actual = PipelineUtils.FindObjectsByType<Transform>();
#if UNITY_6000_4_OR_NEWER
                var expected = Object.FindObjectsByType<Transform>();
#else
                var expected = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
#endif
                // Transform is IEnumerable: compare object identity, not NUnit's child collections.
                Assert.That(actual.Length, Is.EqualTo(expected.Length));
                for (var i = 0; i < actual.Length; i++)
                    Assert.That(actual[i] == expected[i], Is.True);
                Assert.That(Array.Exists(actual, t => t == active.transform), Is.True);
                Assert.That(Array.Exists(actual, t => t == inactive.transform), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(active);
                Object.DestroyImmediate(inactive);
            }
        }

        [TestCase(-12345)]
        [TestCase(12345)]
        public void ObjectIdRoundTripsSignedInstanceIds(int value)
        {
#if !UNITY_6000_4_OR_NEWER
            var id = new ObjectId(value);
            Assert.That(ObjectId.Parse(id.ToString()), Is.EqualTo(id));
            Assert.That(ObjectId.TryParse(id.ToString(), out var parsed), Is.True);
            Assert.That(parsed.RawValue, Is.EqualTo(value));
#else
            var id = ObjectId.FromRaw((ulong)Math.Abs(value));
            Assert.That(ObjectId.Parse(id.ToString()), Is.EqualTo(id));
#endif
        }

        [TestCase(-1)]
        [TestCase(2450)]
        public void MaterialPropertiesPreserveInheritedAndExplicitQueue(int queue)
        {
            var shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                material.renderQueue = queue;
                var result = Unity.Pipeline.Editor.Commands.Materials.MaterialCommands.GetProperties(
                    new Unity.Pipeline.Models.ObjectRef { InstanceId = material.GetInstanceID() });
                Assert.That(result.RenderQueue, Is.EqualTo(queue));
                Assert.That(new SerializedObject(material).FindProperty("m_CustomRenderQueue").intValue, Is.EqualTo(queue));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void ConsoleGroundTruthSamplesNativeCounts()
        {
            var type = typeof(TestCommands).Assembly.GetType("Unity.Pipeline.Editor.Console.EditorConsoleGroundTruth");
            Assert.That(type, Is.Not.Null);
            type.GetMethod("SampleForTests", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            var property = typeof(Unity.Pipeline.Console.ConsoleLogCapture)
                .GetProperty("GroundTruth", BindingFlags.Static | BindingFlags.NonPublic);
            var snapshot = property.GetValue(null);
            Assert.That(snapshot, Is.Not.Null);
            var args = new object[] { 0, 0, 0 };
            typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries")
                .GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args);
            foreach (var entry in new[] { "ConsoleErrors", "ConsoleWarnings", "ConsoleLogs" })
            {
                var index = entry == "ConsoleErrors" ? 0 : entry == "ConsoleWarnings" ? 1 : 2;
                Assert.That(snapshot.GetType().GetProperty(entry).GetValue(snapshot), Is.EqualTo(args[index]));
            }
        }
    }

    public class PipelineAutoTickTests
    {
        bool m_Enabled, m_Explicit, m_HasPersisted, m_PersistedEnabled;
        long m_Interval, m_PersistedInterval;

        [SetUp]
        public void SaveState()
        {
            m_Enabled = AutoTickCommand.IsEnabled;
            m_Interval = AutoTickCommand.CurrentIntervalMs;
            m_Explicit = (bool)typeof(AutoTickCommand).GetField("s_ExplicitChoice", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            m_HasPersisted = AutoTickCommand.TryGetPersistedSessionForTests(out m_PersistedEnabled, out m_PersistedInterval);
            AutoTickCommand.ResetForTests();
            AutoTickCommand.EraseSessionForTests();
        }

        [TearDown]
        public void RestoreState()
        {
            AutoTickCommand.ResetForTests();
            AutoTickCommand.EraseSessionForTests();
            if (m_Explicit)
                AutoTickCommand.SetAutoTick(m_Enabled, (int)m_Interval, persist: false);
            else
                AutoTickCommand.RestoreFromSession(m_Enabled);
            if (m_HasPersisted)
                AutoTickCommand.SetPersistedSessionForTests(m_PersistedEnabled, m_PersistedInterval);
        }

        [Test]
        public void ServerRequestIsReleasedAndCanBeReacquired()
        {
            AutoTickCommand.RestoreFromSession(true);
            Assert.That(AutoTickCommand.IsEnabled, Is.True);
            AutoTickCommand.ReleaseServerRequest();
            Assert.That(AutoTickCommand.IsEnabled, Is.False);
            AutoTickCommand.RestoreFromSession(true);
            Assert.That(AutoTickCommand.IsEnabled, Is.True);
        }

        [Test]
        public void DisablingWatchdogReleasesImplicitTick()
        {
            AutoTickCommand.RestoreFromSession(true);
            AutoTickCommand.RestoreFromSession(false);
            Assert.That(AutoTickCommand.IsEnabled, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExplicitSessionChoiceWinsOverServerLifecycle(bool enabled)
        {
            AutoTickCommand.SetAutoTick(enabled, 73, persist: false);
            AutoTickCommand.ReleaseServerRequest();
            AutoTickCommand.RestoreFromSession(!enabled);
            Assert.That(AutoTickCommand.IsEnabled, Is.EqualTo(enabled));
            Assert.That(AutoTickCommand.CurrentIntervalMs, Is.EqualTo(73));
        }

        [Test]
        public void PersistedChoiceIsRestoredAfterReload()
        {
            AutoTickCommand.SetAutoTick(true, 81);
            AutoTickCommand.ResetForTests();
            AutoTickCommand.RestoreFromSession(false);
            AutoTickCommand.ReleaseServerRequest();
            Assert.That(AutoTickCommand.IsEnabled, Is.True);
            Assert.That(AutoTickCommand.CurrentIntervalMs, Is.EqualTo(81));
        }
    }

    public class PipelineCancellationTests
    {
        sealed class AsyncProbe
        {
            internal readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>();
            internal readonly TaskCompletionSource<bool> Resume = new TaskCompletionSource<bool>();
            internal bool Observed;
            public async Task<bool> Run()
            {
                Started.TrySetResult(true);
                await Resume.Task;
                Observed = PipelineCancellation.IsCancellationRequested;
                return Observed;
            }
        }

        [UnityTest]
        public IEnumerator GeneralAsyncCommandRetainsCancellationAfterAwaitAndReleasesGate()
        {
            var dispatcher = new Dispatcher();
            dispatcher.Initialize();
            var probe = new AsyncProbe();
            var next = new AsyncProbe();
            next.Resume.SetResult(true);
            var queue = new DefaultCommandQueue(dispatcher, _ => { });
            CommandExecution execution = null;
            try
            {
                execution = Enqueue(queue, probe);
                var deadline = EditorApplication.timeSinceStartup + 5;
                while (!probe.Started.Task.IsCompleted && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(probe.Started.Task.IsCompleted, Is.True);
                execution.Cancel();
                probe.Resume.TrySetResult(true);
                while (execution.State != CommandExecutionState.Ended && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(execution.State, Is.EqualTo(CommandExecutionState.Ended));
                Assert.That(execution.Result, Is.EqualTo(ExitCode.Cancelled));
                Assert.That(probe.Observed, Is.True, "Cancellation must flow beyond the invocation's synchronous scope.");

                var following = Enqueue(queue, next);
                while (following.State != CommandExecutionState.Ended && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(following.State, Is.EqualTo(CommandExecutionState.Ended));
                Assert.That(next.Observed, Is.False, "The next command must not inherit the previous token.");
            }
            finally
            {
                execution?.Cancel();
                probe.Resume.TrySetResult(true);
                dispatcher.Shutdown();
            }
        }

        static CommandExecution Enqueue(DefaultCommandQueue queue, AsyncProbe probe)
        {
            var method = typeof(AsyncProbe).GetMethod(nameof(AsyncProbe.Run));
            var command = new CommandInfo("probe", "test", false, method, Array.Empty<CommandParameterInfo>(), target: probe);
            return queue.Enqueue(command, null, Array.Empty<object>(),
                new CommandRequest("probe", null, null, TextWriter.Null, TextWriter.Null) { Timeout = TimeSpan.FromSeconds(5) });
        }

        [Test]
        public void TestCancelBypassesOrdinaryCommandGate()
        {
            Assert.That(Array.Exists(typeof(TestCommands).GetMethod(nameof(TestCommands.Cancel)).GetCustomAttributes(false),
                attribute => attribute.GetType().Name == "ImmediateCommandAttribute"), Is.True);
        }

        [Test]
        public void CollectorCancellationCompletesActualSyncWaiter()
        {
            var collector = new TestResultCollector();
            var task = collector.WaitForCompletionAsync();
            collector.Cancel();
            collector.Cancel();
            Assert.That(task.IsCanceled, Is.True);
        }

        [UnityTest]
        public IEnumerator TimerTimeoutResumesCleanupOnEditorThread()
        {
            int editorThread = Thread.CurrentThread.ManagedThreadId;
            int cleanupThread = -1;
            Task operation;
            using (var cts = new CancellationTokenSource(30))
            {
                operation = AwaitAndCleanup(cts.Token, () => cleanupThread = Thread.CurrentThread.ManagedThreadId);
                var deadline = EditorApplication.timeSinceStartup + 5;
                while (!operation.IsCompleted && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(operation.IsCompleted, Is.True);
                Assert.That(operation.IsFaulted, Is.False);
                Assert.That(cleanupThread, Is.EqualTo(editorThread));
            }
        }

        static async Task AwaitAndCleanup(CancellationToken token, Action cleanup)
        {
            try { await PipelineTestRunner.AwaitUnlessCanceled(new TaskCompletionSource<bool>().Task, token); }
            catch (OperationCanceledException) { }
            finally { cleanup(); }
        }

        [UnityTest]
        public IEnumerator RepeatedTestDiscoveryDoesNotRetainApiObjects()
        {
            int before = Resources.FindObjectsOfTypeAll<TestRunnerApi>().Length;
            for (int i = 0; i < 3; i++)
            {
                var task = TestCommands.List("editor");
                var deadline = EditorApplication.timeSinceStartup + 15;
                while (!task.IsCompleted && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(task.IsCompleted && !task.IsFaulted, Is.True);
                Assert.That(task.Result.Success, Is.True);
                Assert.That(task.Result.Count, Is.GreaterThan(0));
            }
            Assert.That(Resources.FindObjectsOfTypeAll<TestRunnerApi>().Length, Is.EqualTo(before));
        }
    }

#if !UNITY_6000_7_OR_NEWER
    public class PipelineCaptureTests
    {
        [TestCase(1f)]
        [TestCase(1.5f)]
        [TestCase(2f)]
        public void PixelRectPreservesDpiAndBottomOrigin(float scale)
        {
            var rect = PanelElementCapture.PixelRect(new Rect(10, 20, 30, 40), new Rect(0, 0, 100, 100),
                new Vector2(scale, scale), (int)(100 * scale), (int)(100 * scale));
            Assert.That(rect, Is.EqualTo(new RectInt((int)(10 * scale), (int)(40 * scale), (int)(30 * scale), (int)(40 * scale))));
        }

        [UnityTest]
        public IEnumerator RuntimeCaptureDoesNotRepaintOrModifyExistingTarget()
        {
            // Use the Test Framework's untitled isolation scene; never save or replace user scenes.
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Assert.That(scene.path, Is.Empty, "Runtime capture objects must stay in the test runner's isolation scene.");
            var settings = CreateTransientPanelSettings();
            var target = new RenderTexture(100, 100, 24);
            var previous = RenderTexture.active;
            GameObject go = null;
            Texture2D decoded = null;
            try
            {
                target.Create();
                settings.targetTexture = target;
                settings.clearColor = false;
                go = new GameObject("Pipeline capture regression");
                var document = go.AddComponent<UIDocument>();
                document.panelSettings = settings;
                document.rootVisualElement.style.width = 100;
                document.rootVisualElement.style.height = 100;
                document.rootVisualElement.style.backgroundColor = new Color(1, 0, 0, .5f);
                // EditMode has no player render loop to size the runtime panel's container.
                var panel = document.rootVisualElement.panel;
                Assert.That(panel, Is.Not.Null);
                panel.visualTree.style.width = 100;
                panel.visualTree.style.height = 100;
                panel.Pick(Vector2.zero);
                var deadline = EditorApplication.timeSinceStartup + 5;
                while ((document.rootVisualElement.panel == null || document.rootVisualElement.worldBound.width < 1)
                    && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(document.rootVisualElement.panel, Is.Not.Null);
                Assert.That(panel.visualTree.worldBound.width, Is.GreaterThan(0));
                Assert.That(panel.visualTree.worldBound.height, Is.GreaterThan(0));
                RenderTexture.active = target;
                GL.Clear(true, true, Color.green);
                RenderTexture.active = previous;

                decoded = new Texture2D(2, 2);
                for (int i = 0; i < 2; i++)
                {
                    var png = PanelElementCapture.Capture(document.rootVisualElement, out var w, out var h);
                    Assert.That(w, Is.GreaterThan(0));
                    Assert.That(h, Is.GreaterThan(0));
                    Assert.That(decoded.LoadImage(png), Is.True);
                    var pixel = decoded.GetPixel(w / 2, h / 2);
                    Assert.That(pixel.g, Is.GreaterThan(.95f));
                    Assert.That(pixel.r, Is.LessThan(.05f), "A repaint would blend the red panel into the green business target.");
                    Assert.That(RenderTexture.active, Is.EqualTo(previous));
                }
            }
            finally
            {
                RenderTexture.active = previous;
                if (go != null) Object.DestroyImmediate(go);
                Object.DestroyImmediate(settings);
                if (decoded != null) Object.DestroyImmediate(decoded);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        static PanelSettings CreateTransientPanelSettings()
        {
            // PanelSettings.Reset otherwise creates a default theme asset on disk in the Editor.
            var factory = typeof(PanelSettings).GetField("GetOrCreateDefaultTheme", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(factory, Is.Not.Null);
            var previous = factory.GetValue(null);
            try
            {
                factory.SetValue(null, null);
                return ScriptableObject.CreateInstance<PanelSettings>();
            }
            finally
            {
                factory.SetValue(null, previous);
            }
        }

        [UnityTest]
        public IEnumerator EditorElementCaptureReturnsCroppedPngWithoutIntermediateResponse()
        {
            var window = ScriptableObject.CreateInstance<PipelineCaptureTestWindow>();
            Texture2D decoded = null;
            try
            {
                window.position = new Rect(100, 100, 320, 240);
                var element = new VisualElement();
                element.style.width = 80;
                element.style.height = 40;
                element.style.backgroundColor = Color.green;
                window.rootVisualElement.Add(element);
                window.ShowUtility();
                var deadline = EditorApplication.timeSinceStartup + 5;
                while (element.worldBound.width < 1 && EditorApplication.timeSinceStartup < deadline) yield return null;
                var png = CaptureEditorElementCommand.CaptureWindowElement(window, element, out var w, out var h);
                decoded = new Texture2D(2, 2);
                Assert.That(decoded.LoadImage(png), Is.True);
                Assert.That(decoded.width, Is.EqualTo(w));
                Assert.That(decoded.height, Is.EqualTo(h));
                Assert.That(w, Is.LessThan(320 * EditorGUIUtility.pixelsPerPoint));
                var center = decoded.GetPixel(w / 2, h / 2);
                Assert.That(center.g, Is.GreaterThan(.8f));
                Assert.That(center.r, Is.LessThan(.2f));
            }
            finally
            {
                if (decoded != null) Object.DestroyImmediate(decoded);
                window.Close();
                if (window != null) Object.DestroyImmediate(window);
            }
        }
    }

    public class PipelineCaptureTestWindow : EditorWindow { }
#endif
}
