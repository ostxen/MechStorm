#if UNITY_EDITOR_WIN && !UNITY_6000_7_OR_NEWER
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using NUnit.Framework;
using Unity.Pipeline.Editor;
using UnityEditor;
using UnityEngine.TestTools;

namespace Unity.Pipeline.Tests.Editor
{
    public class DialogObservationTests
    {
        [UnityTest]
        public IEnumerator ObservesOnlyTestDialogWhileMainThreadIsBlockedThenClearsIt()
        {
            var server = PipelineServerStartup.Server;
            Assert.That(server, Is.Not.Null);
            Assert.That(server.Dialogs.IsDialogOpen, Is.False, "Close existing dialogs before running this test.");
            yield return null;

            string title = "Pipeline dialog test " + Guid.NewGuid().ToString("N");
            uint process = (uint)Process.GetCurrentProcess().Id;
            bool observed = false;
            var watch = Stopwatch.StartNew();
            using (var timer = new Timer(_ =>
            {
                NativeWindows.EnumWindows((window, state) =>
                {
                    NativeWindows.GetWindowThreadProcessId(window, out var owner);
                    if (owner != process)
                        return true;
                    var text = new StringBuilder(256);
                    GetWindowText(window, text, text.Capacity);
                    if (text.ToString() != title)
                        return true;
                    observed = server.Dialogs.CurrentlyOpen.Any(d => d.Title == title && d.Source == "WindowsNative");
                    if (observed || watch.Elapsed.TotalSeconds > 10)
                        PostMessage(window, 0x0111, (IntPtr)1, IntPtr.Zero); // IDOK, our test dialog only.
                    return true;
                }, IntPtr.Zero);
            }, null, 200, 200))
            {
                EditorUtility.DisplayDialog(title, "Temporary Pipeline observation test.", "OK");
            }

            Assert.That(observed, Is.True, "The background observer did not publish the native dialog.");
            double deadline = EditorApplication.timeSinceStartup + 3;
            while (server.Dialogs.IsDialogOpen && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(server.Dialogs.IsDialogOpen, Is.False);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    }
}
#endif
