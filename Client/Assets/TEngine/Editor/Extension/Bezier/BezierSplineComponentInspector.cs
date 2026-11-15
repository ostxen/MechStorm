using UnityEditor;
using UnityEngine;

namespace TEngine.Editor
{
    [CustomEditor(typeof(BezierSplineComponent))]
    internal sealed class BezierSplineComponentInspector : UnityEditor.Editor
    {
        private SerializedProperty m_Points;
        private SerializedProperty m_Closed;
        private SerializedProperty m_ArcLengthSamples;
        private int m_SelectedIndex = -1;
        private int m_SelectedTangent;
        private bool m_ShowDirectionArrows = true;
        private bool m_InsertMode;
        private static readonly Rect ToolbarRect = new Rect(12f, 0f, 340f, 210f);

        private void OnEnable()
        {
            RefreshSerializedProperties();
        }

        private void RefreshSerializedProperties()
        {
            if (!HasValidTargets())
            {
                m_Points = null;
                m_Closed = null;
                m_ArcLengthSamples = null;
                return;
            }

            m_Points = serializedObject.FindProperty("_spline._points");
            m_Closed = serializedObject.FindProperty("_spline._closed");
            m_ArcLengthSamples = serializedObject.FindProperty("_spline._arcLengthSamplesPerSegment");
        }

        public override void OnInspectorGUI()
        {
            if (!HasValidTargets())
            {
                return;
            }

            if (m_Points == null || m_Closed == null || m_ArcLengthSamples == null)
            {
                serializedObject.Update();
                RefreshSerializedProperties();
            }

            serializedObject.Update();
            EditorGUILayout.HelpBox("可在 Scene 中点击曲线或锚点进行编辑。选中锚点后可拖动位置，选中控制柄后可拖动切线。", MessageType.Info);
            if (m_Closed != null)
            {
                EditorGUILayout.PropertyField(m_Closed);
            }
            if (m_ArcLengthSamples != null)
            {
                EditorGUILayout.PropertyField(m_ArcLengthSamples, new GUIContent("匀速采样精度"));
            }
            if (m_Points != null)
            {
                EditorGUILayout.PropertyField(m_Points, true);
            }
            serializedObject.ApplyModifiedProperties();
        }

        private bool HasValidTargets()
        {
            if (target == null || targets == null || targets.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawUnselectedPathGizmo(BezierSplineComponent path, GizmoType gizmoType)
        {
            if (path == null || path.SegmentCount == 0)
            {
                return;
            }

            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.85f);
            var sampleCount = Mathf.Max(16, path.SegmentCount * 16);
            var previous = path.EvaluateWorldPosition(0f);
            for (var i = 1; i <= sampleCount; i++)
            {
                var current = path.EvaluateWorldPosition(i / (float)sampleCount);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }

            for (var i = 0; i < path.Points.Count; i++)
            {
                Gizmos.DrawWireSphere(
                    path.transform.TransformPoint(path.Points[i].Position),
                    HandleUtility.GetHandleSize(path.transform.TransformPoint(path.Points[i].Position)) * 0.09f);
            }
        }

        private void OnSceneGUI()
        {
            if (!HasValidTargets())
            {
                return;
            }

            var path = (BezierSplineComponent)target;
            var points = path.Points;
            if (points == null || points.Count == 0)
            {
                return;
            }

            DrawCurve(path);
            DrawPathMarkers(path, m_ShowDirectionArrows);
            HandleSceneCommands(path);
            DrawSceneToolbar(path);

            if (m_InsertMode)
            {
                HandleInsertion(path);
            }
            else for (var i = 0; i < points.Count; i++)
            {
                DrawPoint(path, i);
            }
        }

        private void HandleInsertion(BezierSplineComponent path)
        {
            var evt = Event.current;
            if (path.SegmentCount == 0 || evt.alt || ToolbarRect.Contains(evt.mousePosition)) return;
            var control = GUIUtility.GetControlID("BezierSplineInsert".GetHashCode(), FocusType.Passive);
            var bestDistance = float.MaxValue;
            var bestT = 0f;
            var samples = path.SegmentCount * 32;
            var previous = HandleUtility.WorldToGUIPoint(path.EvaluateWorldPosition(0f));
            for (var i = 1; i <= samples; i++)
            {
                var current = HandleUtility.WorldToGUIPoint(path.EvaluateWorldPosition(i / (float)samples));
                var delta = current - previous;
                var fraction = delta.sqrMagnitude > 0.000001f
                    ? Mathf.Clamp01(Vector2.Dot(evt.mousePosition - previous, delta) / delta.sqrMagnitude)
                    : 0f;
                var distance = Vector2.Distance(evt.mousePosition, previous + delta * fraction);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestT = (i - 1 + fraction) / samples;
                }
                previous = current;
            }

            if (evt.type == EventType.Layout && bestDistance <= 12f)
                HandleUtility.AddControl(control, bestDistance);
            if (evt.type == EventType.MouseMove) SceneView.RepaintAll();
            if (evt.type == EventType.Repaint && bestDistance <= 12f)
            {
                var position = path.EvaluateWorldPosition(bestT);
                Handles.color = Color.yellow;
                Handles.SphereHandleCap(control, position, Quaternion.identity,
                    HandleUtility.GetHandleSize(position) * 0.14f, EventType.Repaint);
            }
            if (evt.type == EventType.MouseDown && evt.button == 0 && bestDistance <= 12f
                && HandleUtility.nearestControl == control && GUIUtility.hotControl == 0)
            {
                Undo.RecordObject(path, "Insert Bézier Point");
                m_SelectedIndex = path.InsertPointAt(bestT);
                m_SelectedTangent = 0;
                m_InsertMode = false;
                EditorUtility.SetDirty(path);
                PrefabUtility.RecordPrefabInstancePropertyModifications(path);
                evt.Use();
                SceneView.RepaintAll();
            }
        }

