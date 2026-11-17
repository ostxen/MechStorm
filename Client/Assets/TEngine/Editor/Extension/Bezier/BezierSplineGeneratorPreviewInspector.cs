using UnityEditor;
using UnityEngine;

namespace TEngine.Editor
{
    [CustomEditor(typeof(BezierSplineGeneratorPreview))]
    internal sealed class BezierSplineGeneratorPreviewInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("引用起点和目标后，Scene 会实时预览生成的 Bézier 样条，不会修改 BezierSplineComponent。", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_start"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_target"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_arcHeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_sideOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_samples"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_showDirection"));
            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            var preview = (BezierSplineGeneratorPreview)target;
            if (preview.Start == null || preview.Target == null)
            {
                return;
            }

            Handles.color = Color.yellow;
            Handles.DrawBezier(
                preview.Start.position,
                preview.Target.position,
                preview.Start.position + preview.EvaluateTangent(0f) * 0.3f,
                preview.Target.position - preview.EvaluateTangent(1f) * 0.3f,
                Color.yellow,
                null,
                2f);
        }
    }
}
