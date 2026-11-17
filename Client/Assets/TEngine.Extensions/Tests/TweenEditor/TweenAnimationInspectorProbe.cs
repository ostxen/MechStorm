using System;
using UnityEditor;
using UnityEngine;

namespace TEngine.Extensions.Tests.TweenEditor
{
    internal sealed class TweenAnimationInspectorProbe : EditorWindow
    {
        internal SerializedObject Data;
        internal Exception Error;
        internal int Draws;
        internal bool RepaintOnly;
        internal object Panel;
        internal float MeasuredHeight, DrawnHeight;
        internal EditorWindow DrawingWindow;
        internal double RepaintMilliseconds;
        internal double HeightMilliseconds;
        internal int DrawnItems, LastDrawnIndex;
        internal bool ScrollToBottom;
        internal readonly System.Collections.Generic.List<double> RepaintTimes = new System.Collections.Generic.List<double>();
        private void OnGUI()
        {
            if (Data == null) return;
            if (RepaintOnly && Event.current.type != EventType.Repaint) return;
            try
            {
                Data.Update();
                if (Panel != null)
                {
                    var type = Panel.GetType();
                    const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    var heightStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    MeasuredHeight = (float)type.GetMethod("GetHeight", flags).Invoke(Panel, new object[] { position.width });
                    HeightMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - heightStart) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                    var start = System.Diagnostics.Stopwatch.GetTimestamp();
                    type.GetMethod("OnGUI", flags).Invoke(Panel, new object[] { new Rect(0,
                        ScrollToBottom ? position.height - MeasuredHeight : 0, position.width, MeasuredHeight) });
                    if (Event.current.type == EventType.Repaint)
                        RepaintMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                    DrawnHeight = (float)type.GetProperty("LastDrawHeight", flags).GetValue(Panel);
                    DrawnItems = (int)type.GetProperty("LastDrawnItems", flags).GetValue(Panel);
                    LastDrawnIndex = (int)type.GetProperty("LastDrawnIndex", flags).GetValue(Panel);
                    var repaint = type.Assembly.GetType("TEngine.Extensions.Editor.Tween.TweenInspectorRepaint");
                    DrawingWindow = (EditorWindow)repaint.GetProperty("CurrentWindow", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
                }
                else if (RepaintOnly)
                {
                    // 默认优化 Inspector 可能只调用 Repaint，不为 PropertyDrawer 建立 GUILayout 树。
                    var y = 0f;
                    foreach (var name in new[] { "Unrelated", "First", "Second" })
                    {
                        var property = Data.FindProperty(name);
                        var height = EditorGUI.GetPropertyHeight(property, true);
                        EditorGUI.PropertyField(new Rect(0, y, position.width, height), property, true);
                        y += height + 4;
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(Data.FindProperty("Unrelated"));
                    EditorGUILayout.PropertyField(Data.FindProperty("First"), true);
                    EditorGUILayout.PropertyField(Data.FindProperty("Second"), true);
                }
                Data.ApplyModifiedProperties();
                if (Event.current.type == EventType.Repaint)
                {
                    Draws++;
                    RepaintTimes.Add(EditorApplication.timeSinceStartup);
                }
            }
            catch (Exception exception) { Error = exception; }
        }
    }
}
