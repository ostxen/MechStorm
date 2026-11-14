#if !UNITY_6000_7_OR_NEWER
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Unity.Pipeline.Editor.Commands;
using Unity.Pipeline.Runtime.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Unity.Pipeline.Tests.Editor
{
    public class ElementCaptureTestWindow : EditorWindow { }

    public class ElementCaptureTests
    {
        [Test]
        public void SelectorSupportsChildClassesAndDisabledState()
        {
            var root = new VisualElement();
            var group = new VisualElement { name = "group" };
            var button = new Button { name = "target" };
            button.AddToClassList("capture-test");
            button.SetEnabled(false);
            root.Add(group);
            group.Add(button);
            Assert.That(VisualElementCaptureSupport.ResolveElement(root, "#group > Button.capture-test:disabled"), Is.SameAs(button));
            Assert.That(VisualElementCaptureSupport.ResolveElement(root, "#group Button:enabled"), Is.Null);
            Assert.That(VisualElementCaptureSupport.ResolveElement(root, "#missing"), Is.Null);
        }

        [Test]
        public void PixelRectangleUsesDpiAndBottomLeftTextureCoordinates()
        {
            Assert.That(PanelElementCapture.PixelRect(new Rect(20, 30, 40, 10), new Rect(10, 10, 100, 100),
                new Vector2(2, 2), 200, 200), Is.EqualTo(new RectInt(20, 140, 80, 20)));
        }

        [Test]
        public void PixelRectangleClipsOutsidePanelAndRejectsInvalidLayout()
        {
            Assert.That(PanelElementCapture.PixelRect(new Rect(-10, -10, 30, 30), new Rect(0, 0, 100, 100),
                Vector2.one, 100, 100), Is.EqualTo(new RectInt(0, 80, 20, 20)));
            Assert.That(PanelElementCapture.PixelRect(new Rect(float.NaN, 0, 20, 20), new Rect(0, 0, 100, 100),
                Vector2.one, 100, 100), Is.EqualTo(new RectInt()));
        }

        [Test]
        public void DetachedElementCaptureFailsClearly()
        {
            Assert.Throws<InvalidOperationException>(() => PanelElementCapture.Capture(new VisualElement(), out _, out _));
        }

        [UnityTest]
        public IEnumerator EditorCommandCapturesSelectedRectangleAndRestoresRenderTarget()
        {
            var window = ScriptableObject.CreateInstance<ElementCaptureTestWindow>();
            var previousFocus = EditorWindow.focusedWindow;
            string path = OutputPath("editor-element");
            try
            {
                window.titleContent = new GUIContent("Pipeline Capture Test");
                window.position = new Rect(200, 200, 320, 240);
                var element = TestElement();
                window.rootVisualElement.Add(element);
                window.ShowUtility();
                window.Focus();
                yield return null;
                yield return null;
                var previousTarget = RenderTexture.active;
                var result = CaptureEditorElementCommand.EditorElement(nameof(ElementCaptureTestWindow), "#target", path);
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.CaptureMode, Is.EqualTo("panel_crop"));
                Assert.That(result.Path, Is.EqualTo(path));
                Assert.That(RenderTexture.active, Is.SameAs(previousTarget));
                AssertPng(result, Color.red);
            }
            finally
            {
                window.Close();
                if (previousFocus != null)
                    previousFocus.Focus();
            }
        }

        [UnityTest]
        public IEnumerator RuntimeCommandCapturesUidocumentWithoutChangingPanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            var host = new GameObject("PipelineCaptureTestHost");
            try
            {
                settings.name = "PipelineCaptureTestPanel";
                var doc = host.AddComponent<UIDocument>();
                doc.panelSettings = settings;
                yield return null;
                var element = TestElement();
                doc.rootVisualElement.Add(element);
                yield return null;
                yield return null;
                var previousTarget = RenderTexture.active;
                var result = CaptureRuntimeElementCommand.RuntimeElement(host.name, "#target", OutputPath("runtime-element"));
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.CaptureMode, Is.EqualTo("panel_crop"));
                Assert.That(settings.targetTexture, Is.Null);
                Assert.That(RenderTexture.active, Is.SameAs(previousTarget));
                AssertPng(result, Color.red);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeCaptureWorksInPlayMode()
        {
            yield return new EnterPlayMode();
            Exception failure = null;
            var capture = RuntimeCommandCapturesUidocumentWithoutChangingPanelSettings();
            while (true)
            {
                bool next;
                try
                {
                    next = capture.MoveNext();
                }
                catch (Exception ex)
                {
                    // Leave Play Mode even when a capture assertion fails.
                    failure = ex;
                    break;
                }
                if (!next)
                    break;
                yield return capture.Current;
            }
            yield return new ExitPlayMode();
            if (failure != null)
                throw failure;
        }

        static VisualElement TestElement()
        {
            var element = new VisualElement { name = "target" };
            element.style.position = Position.Absolute;
            element.style.left = 20;
            element.style.top = 30;
            element.style.width = 80;
            element.style.height = 40;
            element.style.backgroundColor = Color.red;
            var marker = new VisualElement();
            marker.style.position = Position.Absolute;
            marker.style.left = 0;
            marker.style.top = 0;
            marker.style.width = 20;
            marker.style.height = 10;
            marker.style.backgroundColor = Color.blue;
            element.Add(marker);
            return element;
        }

        static string OutputPath(string name)
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "pipeline-screenshots",
                name + "-" + Guid.NewGuid().ToString("N") + ".png");
        }

        static void AssertPng(CaptureElementResponse result, Color expected)
        {
            Assert.That(File.Exists(result.Path), Is.True);
            Assert.That(result.Width, Is.GreaterThan(0));
            Assert.That(result.Height, Is.GreaterThan(0));
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(result.Path)), Is.True);
                Assert.That(texture.width, Is.EqualTo(result.Width));
                Assert.That(texture.height, Is.EqualTo(result.Height));
                Assert.That((float)texture.width / texture.height, Is.EqualTo(2).Within(0.05));
                var center = texture.GetPixel(texture.width / 2, texture.height / 2);
                Assert.That(center.r, Is.EqualTo(expected.r).Within(0.1));
                Assert.That(center.g, Is.EqualTo(expected.g).Within(0.1));
                Assert.That(center.b, Is.EqualTo(expected.b).Within(0.1));
                var topLeft = texture.GetPixel(texture.width / 8, texture.height * 7 / 8);
                Assert.That(topLeft.b, Is.GreaterThan(0.9), "Top-left blue marker must not be vertically flipped.");
                Assert.That(topLeft.r, Is.LessThan(0.1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
#endif
