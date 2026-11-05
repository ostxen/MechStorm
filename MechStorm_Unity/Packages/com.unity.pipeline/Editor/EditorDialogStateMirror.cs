#if UNITY_6000_7_OR_NEWER
using System;
using System.Linq;
using UnityEditor;

namespace Unity.Pipeline.Editor
{
    /// <summary>
    /// Mirrors UnityEditor.EditorDialogEvents into the live pipeline server's DialogStateTracker,
    /// so GET /api/dialog, /api/status and the dialog busy-gate all observe modal
    /// dialogs with no per-command instrumentation (see the trunk-side design doc). Ambient
    /// dialogs are a live-editor-wide concept, not per-test-server — targets the live server
    /// explicitly, the same way EditorProgressMirror does, so a test server's DialogStateTracker
    /// never receives them.
    /// </summary>
    [InitializeOnLoad]
    internal static class EditorDialogStateMirror
    {
        static EditorDialogStateMirror()
        {
            Subscribe();
        }

        /// <summary>Subscribes to <see cref="EditorDialogEvents"/>, replacing any earlier subscription.</summary>
        internal static void Subscribe()
        {
            EditorDialogEvents.dialogWillShow -= OnDialogWillShow;
            EditorDialogEvents.dialogWillShow += OnDialogWillShow;
            EditorDialogEvents.dialogDismissed -= OnDialogDismissed;
            EditorDialogEvents.dialogDismissed += OnDialogDismissed;
        }

        static void OnDialogWillShow(DialogEventInfo info)
        {
            var server = PipelineServerStartup.Server;
            if (server == null)
                return;

            // Still on the main thread here — dialogWillShow fires before the dialog actually
            // blocks it — so this is the last chance to read live Editor state for the dialog
            // busy-gate's editor_status fallback (see SnapshotStatusForDialogGate's doc).
            server.SnapshotStatusForDialogGate();

            server.Dialogs.OnShown(info.Id, info.Source.ToString(), info.Title, info.Message,
                info.ButtonLabels?.ToArray(), info.Level?.ToString(), info.OpenedAtUtc);
        }

        static void OnDialogDismissed(DialogEventInfo info)
        {
            var server = PipelineServerStartup.Server;
            if (server == null)
                return;

            server.Dialogs.OnDismissed(info.Id, DateTime.UtcNow);
        }
    }
}
#elif UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEditor;

namespace Unity.Pipeline.Editor
{
    /// <summary>
    /// Observes this process's owned modal windows without using the blocked Unity main thread.
    /// Windows text is best-effort; this is not an equivalent of EditorDialogEvents.
    /// </summary>
    [InitializeOnLoad]
    internal static class EditorDialogStateMirror
    {
        static readonly object Gate = new object();
        static readonly Dictionary<IntPtr, int> OpenWindows = new Dictionary<IntPtr, int>();
        static readonly uint ProcessId = (uint)Process.GetCurrentProcess().Id;
        static Timer s_Timer;
        static EditorPipelineServer s_Server;
        static int s_NextId;
        static bool s_Stopped;
        static double s_NextSnapshot;

        static EditorDialogStateMirror()
        {
            if (!EditorProcessInfo.IsMainProcess())
                return;
            Subscribe();
        }

        internal static void Subscribe()
        {
            Stop();
            s_Stopped = false;
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            s_Timer = new Timer(Poll, null, 250, 250);
        }

        static void Update()
        {
            lock (Gate)
            {
                var server = PipelineServerStartup.Server;
                if (!ReferenceEquals(server, s_Server))
                {
                    Clear();
                    s_Server = server;
                }
                if (server == null || !server.IsRunning || server.Dialogs.IsDialogOpen
                    || EditorApplication.timeSinceStartup < s_NextSnapshot)
                    return;

                // Older engines have no pre-dialog callback. This is the last idle snapshot,
                // which may precede the modal window by an Editor update.
                server.SnapshotStatusForDialogGate();
                s_NextSnapshot = EditorApplication.timeSinceStartup + 0.25;
            }
        }

