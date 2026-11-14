using MechStorm.Presentation.UI;
using UnityEditor;

namespace MechStorm.Editor
{
    [CustomEditor(typeof(SegmentedSlider))]
    public sealed class SegmentedSliderEditor : UnityEditor.Editor
    {
        private SerializedProperty _highlightMode;
        private SerializedProperty _highlightCount;
        private SerializedProperty _highlightDirection;
        private SerializedProperty _highlightAnchor;
        private SerializedProperty _breathingHighlightColor;
        private SerializedProperty _breathingHighlightSpeed;
        private SerializedProperty _breathingHighlightStrength;

        private void OnEnable()
        {
            _highlightMode = serializedObject.FindProperty("_highlightMode");
            _highlightCount = serializedObject.FindProperty("_highlightCount");
            _highlightDirection = serializedObject.FindProperty("_highlightDirection");
            _highlightAnchor = serializedObject.FindProperty("_highlightAnchor");
            _breathingHighlightColor = serializedObject.FindProperty("_breathingHighlightColor");
            _breathingHighlightSpeed = serializedObject.FindProperty("_breathingHighlightSpeed");
            _breathingHighlightStrength = serializedObject.FindProperty("_breathingHighlightStrength");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject,
                "m_Script",
                "m_RaycastTarget",
                "m_OnCullStateChanged",
                "_highlightMode",
                "_highlightCount",
                "_highlightDirection",
                "_highlightAnchor",
                "_breathingHighlightColor",
                "_breathingHighlightSpeed",
                "_breathingHighlightStrength");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Breathing Highlight", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_highlightMode);
            if (_highlightMode.enumValueIndex == (int)SegmentedSliderHighlightMode.Contiguous)
            {
                EditorGUILayout.PropertyField(_highlightCount);
                EditorGUILayout.PropertyField(_highlightDirection);
                EditorGUILayout.PropertyField(_highlightAnchor);
                EditorGUILayout.PropertyField(_breathingHighlightColor);
                EditorGUILayout.PropertyField(_breathingHighlightSpeed);
                EditorGUILayout.PropertyField(_breathingHighlightStrength);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