        private static void DrawCurve(BezierSplineComponent path)
        {
            Handles.color = Color.green;
            for (var i = 0; i < path.SegmentCount; i++)
            {
                var nextIndex = path.Closed ? (i + 1) % path.Points.Count : i + 1;
                var current = path.Points[i];
                var next = path.Points[nextIndex];
                var start = path.transform.TransformPoint(current.Position);
                var end = path.transform.TransformPoint(next.Position);
                var startTangent = path.transform.TransformPoint(current.Position + current.OutTangent);
                var endTangent = path.transform.TransformPoint(next.Position + next.InTangent);
                Handles.DrawBezier(start, end, startTangent, endTangent, Color.cyan, null, 3.5f);
            }
        }

        private static void DrawPathMarkers(BezierSplineComponent path, bool showDirectionArrows)
        {
            var start = path.EvaluateWorldPosition(0f);
            var end = path.EvaluateWorldPosition(1f);
            Handles.color = Color.green;
            Handles.SphereHandleCap(0, start, Quaternion.identity, HandleUtility.GetHandleSize(start) * 0.18f, EventType.Repaint);
            if (!path.Closed)
            {
                Handles.color = Color.red;
                Handles.SphereHandleCap(0, end, Quaternion.identity, HandleUtility.GetHandleSize(end) * 0.18f, EventType.Repaint);
            }

            if (!showDirectionArrows)
            {
                return;
            }

            Handles.color = new Color(1f, 0.8f, 0.1f);
            var arrowCount = Mathf.Max(1, path.SegmentCount * 2);
            for (var i = 0; i < arrowCount; i++)
            {
                var t = (i + 0.5f) / arrowCount;
                var position = path.EvaluateWorldPosition(t);
                var tangent = path.EvaluateWorldTangent(t);
                if (tangent.sqrMagnitude > 0.000001f)
                {
                    Handles.ArrowHandleCap(0, position, Quaternion.LookRotation(tangent), HandleUtility.GetHandleSize(position) * 0.24f, EventType.Repaint);
                }
            }
        }

        private void HandleSceneCommands(BezierSplineComponent path)
        {
            var current = Event.current;
            if (GUIUtility.hotControl != 0)
            {
                return;
            }
            if (m_InsertMode && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                m_InsertMode = false;
                current.Use();
                SceneView.RepaintAll();
                return;
            }

            var hasSelection = m_SelectedIndex >= 0 && m_SelectedIndex < path.Points.Count;
            var deleteRequested = (current.type == EventType.KeyDown
                    && (current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace))
                || (current.type == EventType.ExecuteCommand && current.commandName == "Delete");

            if (deleteRequested)
            {
                if (hasSelection && path.Points.Count > 2)
                {
                    Undo.RecordObject(path, "Delete Bézier Point");
                    path.RemovePointAt(m_SelectedIndex);
                    m_SelectedIndex = Mathf.Clamp(m_SelectedIndex - 1, 0, path.Points.Count - 1);
                    EditorUtility.SetDirty(path);
                }

                if (current.type == EventType.KeyDown || current.type == EventType.ExecuteCommand)
                {
                    current.Use();
                }
                return;
            }

            if (current.type != EventType.KeyDown || !hasSelection)
            {
                return;
            }

            BezierPointMode? mode = current.keyCode == KeyCode.Alpha1 || current.keyCode == KeyCode.Keypad1
                    ? BezierPointMode.Corner
                    : current.keyCode == KeyCode.Alpha2 || current.keyCode == KeyCode.Keypad2
                        ? BezierPointMode.BezierCorner
                        : current.keyCode == KeyCode.Alpha3 || current.keyCode == KeyCode.Keypad3
                            ? BezierPointMode.Smooth
                            : (BezierPointMode?)null;
            if (!mode.HasValue)
            {
                return;
            }

            Undo.RecordObject(path, "Change Bézier Point Mode");
            path.SetMode(m_SelectedIndex, mode.Value);
            EditorUtility.SetDirty(path);
            current.Use();
        }

