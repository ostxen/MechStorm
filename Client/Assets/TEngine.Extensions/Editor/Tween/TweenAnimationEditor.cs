using System;
using System.Collections.Generic;
using System.Globalization;
using LitMotion;
using TEngine.Extensions.Tween;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;

namespace TEngine.Extensions.Editor.Tween
{
    internal sealed class TweenAnimationInspector
    {
        private ReorderableList m_SequenceList;
        private SerializedProperty m_ItemCursor;
        private int m_ItemCursorIndex;

        private void ResetItemCursor()
        {
            m_ItemCursor?.Dispose();
            m_ItemCursor = null;
        }

        private SerializedProperty GetItem(int index)
        {
            if (m_ItemCursor == null || index < m_ItemCursorIndex)
            {
                ResetItemCursor();
                m_ItemCursor = m_SequenceList.serializedProperty.GetArrayElementAtIndex(index);
                m_ItemCursorIndex = index;
            }
            while (m_ItemCursorIndex < index)
            {
                m_ItemCursor.Next(false);
                m_ItemCursorIndex++;
            }
            return m_ItemCursor;
        }
        private bool m_ShowSettings;
        private bool m_ShowLifecycle;
        private bool m_ShowAdvanced;
        private bool m_ShowReverse;
        private bool m_ShowEvents;
        private bool m_ShowStagger;
        private int m_StaggerFirst = 1, m_StaggerLast = 1;
        private float m_StaggerInterval = .1f;
        private int m_DragItem = -1, m_DragUndo = -1, m_DragControl;
        private bool m_DragDuration;
        private float m_DragMouseX, m_DragStart, m_DragItemDuration;
        private double m_DragRange;
        private bool m_TimingHover;
        private Rect m_TimingFeedbackArea;
        private GUIStyle m_TimingFeedbackStyle;
        private TweenAnimation.TimelineSnapshot m_Timeline;
        private bool m_TimelinePreview;
        private double m_Elapsed;
        private string m_TimelineMessage;
        private readonly List<string> m_ValidationErrors = new List<string>();
        private readonly List<string> m_Warnings = new List<string>();

