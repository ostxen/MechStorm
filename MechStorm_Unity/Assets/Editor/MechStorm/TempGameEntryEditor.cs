using MechStorm.Presentation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MechStorm.Editor
{
    [CustomEditor(typeof(TempGameEntry))]
    public sealed class TempGameEntryEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            root.Add(new HelpBox(
                "Runtime controls are available in the in-game UI Toolkit panel. " +
                "This Inspector only edits temporary scene configuration.",
                HelpBoxMessageType.Info));
            return root;
        }
    }
}
