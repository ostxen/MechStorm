using UnityEditor;
using UnityEditor.UI;

namespace TEngine.Extensions.UGUI.Editor
{
    [CustomEditor(typeof(ButtonEx), true)]
    [CanEditMultipleObjects]
    public sealed class ButtonExEditor : ButtonEditor
    {
        private SerializedProperty _canClick;
        private SerializedProperty _canDoubleClick;
        private SerializedProperty _doubleClickInterval;
        private SerializedProperty _onDoubleClick;
        private SerializedProperty _canLongPress;
        private SerializedProperty _responseOnceByPress;
        private SerializedProperty _longPressDuration;
        private SerializedProperty _longPressInterval;
        private SerializedProperty _onPress;
        private SerializedProperty _onBeginDrag;
        private SerializedProperty _onDrag;
        private SerializedProperty _onEndDrag;

        protected override void OnEnable()
        {
            base.OnEnable();
            _canClick = serializedObject.FindProperty("canClick");
            _canDoubleClick = serializedObject.FindProperty("canDoubleClick");
            _doubleClickInterval = serializedObject.FindProperty("doubleClickInterval");
            _onDoubleClick = serializedObject.FindProperty("onDoubleClick");
            _canLongPress = serializedObject.FindProperty("canLongPress");
            _responseOnceByPress = serializedObject.FindProperty("responseOnceByPress");
            _longPressDuration = serializedObject.FindProperty("longPressDuration");
            _longPressInterval = serializedObject.FindProperty("longPressInterval");
            _onPress = serializedObject.FindProperty("onPress");
            _onBeginDrag = serializedObject.FindProperty("onBeginDrag");
            _onDrag = serializedObject.FindProperty("onDrag");
            _onEndDrag = serializedObject.FindProperty("onEndDrag");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_canClick);
            EditorGUILayout.PropertyField(_canDoubleClick);
            EditorGUILayout.PropertyField(_doubleClickInterval);
            EditorGUILayout.PropertyField(_onDoubleClick);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_canLongPress);
            EditorGUILayout.PropertyField(_responseOnceByPress);
            EditorGUILayout.PropertyField(_longPressDuration);
            EditorGUILayout.PropertyField(_longPressInterval);
            EditorGUILayout.PropertyField(_onPress);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_onBeginDrag);
            EditorGUILayout.PropertyField(_onDrag);
            EditorGUILayout.PropertyField(_onEndDrag);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