        private readonly SerializedObject serializedObject;
        private readonly string m_Path;
        private readonly Action m_Repaint;
        private UnityEngine.Object target => serializedObject.targetObject;
        private UnityEngine.Object[] targets => serializedObject.targetObjects;
        private TweenAnimationBinding Binding => new TweenAnimationBinding(target, m_Path);
        private SerializedProperty FindProperty(string name) => serializedObject.FindProperty(Binding.PropertyPath(name));
        private void Repaint() => m_Repaint?.Invoke();
        internal TweenAnimationInspector(SerializedObject data, string path, Action repaint)
        {
            serializedObject = data;
            m_Path = path;
            m_Repaint = repaint;
            var sequence = FindProperty("m_Sequence");
            m_SequenceList = new ReorderableList(serializedObject, sequence, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, $"动画列表（{sequence.arraySize}项）");
                    var button = new Rect(rect.xMax - 70f, rect.y, 70f, rect.height);
                    if (GUI.Button(button, "全部折叠", EditorStyles.miniButton))
                        for (var i = 0; i < sequence.arraySize; i++) sequence.GetArrayElementAtIndex(i).isExpanded = false;
                },
                drawElementCallback = (rect, index, active, focused) =>
                {
                    LastDrawnItems++;
                    LastDrawnIndex = index;
                    EditorGUI.PropertyField(rect, GetItem(index), new GUIContent($"第 {index + 1} 项"), true);
                    DrawItemTimeline(rect, index);
                },
                elementHeightCallback = index =>
                    EditorGUI.GetPropertyHeight(GetItem(index), GUIContent.none, true) + 4f,
                onAddCallback = list =>
                {
                    sequence.InsertArrayElementAtIndex(sequence.arraySize);
                    var item = sequence.GetArrayElementAtIndex(sequence.arraySize - 1);
                    // Unity 新增数组项时会复制末项，这里清空引用和事件。
                    item.FindPropertyRelative("Target").objectReferenceValue = null;
                    item.FindPropertyRelative("FromTarget").objectReferenceValue = null;
                    item.FindPropertyRelative("ToTarget").objectReferenceValue = null;
                    foreach (var name in new[] { "OnPlay", "OnUpdate", "OnComplete" })
                        item.FindPropertyRelative(name).FindPropertyRelative("m_PersistentCalls.m_Calls").ClearArray();
                    item.isExpanded = true;
                    item.FindPropertyRelative("SpeedBased").isExpanded = false;
                    item.FindPropertyRelative("CustomEase").isExpanded = false;
                }
            };
        }

        internal void Dispose()
        {
            TweenInspectorRepaint.ForgetPreview(this);
            ResetItemCursor();
            EndTimingDrag();
            foreach (var item in targets)
                if (TweenAnimationPreview.ContainsBinding(new TweenAnimationBinding(item, m_Path))) TweenAnimationPreview.Stop();
        }

        private Rect m_Content;
        private float m_NextY;
        private bool m_Measuring;
        internal float LastDrawHeight { get; private set; }
        internal int LastDrawnItems { get; private set; }
        internal int LastDrawnIndex { get; private set; }
        private const float RowGap = 2f;
        private static float RowHeight => EditorGUIUtility.singleLineHeight;
        private const string SinglePreviewMessage = "单项预览：不代表整个序列的合成效果，不应用序列设置。停止后恢复原值。";
        private const string StaggerMessage = "指定连续项范围，以首项编排起点为基准设置 Insert；保留各项原延迟。后续 Chain 可能随范围结束时间调整。";

        private Rect NextRect(float height = -1)
        {
            if (height < 0) height = RowHeight;
            var rect = new Rect(m_Content.x, m_NextY, m_Content.width, height);
            m_NextY += height + RowGap;
            return rect;
        }

        private static float HelpHeight(string message, float width) =>
            Mathf.Max(38, EditorStyles.helpBox.CalcHeight(new GUIContent(message), Mathf.Max(40, width)));

        private void HelpBox(string message, MessageType type)
        {
            var rect = NextRect(HelpHeight(message, m_Content.width));
            if (!m_Measuring) EditorGUI.HelpBox(rect, message, type);
        }

        private bool Foldout(bool value, string label)
        {
            var rect = NextRect();
            return m_Measuring ? value : EditorGUI.Foldout(rect, value, label, true);
        }

        internal float GetHeight(float width)
        {
            ResetItemCursor();
            RefreshValidation();
            var content = m_Content;
            var y = m_NextY;
            m_Content = new Rect(0, 0, width, 0);
            m_NextY = 0;
            m_Measuring = true;
            try { DrawContents(); return m_NextY; }
            finally { ResetItemCursor(); m_Measuring = false; m_Content = content; m_NextY = y; }
        }

        internal void OnInspectorGUI()
        {
            serializedObject.Update();
            var height = GetHeight(EditorGUIUtility.currentViewWidth - 40);
            OnGUI(GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true)));
            serializedObject.ApplyModifiedProperties();
        }

        internal void OnGUI(Rect position)
        {
            ResetItemCursor();
            m_Content = position;
            m_NextY = position.y;
            var wasTimingHover = m_TimingHover;
            m_TimingHover = false;
            LastDrawnItems = 0;
            LastDrawnIndex = -1;
            RefreshValidation();
            RefreshTimeline();
            DrawContents();
            LastDrawHeight = m_NextY - position.y;
            DrawTimingFeedback();
            if (wasTimingHover != m_TimingHover || Event.current.type == EventType.MouseMove) Repaint();
            ResetItemCursor();
            TweenInspectorRepaint.ObservePreview(this, TweenInspectorRepaint.CurrentWindow);
        }

        private void DrawContents()
        {
            if (!Application.isPlaying) DrawPlaybackToolbar();
            if (TweenAnimationPreview.IsEndpoint && TweenAnimationPreview.ContainsBinding(Binding))
            {
                HelpBox(TweenAnimationPreview.EndpointDescription, MessageType.Info);
                var rect = NextRect();
                if (!m_Measuring && GUI.Button(rect, "结束并恢复")) TweenAnimationPreview.Stop();
            }
            if (m_Measuring) DrawEditableContents();
            else using (new EditorGUI.DisabledScope(TweenAnimationPreview.IsActive)) DrawEditableContents();
            if (m_ShowAdvanced && !Application.isPlaying) DrawReverse();
            if (m_Measuring) DrawSequenceEvents();
            else using (new EditorGUI.DisabledScope(TweenAnimationPreview.IsActive)) DrawSequenceEvents();
            // 单选的动画项错误已在列表就近显示；多选仍列出每个对象的错误。
            foreach (var error in m_ValidationErrors)
                if (ShouldShowValidationText(error, serializedObject.isEditingMultipleObjects))
                    HelpBox(error, MessageType.Error);
            foreach (var warning in m_Warnings) HelpBox(warning, MessageType.Warning);
        }

        private void DrawEditableContents()
        {
            var rect = NextRect(m_SequenceList.GetHeight());
            if (!m_Measuring)
            {
                var visible = TweenInspectorRepaint.VisibleRect;
                visible.y -= rect.y;
                m_SequenceList.DoList(rect, visible);
            }
            DrawStagger();
            m_NextY += 2;
            DrawSequenceSettings();
            DrawLifecycle();
        }

        internal static bool ShouldShowValidationText(string error, bool multiple)
        {
            var item = error.IndexOf(" 项: ", StringComparison.Ordinal);
            if (item < 0) return true;
            return multiple && !TweenAnimationItemDrawer.IsReferenceValidationError(error.Substring(item + 4));
        }

        private void RefreshValidation()
        {
            m_ValidationErrors.Clear();
            m_Warnings.Clear();
            foreach (var item in targets)
            {
                var error = new TweenAnimationBinding(item, m_Path).Animation?.GetValidationError();
                if (error != null) m_ValidationErrors.Add(error);
            }
            if (!serializedObject.isEditingMultipleObjects)
                m_Warnings.AddRange(TweenAnimationDiagnostics.Collect(serializedObject.FindProperty(m_Path)));
        }

        internal bool RequiresConstantRepaint() => m_DragItem >= 0 || TweenAnimationPreview.ContainsBinding(Binding)
            && !TweenAnimationPreview.IsPaused && !TweenAnimationPreview.IsEndpoint;

        private void RefreshTimeline()
        {
            m_Timeline = null;
            m_TimelineMessage = "多选或运行模式不显示时间轴";
            if (TweenAnimationPreview.IsSingle) { m_TimelinePreview = false; m_TimelineMessage = "单项预览不显示整序列进度"; return; }
            if (targets.Length != 1 || Application.isPlaying) return;
            var sequence = Binding;
            m_Timeline = TweenAnimationPreview.GetTimelineBinding(sequence);
            m_TimelinePreview = m_Timeline != null;
            if (!m_TimelinePreview)
            {
                m_TimelineMessage = "配置错误，时间轴不可用";
                if (m_ValidationErrors.Count != 0) return;
                try { m_Timeline = sequence.Animation.CaptureTimeline(); }
                catch (InvalidOperationException exception) { m_TimelineMessage = exception.Message; return; }
            }
            m_Elapsed = m_TimelinePreview ? TweenAnimationPreview.GetElapsedBinding(sequence) : 0;
        }

        private void DrawItemTimeline(Rect rect, int index)
        {
            var area = TweenAnimationItemDrawer.GetTimelineRect(rect);
            EditorGUI.DrawRect(area, new Color(.45f, .45f, .45f, .25f));
            if (m_Timeline == null)
            {
                GUI.Label(new Rect(area.x, area.y - 2, area.width, 8), new GUIContent(string.Empty, m_TimelineMessage));
                return;
            }
            var order = m_Timeline.Reverse ? m_Timeline.Entries.Count - 1 - index : index;
            if (order < 0 || order >= m_Timeline.Entries.Count) return;
            var entry = m_Timeline.Entries[order];
            var range = Math.Max(m_Timeline.Duration, .001);
            if (entry.ActiveStart > entry.Start)
                DrawSegment(area, range, entry.Start, entry.ActiveStart, new Color(.6f, .6f, .6f));
            var speedBased = GetItem(index)
                .FindPropertyRelative("SpeedBased").boolValue && entry.Type != TweenAnimation.TweenType.Wait;
            DrawSegment(area, range, entry.ActiveStart, entry.End,
                !m_TimelinePreview && speedBased ? new Color(.36f, .36f, .36f) : new Color(.25f, .38f, .48f));
            if (m_TimelinePreview)
            {
                var end = Math.Min(entry.End, m_Timeline.GetPosition(m_Elapsed));
                if (end > entry.ActiveStart)
                    DrawSegment(area, range, entry.ActiveStart, end, new Color(.2f, .7f, 1f));
            }
            if (entry.Loops <= 100 && entry.Duration / range * area.width >= 3)
                for (var loop = 1; loop < entry.Loops; loop++)
                {
                    var x = area.x + (float)((entry.ActiveStart + entry.Duration * loop) / range) * area.width;
                    EditorGUI.DrawRect(new Rect(x, area.y, 1, area.height), Color.gray);
                }
            HandleTimingDrag(rect, area, index, entry, range);
            var tooltip = $"起点 {entry.Start:0.###} 秒 · 结束 {entry.End:0.###} 秒\n单轮 {entry.Duration:0.###} 秒 × {entry.Loops} 次";
            if (!speedBased && !TweenAnimationPreview.IsActive && !Application.isPlaying && GUI.enabled)
                tooltip += "\n拖条身调整起点（Insert），拖右端调整时长。";
            GUI.Label(new Rect(area.x, area.y - 2, area.width, 8), new GUIContent(string.Empty, tooltip));
        }

        private void HandleTimingDrag(Rect itemRect, Rect area, int index, TweenAnimation.TimelineEntry entry, double range)
        {
            if (TweenAnimationPreview.IsActive || Application.isPlaying || serializedObject.isEditingMultipleObjects || !GUI.enabled) return;
            var property = FindProperty("m_Sequence").GetArrayElementAtIndex(index);
            var wait = entry.Type == TweenAnimation.TweenType.Wait;
            if (!wait && property.FindPropertyRelative("SpeedBased").boolValue) return;
            var segment = SegmentRect(area, range, entry.Start, entry.End);
            var hit = new Rect(segment.x, area.y - 2, segment.width, 8);
            var edge = new Rect(Mathf.Max(hit.x, hit.xMax - 6), hit.y, Mathf.Min(6, hit.width), hit.height);
            var id = GUIUtility.GetControlID(FocusType.Passive, itemRect);
            var e = Event.current;
            var dragging = m_DragItem == index && GUIUtility.hotControl == m_DragControl;
            if (dragging)
            {
                var cursorArea = itemRect;
                cursorArea.xMin = Mathf.Min(cursorArea.xMin, e.mousePosition.x - 1);
                cursorArea.xMax = Mathf.Max(cursorArea.xMax, e.mousePosition.x + 1);
                cursorArea.yMin = Mathf.Min(cursorArea.yMin, e.mousePosition.y - 1);
                cursorArea.yMax = Mathf.Max(cursorArea.yMax, e.mousePosition.y + 1);
                EditorGUIUtility.AddCursorRect(cursorArea, GetTimingCursor(true, m_DragDuration, false), m_DragControl);
            }
            else if (GUIUtility.hotControl == 0 && hit.Contains(e.mousePosition))
                // 同一位置只注册一个光标，避免条身与右端两个区域争抢。
                EditorGUIUtility.AddCursorRect(hit, GetTimingCursor(false, false, edge.Contains(e.mousePosition)), id);
            var hover = hit.Contains(e.mousePosition) && (GUIUtility.hotControl == 0 || dragging);
            if (hover || dragging)
            {
                m_TimingHover = true;
                m_TimingFeedbackArea = new Rect(area.x, itemRect.y, area.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.DrawRect(segment, new Color(.25f, .75f, 1f));
                var handleWidth = Mathf.Min(3, edge.width);
                EditorGUI.DrawRect(new Rect(edge.xMax - handleWidth, edge.y, handleWidth, edge.height),
                    (dragging ? m_DragDuration : edge.Contains(e.mousePosition)) ? Color.white : new Color(.65f, .85f, 1f));
            }
            if (e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition) && m_DragItem < 0)
            {
                m_DragItem = index;
                m_DragDuration = edge.Contains(e.mousePosition);
                m_DragMouseX = e.mousePosition.x;
                m_DragStart = (float)entry.Start;
                m_DragItemDuration = entry.Duration;
                m_DragRange = range / Math.Max(1, area.width);
                Undo.IncrementCurrentGroup();
                m_DragUndo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(m_DragDuration ? "调整动画时长" : "调整动画插入时间");
                GUIUtility.hotControl = m_DragControl = id;
                Repaint();
                e.Use();
            }
            else if (m_DragItem == index && GUIUtility.hotControl == m_DragControl)
            {
                if (e.type == EventType.MouseDrag)
                {
                    var delta = (e.mousePosition.x - m_DragMouseX) * m_DragRange;
                    var loops = wait ? 1 : property.FindPropertyRelative("Loops").intValue;
                    if (m_DragDuration)
                        ApplyItemTiming(property, m_DragStart, (float)Math.Max(0, m_DragItemDuration + delta / Math.Max(1, loops)), false);
                    else ApplyItemTiming(property, (float)Math.Max(0, m_DragStart + delta), m_DragItemDuration, true);
                    serializedObject.ApplyModifiedProperties();
                    Repaint();
                    e.Use();
                }
                else if (e.rawType == EventType.MouseUp)
                {
                    EndTimingDrag();
                    e.Use();
                }
            }
        }

        internal static MouseCursor GetTimingCursor(bool dragging, bool duration, bool overEdge) =>
            (dragging ? duration : overEdge) ? MouseCursor.ResizeHorizontal : MouseCursor.MoveArrow;

        internal static string GetTimingFeedback(bool duration, float start, float seconds) => duration
            ? $"时长 {seconds:0.###} 秒 / 单轮"
            : $"Insert · 起点 {start:0.###} 秒";

        private void DrawTimingFeedback()
        {
            if (m_DragItem < 0 || TweenAnimationPreview.IsActive || Application.isPlaying || GUIUtility.hotControl != m_DragControl) return;
            var list = FindProperty("m_Sequence");
            if (m_DragItem >= list.arraySize) { EndTimingDrag(); return; }
            var item = list.GetArrayElementAtIndex(m_DragItem);
            var start = item.FindPropertyRelative("AddType").intValue == (int)TweenAnimation.AddType.Insert
                ? item.FindPropertyRelative("InsertTime").floatValue : m_DragStart;
            var text = GetTimingFeedback(m_DragDuration, start, item.FindPropertyRelative("DurationOrSpeed").floatValue);
            if (!m_DragDuration && item.FindPropertyRelative("AddType").intValue != (int)TweenAnimation.AddType.Insert)
                text = "移动后改为 " + text;
            if (m_TimingFeedbackStyle == null)
            {
                m_TimingFeedbackStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                m_TimingFeedbackStyle.normal.textColor = Color.white;
            }
            var area = m_TimingFeedbackArea;
            area.width = Mathf.Min(area.width, m_TimingFeedbackStyle.CalcSize(new GUIContent(text)).x + 14);
            area.x = Mathf.Clamp(Event.current.mousePosition.x - area.width * .5f,
                m_TimingFeedbackArea.x, m_TimingFeedbackArea.xMax - area.width);
            EditorGUI.DrawRect(area, new Color(.1f, .18f, .24f, .98f));
            GUI.Label(area, text, m_TimingFeedbackStyle);
        }

        private void EndTimingDrag()
        {
            if (m_DragUndo >= 0) Undo.CollapseUndoOperations(m_DragUndo);
            if (m_DragControl != 0 && GUIUtility.hotControl == m_DragControl) GUIUtility.hotControl = 0;
            m_DragItem = m_DragUndo = -1;
            m_DragControl = 0;
            Repaint();
        }

        internal static void ApplyItemTiming(SerializedProperty item, float start, float duration, bool move)
        {
            if (TweenAnimationPreview.IsActive || Application.isPlaying) throw new InvalidOperationException("请先停止预览或运行。");
            var wait = item.FindPropertyRelative("AnimationType").intValue == (int)TweenAnimation.TweenType.Wait;
            if (!wait && item.FindPropertyRelative("SpeedBased").boolValue) throw new InvalidOperationException("速度模式不能拖动时间条。");
            if (float.IsNaN(start) || float.IsInfinity(start) || start < 0 || float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0)
                throw new ArgumentOutOfRangeException(nameof(start));
            if (move)
            {
                item.FindPropertyRelative("AddType").intValue = (int)TweenAnimation.AddType.Insert;
                item.FindPropertyRelative("InsertTime").floatValue = start;
            }
            else item.FindPropertyRelative("DurationOrSpeed").floatValue = duration;
        }

        internal static Rect SegmentRect(Rect area, double range, double start, double end)
        {
            var width = Mathf.Max(0, area.width);
            var left = Mathf.Clamp01((float)(start / Math.Max(range, .001))) * width;
            var right = Mathf.Clamp01((float)(end / Math.Max(range, .001))) * width;
            var minimum = Mathf.Min(2, width);
            left = Mathf.Min(left, width - minimum);
            return new Rect(area.x + left, area.y, Mathf.Min(width - left, Mathf.Max(minimum, right - left)), area.height);
        }

        private static void DrawSegment(Rect area, double range, double start, double end, Color color) =>
            EditorGUI.DrawRect(SegmentRect(area, range, start, end), color);

        private void DrawProgress(Rect rect)
        {
            if (m_Timeline == null)
            {
                DrawProgressFill(rect, 0, "不可用");
                GUI.Label(rect, new GUIContent(string.Empty, m_TimelineMessage));
                return;
            }
            var infinite = double.IsPositiveInfinity(m_Timeline.TotalDuration);
            var total = infinite ? "∞" : $"{m_Timeline.TotalDuration:0.##}s";
            var text = m_TimelinePreview ? $"{m_Elapsed:0.##}s / {total}" : total;
            if (m_Timeline.Reverse) text = "反序 · " + text;
            else if (m_TimelinePreview && Binding.IsBackward) text = "倒播 · " + text;
            var position = m_Timeline.GetPosition(m_Elapsed);
            // 有限序列显示总经过进度；无限序列只显示当前单轮编排进度。
            var scrubDuration = infinite ? m_Timeline.Delay + m_Timeline.Duration : m_Timeline.TotalDuration;
            var progress = !m_TimelinePreview ? 0 : infinite
                ? (m_Elapsed <= scrubDuration ? m_Elapsed / Math.Max(.001, scrubDuration)
                    : (m_Elapsed - m_Timeline.Delay) % Math.Max(.001, m_Timeline.Duration) / Math.Max(.001, m_Timeline.Duration))
                : m_Elapsed / Math.Max(.001, m_Timeline.TotalDuration);
            DrawProgressFill(rect, Mathf.Clamp01((float)progress), text);
            var cursorX = rect.x + Mathf.Clamp01((float)progress) * Mathf.Max(0, rect.width - 3);
            EditorGUI.DrawRect(new Rect(cursorX, rect.y, 3, rect.height), Color.white);
            HandleScrub(rect, scrubDuration);
            var tooltip = $"单轮 {m_Timeline.Duration:0.###}秒；全局延迟 {m_Timeline.Delay:0.###}秒；播放{(infinite ? "无限" : m_Timeline.Loops.ToString())}次\n编排位置 {position:0.###}秒；全局缓动 {m_Timeline.Ease}\n有限序列按实际动画经过时间填充；无限循环显示单轮编排进度，越界时填充限制在0–100%。\n"
                + (m_Timeline.Estimated ? m_TimelinePreview ? "速度时长已按本次播放采样固定。\n" : "速度时长由当前起始值和结束值估算，播放时重新采样。\n" : "")
                + "点击或拖动定位，松开后停留；继续从当前位置推进，重播从头开始。无限循环定位第一轮。停止后恢复原值，不触发业务事件。";
            GUI.Label(rect, new GUIContent(string.Empty, tooltip));
        }

        private void HandleScrub(Rect rect, double duration)
        {
            var id = GUIUtility.GetControlID(FocusType.Passive, rect);
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition) && CanRun())
            {
                GUIUtility.hotControl = id;
                e.Use();
            }
            else if (GUIUtility.hotControl != id || e.type != EventType.MouseDrag && e.type != EventType.MouseUp) return;
            try
            {
                serializedObject.ApplyModifiedProperties();
                var time = Mathf.Clamp01((e.mousePosition.x - rect.x) / Mathf.Max(1, rect.width)) * duration;
                TweenAnimationPreview.SeekBinding(Binding, time);
                RefreshTimeline();
                Repaint();
            }
            catch (Exception exception)
            {
                GUIUtility.hotControl = 0;
                TweenAnimationPreview.Stop();
                Debug.LogException(exception, target);
            }
            if (e.rawType == EventType.MouseUp) GUIUtility.hotControl = 0;
            e.Use();
        }

        private void DrawPlaybackToolbar()
        {
            if (m_Measuring)
            {
                var single = TweenAnimationPreview.IsSingle && TweenAnimationPreview.ContainsBinding(Binding);
                if (single) HelpBox(SinglePreviewMessage, MessageType.Info);
                NextRect();
                if (!single && m_Content.width < 340) NextRect();
                return;
            }
            if (TweenAnimationPreview.IsSingle && TweenAnimationPreview.ContainsBinding(Binding))
            {
                HelpBox(SinglePreviewMessage, MessageType.Info);
                var row = NextRect();
                var buttonWidth = row.width / 4;
                Rect ButtonRect(int index) => new Rect(row.x + index * buttonWidth, row.y, buttonWidth, row.height);
                if (GUI.Button(ButtonRect(0), TweenAnimationPreview.IsPaused ? "继续" : "暂停", EditorStyles.miniButtonLeft))
                {
                    if (TweenAnimationPreview.IsPaused) TweenAnimationPreview.Resume();
                    else TweenAnimationPreview.Pause();
                }
                if (GUI.Button(ButtonRect(1), "重播", EditorStyles.miniButtonMid)) TweenAnimationPreview.Restart();
                if (GUI.Button(ButtonRect(2), "停止", EditorStyles.miniButtonRight)) TweenAnimationPreview.Stop();
                GUI.Label(ButtonRect(3), $"{TweenAnimationPreview.GetElapsedBinding(Binding):0.##}s", EditorStyles.miniLabel);
                return;
            }
            var wide = m_Content.width >= 340;
            var ownsPreview = TweenAnimationPreview.ContainsBinding(Binding);
            var toolbar = NextRect();
            var width = Mathf.Min(38, toolbar.width / 4);
            Rect Button(int index) => new Rect(toolbar.x + index * width, toolbar.y, width, toolbar.height);
            {
                using (new EditorGUI.DisabledScope(!ownsPreview && !CanRun()))
                {
                    var label = !ownsPreview ? "播放" : TweenAnimationPreview.IsPaused ? "继续" : "暂停";
                    if (GUI.Button(Button(0), new GUIContent(label, "播放预览；暂停保持姿态；继续沿当前方向播放。"), EditorStyles.miniButtonLeft))
                    {
                        if (!ownsPreview) Run(false);
                        else if (TweenAnimationPreview.IsPaused) TweenAnimationPreview.Resume();
                        else TweenAnimationPreview.Pause();
                    }
                }
                using (new EditorGUI.DisabledScope(!ownsPreview && !CanRun()))
                    if (GUI.Button(Button(1), new GUIContent("重播", "按当前预览方向从头播放；未预览时正向播放。"), EditorStyles.miniButtonMid))
                    {
                        if (ownsPreview) TweenAnimationPreview.Restart();
                        else Run(false);
                    }
                var reversePreview = ownsPreview && TweenAnimationPreview.GetTimelineBinding(Binding)?.Reverse == true;
                using (new EditorGUI.DisabledScope(!CanRun() || reversePreview))
                {
                    var backward = ownsPreview && Binding.IsBackward;
                    var directionLabel = backward ? "正放" : "倒播";
                    var hint = reversePreview ? "请先停止反序预览。" : "从当前播放进度切换方向；未播放时从时间线末尾倒播。";
                    if (GUI.Button(Button(2), new GUIContent(directionLabel, hint), EditorStyles.miniButtonMid))
                    {
                        if (ownsPreview) TweenAnimationPreview.SetDirection(!backward);
                        else Run(true);
                    }
                }
                using (new EditorGUI.DisabledScope(!TweenAnimationPreview.IsActive))
                    if (GUI.Button(Button(3), new GUIContent("停止", "停止预览并恢复预览前状态。"), EditorStyles.miniButtonRight)) TweenAnimationPreview.Stop();
                if (wide) DrawProgress(new Rect(toolbar.x + width * 4 + 4, toolbar.y, toolbar.width - width * 4 - 4, toolbar.height));
            }
            if (!wide) DrawProgress(NextRect());
        }

        private GUIStyle m_ProgressLabel;

        private void DrawProgressFill(Rect rect, float progress, string text)
        {
            EditorGUI.DrawRect(rect, new Color(.18f, .18f, .18f));
            var inside = new Rect(rect.x + 1, rect.y + 1, Mathf.Max(0, rect.width - 2), Mathf.Max(0, rect.height - 2));
            EditorGUI.DrawRect(inside, new Color(.34f, .34f, .34f));
            EditorGUI.DrawRect(new Rect(inside.x, inside.y, inside.width * progress, inside.height), new Color(.24f, .48f, .66f));
            if (m_ProgressLabel == null)
            {
                m_ProgressLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip };
                m_ProgressLabel.normal.textColor = Color.white;
            }
            GUI.Label(new Rect(inside.x + 3, inside.y, Mathf.Max(0, inside.width - 6), inside.height), text, m_ProgressLabel);
        }

        private bool CanRun()
        {
            if (TweenAnimationPreview.IsEndpoint) return false;
            if (m_ValidationErrors.Count != 0) return false;
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (AnimationMode.InAnimationMode() && !TweenAnimationPreview.IsActive) return false;
            foreach (var item in targets)
            {
                if (!new TweenAnimationBinding(item, m_Path).IsAvailable) return false;
            }
            return true;
        }

        private void Run(bool backward, bool reverseOrder = false)
        {
            serializedObject.ApplyModifiedProperties();
            var sequences = Array.ConvertAll(targets, item => new TweenAnimationBinding(item, m_Path));
            try
            {
                if (reverseOrder) TweenAnimationPreview.StartReverseBindings(sequences);
                else TweenAnimationPreview.StartBindings(sequences, backward);
            }
            catch (Exception exception)
            {
                TweenAnimationPreview.Stop();
                Debug.LogException(exception, target);
            }
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private void DrawSequenceSettings()
        {
            m_ShowSettings = Foldout(m_ShowSettings, "序列设置");
            if (m_ShowSettings)
            {
                DrawProperty("m_TriggerMode", new GUIContent("重复触发策略", "仅影响 Trigger 入口：Restart 从头重播；IgnoreWhilePlaying 播放中忽略；ContinueOrReverse 首次正向，再次沿原时间线切换方向。Play/Forward/Backward 不受此策略影响。"));
                var delayRect = NextRect();
                if (!m_Measuring) TweenAnimationItemDrawer.FloatField(delayRect, FindProperty("m_Delay"), new GUIContent("全局延迟（秒）"));
                var loops = FindProperty("m_Loops");
                var loopsRect = NextRect();
                if (!m_Measuring) TweenAnimationItemDrawer.IntField(loopsRect, loops,
                    new GUIContent("播放次数", "-1：无限循环；正整数：播放次数。无限循环使用停止或 DOKill 结束。"));
                if (m_Measuring) DrawProperty("m_LoopType");
                else using (new EditorGUI.DisabledScope(!loops.hasMultipleDifferentValues && loops.intValue == 1))
                    DrawProperty("m_LoopType", new GUIContent("循环方式"));
            }
            var ease = FindProperty("m_Ease");
            var easeSummary = m_Measuring ? "" : ease.hasMultipleDifferentValues ? "多个缓动" : ((LitMotion.Ease)ease.intValue).ToString();
            m_ShowAdvanced = Foldout(m_ShowAdvanced, $"高级设置 · 全局缓动 {easeSummary}");
            if (m_ShowAdvanced)
            {
                var easeRect = NextRect();
                if (!m_Measuring) TweenAnimationItemDrawer.DrawGlobalEase(easeRect, ease);
                DrawProperty("m_UpdateType", new GUIContent("更新阶段", "统一控制整个序列；单项更新设置不参与序列调度。"));
                DrawProperty("m_IgnoreTimeScale", new GUIContent("忽略时间缩放", "统一控制序列是否忽略 Time.timeScale；Fixed 阶段仍使用固定时间步。"));
            }
        }

        internal bool HasComponentLifecycle => m_Path == "m_Animation" && target is TweenAnimationComponent;

        private void DrawLifecycle()
        {
            m_ShowLifecycle = Foldout(m_ShowLifecycle, "生命周期");
            if (!m_ShowLifecycle) return;
            DrawProperty("m_PlayOnAwake", new GUIContent("Awake 自动播放", "只在 Awake 播放，不是每次启用或显示时播放。"));
            var play = FindProperty("m_PlayOnAwake");
            if (m_Measuring) DrawProperty("m_ResetOnAwake");
            else using (new EditorGUI.DisabledScope(!play.hasMultipleDifferentValues && play.boolValue))
                DrawProperty("m_ResetOnAwake", new GUIContent("Awake 应用起始值"));
            if (!HasComponentLifecycle) return;
            DrawLifecycleAction("m_OnEnable", new GUIContent("启用时", "每次 OnEnable 时执行；禁用会取消旧动画，不保留续播句柄。"));
            DrawLifecycleAction("m_OnDisable", new GUIContent("禁用时", "每次 OnDisable 时停止动画；可选应用配置起始值，不是播放退出动画。"));
        }

        private void DrawLifecycleAction(string name, GUIContent label)
        {
            var property = serializedObject.FindProperty(name);
            var rect = NextRect(EditorGUI.GetPropertyHeight(property, label, true));
            if (!m_Measuring) EditorGUI.PropertyField(rect, property, label, true);
        }

        private void DrawStagger()
        {
            m_ShowStagger = Foldout(m_ShowStagger, "批量错峰");
            if (!m_ShowStagger) return;
            HelpBox(StaggerMessage, MessageType.Info);
            if (m_Measuring) { for (var i = 0; i < 4; i++) NextRect(); return; }
            m_StaggerFirst = EditorGUI.IntField(NextRect(), "起始项（含）", m_StaggerFirst);
            m_StaggerLast = EditorGUI.IntField(NextRect(), "结束项（含）", m_StaggerLast);
            m_StaggerInterval = EditorGUI.FloatField(NextRect(), "间隔（秒）", m_StaggerInterval);
            using (new EditorGUI.DisabledScope(serializedObject.isEditingMultipleObjects || m_Timeline == null
                || m_StaggerFirst < 1 || m_StaggerLast < m_StaggerFirst
                || m_StaggerLast > FindProperty("m_Sequence").arraySize
                || float.IsNaN(m_StaggerInterval) || float.IsInfinity(m_StaggerInterval) || m_StaggerInterval < 0))
                if (GUI.Button(NextRect(), $"应用到第 {m_StaggerFirst}–{m_StaggerLast} 项"))
                {
                    serializedObject.ApplyModifiedProperties();
                    try { ApplyStaggerBinding(Binding, m_StaggerFirst - 1, m_StaggerLast - 1, m_StaggerInterval); }
                    catch (Exception exception) { Debug.LogException(exception, target); }
                    serializedObject.Update();
                }
        }

        internal static void ApplyStaggerBinding(TweenAnimationBinding sequence, int first, int last, float interval)
        {
            if (TweenAnimationPreview.IsActive || Application.isPlaying) throw new InvalidOperationException("请先停止预览或运行。");
            var timeline = sequence.Animation.CaptureTimeline();
            if (first < 0 || last < first || last >= timeline.Entries.Count || float.IsNaN(interval) || float.IsInfinity(interval) || interval < 0)
                throw new ArgumentOutOfRangeException(nameof(interval));
            var start = timeline.Entries[first].Start;
            if (start + (last - first) * (double)interval > float.MaxValue) throw new ArgumentOutOfRangeException(nameof(interval));
            using (var data = new SerializedObject(sequence.Owner))
            {
                var list = data.FindProperty(sequence.ListPath);
                for (var i = first; i <= last; i++)
                {
                    var item = list.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("AddType").intValue = (int)TweenAnimation.AddType.Insert;
                    item.FindPropertyRelative("InsertTime").floatValue = (float)(start + (i - first) * (double)interval);
                }
                data.ApplyModifiedProperties();
            }
        }

        private void DrawSequenceEvents()
        {
            m_ShowEvents = Foldout(m_ShowEvents, m_Measuring ? "" : "序列事件（仅运行时）"
                + TweenAnimationItemDrawer.EventSummary(FindProperty("m_Sequence"), true));
            if (m_ShowEvents)
                foreach (var name in new[] { "OnPlay", "OnUpdate", "OnComplete" })
                    DrawProperty("m_" + name, new GUIContent(TweenAnimationItemDrawer.EventLabel(name)));
        }

        private void DrawProperty(string name, GUIContent label = null)
        {
            var property = FindProperty(name);
            var rect = NextRect(EditorGUI.GetPropertyHeight(property, label, true));
            if (!m_Measuring) EditorGUI.PropertyField(rect, property, label, true);
        }

        private void DrawReverse()
        {
            var rect = NextRect();
            if (!m_Measuring) m_ShowReverse = EditorGUI.Toggle(rect, new GUIContent("反序", "显示反序播放按钮。"), m_ShowReverse);
            if (!m_ShowReverse) return;
            if (m_Measuring) { NextRect(); return; }
            using (new EditorGUI.DisabledScope(!CanRun()))
                if (GUI.Button(NextRect(), new GUIContent("反序播放", "反转列表并交换起止值，重新播放。"))) Run(false, true);
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(TweenAnimationComponent))]
    internal sealed class TweenAnimationComponentEditor : UnityEditor.Editor
    {
        private TweenAnimationInspector m_Inspector;
        private void OnEnable() => m_Inspector = new TweenAnimationInspector(serializedObject, "m_Animation", Repaint);
        private void OnDisable() => m_Inspector?.Dispose();
        public override void OnInspectorGUI() => m_Inspector.OnInspectorGUI();
    }

    [InitializeOnLoad]
    internal static class TweenAnimationPreview
    {
        private static readonly List<TweenAnimationBinding> Sequences = new List<TweenAnimationBinding>();
        private static readonly List<MotionHandle> Motions = new List<MotionHandle>();
        private static readonly List<TweenAnimation.TimelineSnapshot> Timelines = new List<TweenAnimation.TimelineSnapshot>();
        private static readonly Dictionary<Component, List<PropertyModification>> Baselines = new Dictionary<Component, List<PropertyModification>>();
        private static bool m_OwnsAnimationMode;
        private static bool m_IsPaused;
        private static TweenAnimation.SequenceAnimation m_SingleAnimation;
        private static string m_SinglePath;
        internal static bool IsSingle => IsActive && m_SingleAnimation != null;
        private static TweenAnimation.SequenceAnimation m_Endpoint;
        private static string m_EndpointPath;
        private static bool m_EndpointTo, m_EndpointEdit, m_PreviousToolsHidden;
        private static Vector3 m_BaseLocal, m_BaseAnchored;
        private static Vector4 m_FromEndpoint, m_ToEndpoint;
        private static int m_EndpointUndoGroup = -1;
        internal static bool IsEndpoint => IsActive && m_Endpoint != null;
        internal static string EndpointDescription => $"{(m_EndpointEdit ? "编辑" : "查看")}{(m_EndpointTo ? "终点" : "起点")} · {m_Endpoint?.Target?.name}。"
            + (m_EndpointEdit ? "拖动Scene手柄修改配置；结束后目标恢复原位置。" : "临时显示所选动画项的端点，不代表整个序列姿态。");
        internal static bool IsActive => m_OwnsAnimationMode;
        internal static bool IsPaused => IsActive && m_IsPaused;
        internal static bool ContainsBinding(TweenAnimationBinding binding) => Sequences.Exists(item => item.Same(binding));
        internal static TweenAnimation.TimelineSnapshot GetTimelineBinding(TweenAnimationBinding sequence)
        {
            var index = Sequences.FindIndex(item => item.Same(sequence));
            return index >= 0 && index < Timelines.Count ? Timelines[index] : null;
        }

        internal static double GetElapsedBinding(TweenAnimationBinding sequence)
        {
            var index = Sequences.FindIndex(item => item.Same(sequence));
            return index >= 0 && index < Motions.Count && Motions[index].IsActive() ? Motions[index].Time : 0;
        }

        internal static void SeekBinding(TweenAnimationBinding sequence, double time)
        {
            if (IsEndpoint) throw new InvalidOperationException("请先结束端点查看或编辑。");
            if (IsSingle) throw new InvalidOperationException("单项预览不能定位整序列时间。");
            if (double.IsNaN(time) || double.IsInfinity(time)) throw new ArgumentOutOfRangeException(nameof(time));
            if (!ContainsBinding(sequence)) StartBindings(new[] { sequence }, false);
            Pause();
            var index = Sequences.FindIndex(item => item.Same(sequence));
            var motion = Motions[index];
            motion.Preserve();
            motion.PlaybackSpeed = 0;
            // 延迟期间后端会跳过赋值，定位前先恢复采样基线，避免从末尾拖回时残留。
            foreach (var pair in Baselines)
            {
                if (pair.Key == null) continue;
                using (var data = new SerializedObject(pair.Key))
                {
                    foreach (var modification in pair.Value)
                    {
                        var property = data.FindProperty(modification.propertyPath);
                        if (property == null) continue;
                        if (property.propertyType == SerializedPropertyType.Float)
                            property.floatValue = float.Parse(modification.value, CultureInfo.InvariantCulture);
                        else property.intValue = int.Parse(modification.value, CultureInfo.InvariantCulture);
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            motion.Time = Math.Max(0, Math.Min(time, motion.TotalDuration));
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        internal static void Pause()
        {
            if (!IsActive) return;
            foreach (var motion in Motions)
                if (motion.IsActive()) motion.PlaybackSpeed = 0;
            m_IsPaused = true;
        }

        internal static void Resume()
        {
            if (IsEndpoint) return;
            if (!IsPaused) return;
            m_IsPaused = false;
            for (var i = 0; i < Motions.Count; i++)
            {
                var motion = Motions[i];
                if (motion.IsActive()) motion.PlaybackSpeed = Sequences[i].IsBackward ? -1 : 1;
            }
            Update();
        }

        internal static void SetDirection(bool backward)
        {
            if (IsEndpoint) return;
            if (IsSingle) throw new InvalidOperationException("请先停止单项预览，再切换整序列方向。");
            if (!IsActive) return;
            if (Timelines.Exists(timeline => timeline.Reverse)) throw new InvalidOperationException("请先停止反序预览，再切换方向。");
            foreach (var sequence in Sequences)
            {
                sequence.SetDirection(backward);
            }
            m_IsPaused = false;
            Update();
        }

        internal static void Restart()
        {
            if (IsEndpoint) return;
            if (!IsActive) return;
            if (IsSingle) { StartSingleBinding(Sequences[0], m_SinglePath); return; }
            var sequences = Sequences.ToArray();
            var reverse = Timelines.Count != 0 && (Timelines[0].Reverse || Sequences[0].IsBackward);
            if (Timelines.Exists(timeline => timeline.Reverse)) StartReverseBindings(sequences);
            else StartBindings(sequences, reverse);
        }

        static TweenAnimationPreview()
        {
            EditorApplication.update += Update;
            SceneView.duringSceneGui += _ => DrawEndpointHandles();
            Selection.selectionChanged += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.playModeStateChanged += _ => Stop();
            Undo.undoRedoPerformed += Stop;
            EditorSceneManager.sceneClosing += (_, __) => Stop();
        }

        internal static void StartBindings(TweenAnimationBinding[] sequences, bool backward) => StartCore(sequences, backward, false);
        internal static void StartReverseBindings(TweenAnimationBinding[] sequences) => StartCore(sequences, false, true);
        internal static void StartSingleBinding(TweenAnimationBinding sequence, string path)
        {
            Stop();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || AnimationMode.InAnimationMode())
                throw new InvalidOperationException("请先停止其他动画预览并等待编译完成。");
            if (sequence == null || !sequence.IsAvailable)
                throw new InvalidOperationException("请在场景或Prefab Mode中选择启用的序列。");
            TweenAnimation.SequenceAnimation item;
            using (var data = new SerializedObject(sequence.Owner))
            {
                var property = data.FindProperty(path);
                if (property == null) throw new InvalidOperationException("动画项不存在。");
                item = TweenAnimationItemDrawer.ReadAnimation(property);
            }
            var error = item.GetValidationError();
            if (error != null) throw new InvalidOperationException(error);
            if (EditorUtility.IsPersistent(item.Target) || !item.Target.gameObject.activeInHierarchy)
                throw new InvalidOperationException("单项预览目标必须是启用的场景对象。");
            try
            {
                AnimationMode.StartAnimationMode();
                m_OwnsAnimationMode = true;
                Sequences.Add(sequence);
                m_SingleAnimation = item;
                m_SinglePath = path;
                RecordAnimation(item, new HashSet<UnityEngine.Object>());
                var motion = sequence.Animation.CreateItemPreview(item);
                Motions.Add(motion);
                motion.Preserve();
                motion.Time = 0;
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
            catch { Stop(); throw; }
        }

        internal static bool SupportsPosition(TweenAnimation.TweenType type) => type switch
        {
            TweenAnimation.TweenType.Move or TweenAnimation.TweenType.MoveX or TweenAnimation.TweenType.MoveY
            or TweenAnimation.TweenType.MoveZ or TweenAnimation.TweenType.LocalMove or TweenAnimation.TweenType.LocalMoveX
            or TweenAnimation.TweenType.LocalMoveY or TweenAnimation.TweenType.LocalMoveZ or TweenAnimation.TweenType.AnchorPosition
            or TweenAnimation.TweenType.AnchorPositionX or TweenAnimation.TweenType.AnchorPositionY
            or TweenAnimation.TweenType.AnchorPositionZ or TweenAnimation.TweenType.AnchorPosition3D => true,
            _ => false
        };

        private static bool IsWorldPosition(TweenAnimation.TweenType type) => type is TweenAnimation.TweenType.Move
            or TweenAnimation.TweenType.MoveX or TweenAnimation.TweenType.MoveY or TweenAnimation.TweenType.MoveZ;

        private static bool IsAnchorPosition(TweenAnimation.TweenType type) => type is TweenAnimation.TweenType.AnchorPosition
            or TweenAnimation.TweenType.AnchorPositionX or TweenAnimation.TweenType.AnchorPositionY
            or TweenAnimation.TweenType.AnchorPositionZ or TweenAnimation.TweenType.AnchorPosition3D;

        private static int PositionAxis(TweenAnimation.TweenType type) => type switch
        {
            TweenAnimation.TweenType.MoveX or TweenAnimation.TweenType.LocalMoveX or TweenAnimation.TweenType.AnchorPositionX => 0,
            TweenAnimation.TweenType.MoveY or TweenAnimation.TweenType.LocalMoveY or TweenAnimation.TweenType.AnchorPositionY => 1,
            TweenAnimation.TweenType.MoveZ or TweenAnimation.TweenType.LocalMoveZ or TweenAnimation.TweenType.AnchorPositionZ => 2,
            _ => -1
        };

        internal static void StartEndpointBinding(TweenAnimationBinding sequence, string path, bool to, bool edit)
        {
            Stop();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || AnimationMode.InAnimationMode())
                throw new InvalidOperationException("请先停止其他动画预览并等待编译完成。");
            if (sequence == null || !sequence.IsAvailable)
                throw new InvalidOperationException("请在场景或Prefab Mode中选择启用的序列。");
            TweenAnimation.SequenceAnimation item;
            using (var data = new SerializedObject(sequence.Owner))
            {
                var property = data.FindProperty(path);
                if (property == null) throw new InvalidOperationException("动画项不存在。");
                item = TweenAnimationItemDrawer.ReadAnimation(property);
                if (edit && TweenAnimationItemDrawer.GetEndpointMode(property, to) != 1)
                    throw new InvalidOperationException("只有指定数值端点支持手柄编辑。");
            }
            var error = item.GetValidationError();
            if (error != null) throw new InvalidOperationException(error);
            if (!SupportsPosition(item.AnimationType) || EditorUtility.IsPersistent(item.Target)
                || !item.Target.gameObject.activeInHierarchy)
                throw new InvalidOperationException("请选择场景中的位置动画。");
            var from = item.CaptureEndpointValue(false);
            var end = item.CaptureEndpointValue(true);
            try
            {
                AnimationMode.StartAnimationMode();
                m_OwnsAnimationMode = true;
                Sequences.Add(sequence);
                m_PreviousToolsHidden = Tools.hidden;
                m_Endpoint = item;
                m_EndpointPath = path;
                m_EndpointTo = to;
                m_EndpointEdit = edit;
                m_FromEndpoint = from;
                m_ToEndpoint = end;
                m_BaseLocal = item.Target.transform.localPosition;
                m_BaseAnchored = item.Target is RectTransform rect ? rect.anchoredPosition3D : Vector3.zero;
                Record(item.Target.transform, new HashSet<UnityEngine.Object>());
                Tools.hidden = true;
                item.ApplyValue(to ? end : from);
                m_IsPaused = true;
                SceneView.RepaintAll();
            }
            catch { Stop(); throw; }
        }

        internal static Vector3 EndpointWorldPosition(Vector4 value)
        {
            var item = m_Endpoint;
            var type = item.AnimationType;
            var transform = item.Target.transform;
            var local = IsWorldPosition(type) ? transform.position : IsAnchorPosition(type) ? m_BaseAnchored : m_BaseLocal;
            var axis = PositionAxis(type);
            if (axis >= 0) local[axis] = value.x;
            else if (type == TweenAnimation.TweenType.AnchorPosition) { local.x = value.x; local.y = value.y; }
            else local = (Vector3)value;
            if (IsWorldPosition(type)) return local;
            if (IsAnchorPosition(type)) local = m_BaseLocal + local - m_BaseAnchored;
            return transform.parent == null ? local : transform.parent.TransformPoint(local);
        }

        internal static void MoveEndpoint(Vector3 world)
        {
            if (!IsEndpoint || !m_EndpointEdit) throw new InvalidOperationException("当前未编辑数值端点。");
            if (float.IsNaN(world.sqrMagnitude) || float.IsInfinity(world.sqrMagnitude))
                throw new ArgumentOutOfRangeException(nameof(world));
            var type = m_Endpoint.AnimationType;
            var parent = m_Endpoint.Target.transform.parent;
            var position = IsWorldPosition(type) || parent == null ? world : parent.InverseTransformPoint(world);
            if (IsAnchorPosition(type)) position = m_BaseAnchored + position - m_BaseLocal;
            using (var data = new SerializedObject(Sequences[0].Owner))
            {
                var property = data.FindProperty(m_EndpointPath);
                if (property == null) { Stop(); return; }
                var endpoint = property.FindPropertyRelative(m_EndpointTo ? "ToValue" : "FromValue");
                var value = endpoint.vector4Value;
                var axis = PositionAxis(type);
                if (axis >= 0) value.x = position[axis];
                else
                {
                    value.x = position.x;
                    value.y = position.y;
                    if (type != TweenAnimation.TweenType.AnchorPosition) value.z = position.z;
                }
                if (m_Endpoint.Snapping)
                {
                    value.x = Mathf.Round(value.x);
                    if (axis < 0)
                    {
                        value.y = Mathf.Round(value.y);
                        if (type != TweenAnimation.TweenType.AnchorPosition) value.z = Mathf.Round(value.z);
                    }
                }
                var stored = value;
                if (m_EndpointTo && m_Endpoint.UsesRelativeEnd)
                {
                    stored.x -= m_FromEndpoint.x;
                    if (axis < 0)
                    {
                        stored.y -= m_FromEndpoint.y;
                        if (type != TweenAnimation.TweenType.AnchorPosition) stored.z -= m_FromEndpoint.z;
                    }
                }
                if (stored == endpoint.vector4Value) return;
                if (m_EndpointUndoGroup < 0)
                {
                    Undo.IncrementCurrentGroup();
                    m_EndpointUndoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName(m_EndpointTo ? "编辑动画终点" : "编辑动画起点");
                }
                endpoint.vector4Value = stored;
                data.ApplyModifiedProperties();
                if (m_EndpointTo) m_ToEndpoint = value;
                else
                {
                    m_FromEndpoint = value;
                    m_Endpoint.FromValue = value;
                    if (m_Endpoint.UsesRelativeEnd) m_ToEndpoint = m_Endpoint.CaptureEndpointValue(true);
                }
                m_Endpoint.ApplyValue(value);
            }
            SceneView.RepaintAll();
        }

        internal static void DrawEndpointHandles()
        {
            if (!IsEndpoint || m_Endpoint.Target == null) return;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Stop();
                Event.current.Use();
                return;
            }
            var previousColor = Handles.color;
            try
            {
                var from = EndpointWorldPosition(m_FromEndpoint);
                var to = EndpointWorldPosition(m_ToEndpoint);
                Handles.color = new Color(.3f, .8f, 1f);
                Handles.DrawDottedLine(from, to, 4f);
                Handles.Label(from, "起点");
                Handles.Label(to, "终点");
                var position = m_EndpointTo ? to : from;
                Handles.SphereHandleCap(0, position, Quaternion.identity, HandleUtility.GetHandleSize(position) * .08f, EventType.Repaint);
                if (!m_EndpointEdit) return;
                var type = m_Endpoint.AnimationType;
                var parent = m_Endpoint.Target.transform.parent;
                var rotation = IsWorldPosition(type) || parent == null ? Quaternion.identity : parent.rotation;
                EditorGUI.BeginChangeCheck();
                var axis = PositionAxis(type);
                if (axis >= 0)
                {
                    var direction = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
                    if (!IsWorldPosition(type) && parent != null) direction = parent.TransformVector(direction).normalized;
                    position = Handles.Slider(position, direction);
                }
                else position = Handles.PositionHandle(position, rotation);
                if (EditorGUI.EndChangeCheck()) MoveEndpoint(position);
                if (Event.current.rawType == EventType.MouseUp && m_EndpointUndoGroup >= 0)
                {
                    Undo.CollapseUndoOperations(m_EndpointUndoGroup);
                    m_EndpointUndoGroup = -1;
                }
            }
            catch (Exception exception) { Stop(); Debug.LogException(exception); }
            finally { Handles.color = previousColor; }
        }

        private static void StartCore(TweenAnimationBinding[] sequences, bool backward, bool reverseOrder)
        {
            Stop();
            if (Application.isPlaying || AnimationMode.InAnimationMode())
                throw new InvalidOperationException("TweenAnimationComponent：请先退出其他动画工具的预览，再开始编辑预览。");
            foreach (var sequence in sequences)
            {
                var error = sequence.Animation.GetValidationError();
                if (error != null) throw new InvalidOperationException(error);
                if (!sequence.IsAvailable)
                    throw new InvalidOperationException("TweenAnimationComponent：请在场景或 Prefab Mode 中预览启用的组件，不可直接修改 Prefab 资产。");
            }
            try
            {
                AnimationMode.StartAnimationMode();
                m_OwnsAnimationMode = true;
                Sequences.AddRange(sequences);
                foreach (var sequence in sequences) Timelines.Add(sequence.Animation.CaptureTimeline(reverseOrder));
                var recorded = new HashSet<UnityEngine.Object>();
                foreach (var sequence in sequences)
                {
                    var data = new SerializedObject(sequence.Owner).FindProperty(sequence.ListPath);
                    for (var i = 0; i < data.arraySize; i++)
                    {
                        var animation = TweenAnimationItemDrawer.ReadAnimation(data.GetArrayElementAtIndex(i));
                        if (animation.AnimationType == TweenAnimation.TweenType.Wait) continue;
                        if (EditorUtility.IsPersistent(animation.Target))
                            throw new InvalidOperationException($"TweenAnimationComponent '{sequence.Name}' 第 {i + 1} 项：Target 不可指向资产，请引用场景或 Prefab Mode 实例。");
                        RecordAnimation(animation, recorded);
                    }
                }
                foreach (var sequence in sequences)
                    Motions.Add(sequence.Play(reverseOrder, backward));
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
            catch
            {
                Stop();
                throw;
            }
        }

        private static void RecordAnimation(TweenAnimation.SequenceAnimation animation, HashSet<UnityEngine.Object> recorded)
        {
            Record(animation.Target, recorded);
            Record(animation.Target.transform, recorded);
            // 布局和 Slider 会联动修改其他节点，整序列与单项共用恢复范围。
            if (animation.Target is LayoutElement || animation.Target is Slider)
            {
                var root = animation.Target is LayoutElement && animation.Target.transform.parent != null
                    ? animation.Target.transform.parent : animation.Target.transform;
                foreach (var child in root.GetComponentsInChildren<RectTransform>(true)) Record(child, recorded);
            }
            if (animation.Target is Slider slider && slider.fillRect != null)
            {
                var fillImage = slider.fillRect.GetComponent<Image>();
                if (fillImage != null) Record(fillImage, recorded);
            }
        }

        private static void Record(Component target, HashSet<UnityEngine.Object> recorded)
        {
            if (!recorded.Add(target)) return;
            var baseline = new List<PropertyModification>();
            Baselines.Add(target, baseline);
            var serialized = new SerializedObject(target);
            var iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (!IsAnimatedProperty(iterator.propertyPath)) continue;
                string value;
                if (iterator.propertyType == SerializedPropertyType.Float)
                    value = iterator.floatValue.ToString("R", CultureInfo.InvariantCulture);
                else if (iterator.propertyType == SerializedPropertyType.Integer)
                    value = iterator.intValue.ToString(CultureInfo.InvariantCulture);
                else continue;
                var modification = new PropertyModification { target = target, propertyPath = iterator.propertyPath, value = value };
                baseline.Add(modification);
                AnimationMode.AddPropertyModification(
                    EditorCurveBinding.FloatCurve(string.Empty, target.GetType(), iterator.propertyPath),
                    modification, true);
            }
        }

        private static bool IsAnimatedProperty(string path)
        {
            foreach (var prefix in new[] { "m_LocalPosition", "m_LocalRotation", "m_LocalEulerAnglesHint", "m_LocalScale", "m_AnchoredPosition", "m_SizeDelta", "m_AnchorMin", "m_AnchorMax", "m_Pivot", "m_Color" })
                if (path.StartsWith(prefix + ".", StringComparison.Ordinal)) return true;
            return path == "m_Alpha" || path == "m_FillAmount" || path == "m_Value"
                || path == "m_FlexibleWidth" || path == "m_FlexibleHeight" || path == "m_MinWidth"
                || path == "m_MinHeight" || path == "m_PreferredWidth" || path == "m_PreferredHeight";
        }

        private static void Update()
        {
            try { UpdatePreview(); }
            finally { TweenInspectorRepaint.TickPreview(); }
        }

        private static void UpdatePreview()
        {
            if (!IsActive) return;
            foreach (var sequence in Sequences)
                if (sequence == null || !sequence.IsAvailable) { Stop(); return; }
            if (!AnimationMode.InAnimationMode()) { Stop(); return; }
            if (IsEndpoint)
            {
                if (m_Endpoint.Target == null || EditorUtility.IsPersistent(m_Endpoint.Target)
                    || !m_Endpoint.Target.gameObject.activeInHierarchy) { Stop(); return; }
                SceneView.RepaintAll();
                return;
            }
            if (IsSingle)
            {
                if (m_SingleAnimation.Target == null || !m_SingleAnimation.Target.gameObject.activeInHierarchy
                    || Motions.Count != 1 || !Motions[0].IsActive()) { Stop(); return; }
                var motion = Motions[0];
                if (motion.Time >= motion.TotalDuration) { motion.Time = motion.TotalDuration; motion.PlaybackSpeed = 0; m_IsPaused = true; }
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
                return;
            }
            foreach (var sequence in Sequences) sequence.Animation.MaintainReversibleEndpoint();
            var hasReversible = !Timelines.Exists(timeline => timeline.Reverse);
            if (hasReversible && Motions.TrueForAll(motion => !motion.IsActive() || motion.PlaybackSpeed == 0)) m_IsPaused = true;
            if (Motions.TrueForAll(motion => !motion.IsActive())
                || !hasReversible && !m_IsPaused && Motions.TrueForAll(motion => !motion.IsActive()
                    || motion.Loops >= 0 && motion.Time >= motion.TotalDuration)) { Stop(); return; }
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        internal static void Stop()
        {
            if (!m_OwnsAnimationMode) return;
            try
            {
                foreach (var sequence in Sequences)
                    if (sequence != null) sequence.Kill();
                foreach (var motion in Motions)
                    if (motion.IsActive()) motion.Cancel();
            }
            finally
            {
                Sequences.Clear();
                Motions.Clear();
                Timelines.Clear();
                Baselines.Clear();
                m_OwnsAnimationMode = false;
                m_IsPaused = false;
                m_SingleAnimation = null;
                m_SinglePath = null;
                if (m_Endpoint != null) Tools.hidden = m_PreviousToolsHidden;
                m_Endpoint = null;
                m_EndpointPath = null;
                if (m_EndpointUndoGroup >= 0) Undo.CollapseUndoOperations(m_EndpointUndoGroup);
                m_EndpointUndoGroup = -1;
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                SceneView.RepaintAll();
            }
        }
    }

    internal sealed class TweenAnimationPreviewSaveProcessor : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            TweenAnimationPreview.Stop();
            return paths;
        }
    }
}