        private void DrawSceneToolbar(BezierSplineComponent path)
        {
            Handles.BeginGUI();
            var toolbarRect = ToolbarRect;
            GUI.Box(toolbarRect, GUIContent.none, GUI.skin.window);
            GUILayout.BeginArea(new Rect(toolbarRect.x + 6f, toolbarRect.y + 2f, toolbarRect.width - 12f, toolbarRect.height - 4f), GUIStyle.none);
            GUILayout.Label("Bezier Spline 编辑", EditorStyles.boldLabel);
            var hasSelection = m_SelectedIndex >= 0 && m_SelectedIndex < path.Points.Count;
            GUILayout.Label(m_InsertMode ? "插入模式：点击曲线，Esc 取消"
                : hasSelection ? $"锚点 {m_SelectedIndex + 1} / {path.Points.Count}"
                : "未选中锚点", EditorStyles.miniLabel);

            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("添加锚点", "在路径末尾添加一个锚点"), EditorStyles.miniButtonLeft))
            {
                AddPoint(path);
            }
            if (GUILayout.Button(new GUIContent(path.Closed ? "设为开放" : "闭合路径", "切换路径是否闭合"), EditorStyles.miniButtonMid))
            {
                Undo.RecordObject(path, path.Closed ? "Open Bézier Path" : "Close Bézier Path");
                path.Closed = !path.Closed;
                path.RebuildCache();
                EditorUtility.SetDirty(path);
            }
            if (GUILayout.Button(new GUIContent("反转路径", "反转锚点顺序和路径方向"), EditorStyles.miniButtonRight))
            {
                Undo.RecordObject(path, "Reverse Bézier Path");
                path.Reverse();
                if (m_SelectedIndex >= 0)
                {
                    m_SelectedIndex = path.Points.Count - 1 - m_SelectedIndex;
                }
                EditorUtility.SetDirty(path);
            }
            GUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(path.SegmentCount == 0))
            {
                var insertMode = GUILayout.Toggle(m_InsertMode, "插入模式", EditorStyles.miniButton);
                if (insertMode != m_InsertMode)
                {
                    m_InsertMode = insertMode;
                    SceneView.RepaintAll();
                }
            }
            if (GUILayout.Button(new GUIContent("重算 C2 连续控制柄", "保留锚点，重算所有控制柄和点类型；消除段接缝处的一、二阶导数跳变"), EditorStyles.miniButton))
            {
                Undo.RecordObject(path, "Recalculate C2 Bézier Tangents");
                path.RecalculateC2Tangents();
                EditorUtility.SetDirty(path);
                PrefabUtility.RecordPrefabInstancePropertyModifications(path);
                SceneView.RepaintAll();
            }
            GUILayout.EndVertical();
            GUILayout.Space(4f);

            GUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(hasSelection && path.Points[m_SelectedIndex].Mode == BezierPointMode.Corner,
                    new GUIContent("角点", "快捷键 1"), EditorStyles.miniButtonLeft)
                    && hasSelection && path.Points[m_SelectedIndex].Mode != BezierPointMode.Corner)
                {
                    SetSelectedMode(path, BezierPointMode.Corner);
                }
                if (GUILayout.Toggle(hasSelection && path.Points[m_SelectedIndex].Mode == BezierPointMode.BezierCorner,
                    new GUIContent("贝塞尔角点", "快捷键 2"), EditorStyles.miniButtonMid)
                    && hasSelection && path.Points[m_SelectedIndex].Mode != BezierPointMode.BezierCorner)
                {
                    SetSelectedMode(path, BezierPointMode.BezierCorner);
                }
                if (GUILayout.Toggle(hasSelection && path.Points[m_SelectedIndex].Mode == BezierPointMode.Smooth,
                    new GUIContent("平滑", "快捷键 3"), EditorStyles.miniButtonRight)
                    && hasSelection && path.Points[m_SelectedIndex].Mode != BezierPointMode.Smooth)
                {
                    SetSelectedMode(path, BezierPointMode.Smooth);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("重置控制柄", EditorStyles.miniButtonLeft))
                {
                    Undo.RecordObject(path, "Reset Bézier Tangents");
                    path.ResetTangents(m_SelectedIndex);
                    EditorUtility.SetDirty(path);
                }
                using (new EditorGUI.DisabledScope(path.Points.Count <= 2))
                {
                    if (GUILayout.Button("删除锚点", EditorStyles.miniButtonRight))
                    {
                        DeleteSelectedPoint(path);
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.Space(4f);

            GUILayout.BeginVertical(EditorStyles.helpBox);
            m_ShowDirectionArrows = GUILayout.Toggle(
                m_ShowDirectionArrows,
                new GUIContent("显示方向箭头", "显示路径运动方向"),
                EditorStyles.toolbarButton);
            GUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private void AddPoint(BezierSplineComponent path)
        {
            var position = path.Points.Count == 0
                ? Vector3.zero
                : path.Points[path.Points.Count - 1].Position
                    + (path.Points[path.Points.Count - 1].OutTangent.sqrMagnitude > 0.000001f
                        ? path.Points[path.Points.Count - 1].OutTangent
                        : Vector3.right * 3f);
            Undo.RecordObject(path, "Add Bézier Point");
            m_SelectedIndex = path.AddPoint(position);
            m_SelectedTangent = 0;
            EditorUtility.SetDirty(path);
            SceneView.RepaintAll();
        }

        private void SetSelectedMode(BezierSplineComponent path, BezierPointMode mode)
        {
            if (m_SelectedIndex < 0 || m_SelectedIndex >= path.Points.Count)
            {
                return;
            }

            Undo.RecordObject(path, "Change Bézier Point Mode");
            path.SetMode(m_SelectedIndex, mode);
            EditorUtility.SetDirty(path);
            SceneView.RepaintAll();
        }

        private void DeleteSelectedPoint(BezierSplineComponent path)
        {
            if (m_SelectedIndex < 0 || m_SelectedIndex >= path.Points.Count || path.Points.Count <= 2)
            {
                return;
            }

            Undo.RecordObject(path, "Delete Bézier Point");
            path.RemovePointAt(m_SelectedIndex);
            m_SelectedIndex = Mathf.Clamp(m_SelectedIndex - 1, 0, path.Points.Count - 1);
            EditorUtility.SetDirty(path);
            SceneView.RepaintAll();
        }

        private void DrawPoint(BezierSplineComponent path, int index)
        {
            var point = path.Points[index];
            var position = path.transform.TransformPoint(point.Position);
            var size = HandleUtility.GetHandleSize(position) * 0.11f;

            Handles.color = index == m_SelectedIndex ? Color.yellow : Color.white;
            if (Handles.Button(position, Quaternion.identity, size, size, Handles.CubeHandleCap))
            {
                m_SelectedIndex = index;
                m_SelectedTangent = 0;
                GUI.changed = true;
            }

            if (index == m_SelectedIndex)
            {
                EditPosition(path, index, position);
            }

            if (point.Mode == BezierPointMode.Corner)
            {
                return;
            }

            var inTangentPosition = path.transform.TransformPoint(point.Position + point.InTangent);
            var outTangentPosition = path.transform.TransformPoint(point.Position + point.OutTangent);
            Handles.color = Color.gray;
            Handles.DrawLine(position, inTangentPosition);
            Handles.DrawLine(position, outTangentPosition);

            Handles.color = m_SelectedIndex == index && m_SelectedTangent == 1
                ? Color.yellow
                : Color.white;
            if (Handles.Button(inTangentPosition, Quaternion.identity, size * 0.8f, size * 0.8f, Handles.SphereHandleCap))
            {
                m_SelectedIndex = index;
                m_SelectedTangent = 1;
                GUI.changed = true;
            }

            Handles.color = m_SelectedIndex == index && m_SelectedTangent == 2
                ? Color.yellow
                : Color.white;
            if (Handles.Button(outTangentPosition, Quaternion.identity, size * 0.8f, size * 0.8f, Handles.SphereHandleCap))
            {
                m_SelectedIndex = index;
                m_SelectedTangent = 2;
                GUI.changed = true;
            }

            if (m_SelectedIndex == index)
            {
                EditTangent(path, index, position, 1, inTangentPosition);
                EditTangent(path, index, position, 2, outTangentPosition);
            }
        }

        private void EditPosition(BezierSplineComponent path, int index, Vector3 position)
        {
            EditorGUI.BeginChangeCheck();
            var newPosition = Handles.PositionHandle(position, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(path, "Move Bézier Point");
            path.Points[index].Position = path.transform.InverseTransformPoint(newPosition);
            path.RebuildCache();
            EditorUtility.SetDirty(path);
        }

        private void EditTangent(BezierSplineComponent path, int index, Vector3 position, int tangent, Vector3 tangentPosition)
        {
            if (m_SelectedTangent != tangent)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            var newPosition = Handles.PositionHandle(tangentPosition, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            Undo.RecordObject(path, "Move Bézier Tangent");
            var localTangent = path.transform.InverseTransformVector(newPosition - position);
            path.SetTangent(index, tangent == 1, localTangent);
            path.RebuildCache();
            EditorUtility.SetDirty(path);
        }
    }
}