        static void Poll(object state)
        {
            // ThreadPool timers may overlap; only one native enumeration may own the state.
            if (!Monitor.TryEnter(Gate))
                return;
            try
            {
                if (s_Stopped || s_Server == null || !s_Server.IsRunning)
                    return;

                var observed = new HashSet<IntPtr>();
                NativeWindows.EnumWindows((window, _) =>
                {
                    NativeWindows.GetWindowThreadProcessId(window, out var process);
                    if (process != ProcessId || !NativeWindows.IsWindowVisible(window))
                        return true;
                    var owner = NativeWindows.GetWindow(window, 4); // GW_OWNER
                    if (owner == IntPtr.Zero || NativeWindows.IsWindowEnabled(owner))
                        return true;
                    NativeWindows.GetWindowThreadProcessId(owner, out var ownerProcess);
                    if (ownerProcess != ProcessId)
                        return true;

                    observed.Add(window);
                    if (!OpenWindows.ContainsKey(window))
                    {
                        var info = NativeWindows.Read(window);
                        int id = ++s_NextId;
                        OpenWindows.Add(window, id);
                        s_Server.Dialogs.OnShown(id, "WindowsNative", info.Title, info.Message,
                            info.Buttons, null, DateTime.UtcNow);
                    }
                    return true;
                }, IntPtr.Zero);

                var closed = new List<IntPtr>();
                foreach (var entry in OpenWindows)
                    if (!observed.Contains(entry.Key))
                    {
                        s_Server.Dialogs.OnDismissed(entry.Value, DateTime.UtcNow);
                        closed.Add(entry.Key);
                    }
                foreach (var window in closed)
                    OpenWindows.Remove(window);
            }
            finally
            {
                Monitor.Exit(Gate);
            }
        }

        static void Clear()
        {
            if (s_Server != null)
                foreach (var id in OpenWindows.Values)
                    s_Server.Dialogs.OnDismissed(id, DateTime.UtcNow);
            OpenWindows.Clear();
        }

        static void Stop()
        {
            EditorApplication.update -= Update;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.quitting -= Stop;
            lock (Gate)
            {
                s_Stopped = true;
                s_Timer?.Dispose();
                s_Timer = null;
                Clear();
                s_Server = null;
            }
        }
    }

    internal static class NativeWindows
    {
        internal delegate bool EnumCallback(IntPtr window, IntPtr state);
        internal struct DialogText
        {
            internal string Title;
            internal string Message;
            internal string[] Buttons;
        }

        internal static DialogText Read(IntPtr window)
        {
            var messages = new List<string>();
            var buttons = new List<string>();
            int count = 0;
            EnumChildWindows(window, (child, _) =>
            {
                // Do not read text-entry controls (paths, passwords, user input).
                var name = new StringBuilder(128);
                GetClassName(child, name, name.Capacity);
                string kind = name.ToString();
                if (kind != "Static" && kind != "Button")
                    return true;
                if (++count > 32)
                    return false;
                string text = ReadText(child);
                if (!string.IsNullOrWhiteSpace(text))
                    (kind == "Button" ? buttons : messages).Add(text);
                return true;
            }, IntPtr.Zero);
            return new DialogText { Title = ReadText(window), Message = string.Join("\n", messages), Buttons = buttons.ToArray() };
        }

        static string ReadText(IntPtr window)
        {
            var text = new StringBuilder(2048);
            // GetWindowText on our own process can block behind its UI thread. Bound the send.
            SendMessageTimeout(window, 0x000D, (IntPtr)text.Capacity, text, 2, 25, out _);
            return text.ToString();
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumCallback callback, IntPtr state);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumChildWindows(IntPtr parent, EnumCallback callback, IntPtr state);
        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr window, StringBuilder name, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr count, StringBuilder text,
            uint flags, uint timeout, out IntPtr result);
    }
}
#endif
