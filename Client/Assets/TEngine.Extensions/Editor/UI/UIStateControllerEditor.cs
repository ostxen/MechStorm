using UnityEditor;
using UnityEngine;

namespace TEngine.Extensions.UGUI.Editor
{
    [CustomEditor(typeof(UIStateController))]
    public sealed class UIStateControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty _groups;
        private SerializedProperty _links;

        private void OnEnable()
        {
            _groups = serializedObject.FindProperty("_groups");
            _links = serializedObject.FindProperty("_links");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("UI State Controller", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "状态组负责管理 UI 表现状态。修改配置后点击“应用当前状态”预览。",
                MessageType.Info);

            DrawGroups();
            DrawLinks();

            serializedObject.ApplyModifiedProperties();
            DrawValidation();
            EditorGUILayout.Space();
            DrawPreviewButtons();
        }

        private void DrawGroups()
        {
            EditorGUILayout.LabelField("状态组", EditorStyles.boldLabel);
            for (var groupIndex = 0; groupIndex < _groups.arraySize; groupIndex++)
            {
                var group = _groups.GetArrayElementAtIndex(groupIndex);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var groupName = group.FindPropertyRelative("_name");
                    var defaultIndex = group.FindPropertyRelative("_defaultStateIndex");
                    var currentIndex = group.FindPropertyRelative("_currentStateIndex");
                    var states = group.FindPropertyRelative("_states");

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(groupName, new GUIContent("组名"));
                        if (GUILayout.Button("删除", GUILayout.Width(52f)))
                        {
                            _groups.DeleteArrayElementAtIndex(groupIndex);
                            break;
                        }
                    }

                    DrawStateIndexPopup("默认状态", defaultIndex, states);
                    DrawStateIndexPopup("当前状态", currentIndex, states);
                    DrawStates(states);
                }
            }

            if (GUILayout.Button("添加状态组"))
            {
                _groups.arraySize++;
                var group = _groups.GetArrayElementAtIndex(_groups.arraySize - 1);
                group.FindPropertyRelative("_name").stringValue = "StateGroup";
                group.FindPropertyRelative("_states").arraySize = 1;
                group.FindPropertyRelative("_states").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("_name").stringValue = "Default";
            }
        }

        private static void DrawStates(SerializedProperty states)
        {
            EditorGUILayout.LabelField("状态项", EditorStyles.miniBoldLabel);
            for (var stateIndex = 0; stateIndex < states.arraySize; stateIndex++)
            {
                var state = states.GetArrayElementAtIndex(stateIndex);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(
                            state.FindPropertyRelative("_name"),
                            new GUIContent($"状态 {stateIndex}"));
                        if (GUILayout.Button("删除", GUILayout.Width(52f)))
                        {
                            states.DeleteArrayElementAtIndex(stateIndex);
                            break;
                        }
                    }

                    DrawBindings(state.FindPropertyRelative("_bindings"));
                }
            }

            if (GUILayout.Button("添加状态"))
            {
                states.arraySize++;
                states.GetArrayElementAtIndex(states.arraySize - 1)
                    .FindPropertyRelative("_name").stringValue = "State";
            }
        }

        private static void DrawBindings(SerializedProperty bindings)
        {
            EditorGUILayout.LabelField("绑定", EditorStyles.miniBoldLabel);
            for (var bindingIndex = 0; bindingIndex < bindings.arraySize; bindingIndex++)
            {
                var binding = bindings.GetArrayElementAtIndex(bindingIndex);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var type = binding.FindPropertyRelative("_type");
                        EditorGUILayout.PropertyField(type, new GUIContent("类型"));
                        if (GUILayout.Button("删除", GUILayout.Width(52f)))
                        {
                            bindings.DeleteArrayElementAtIndex(bindingIndex);
                            break;
                        }
                    }

                    EditorGUILayout.PropertyField(
                        binding.FindPropertyRelative("_target"),
                        new GUIContent("目标组件"));

                    var bindingType = (UIStateBindingType)binding
                        .FindPropertyRelative("_type").enumValueIndex;
                    switch (bindingType)
                    {
                        case UIStateBindingType.GameObjectActive:
                        case UIStateBindingType.SelectableInteractable:
                            EditorGUILayout.PropertyField(
                                binding.FindPropertyRelative("_boolValue"),
                                new GUIContent("值"));
                            break;
                        case UIStateBindingType.GraphicColor:
                            EditorGUILayout.PropertyField(
                                binding.FindPropertyRelative("_colorValue"),
                                new GUIContent("颜色"));
                            break;
                        case UIStateBindingType.ImageSprite:
                            EditorGUILayout.PropertyField(
                                binding.FindPropertyRelative("_spriteValue"),
                                new GUIContent("Sprite"));
                            break;
                        case UIStateBindingType.ImageSpriteAndColor:
                            EditorGUILayout.PropertyField(
                                binding.FindPropertyRelative("_spriteValue"),
                                new GUIContent("Sprite"));
                            EditorGUILayout.PropertyField(
                                binding.FindPropertyRelative("_colorValue"),
                                new GUIContent("颜色"));
                            break;
                        case UIStateBindingType.TextContent:
                            EditorGUILayout.PropertyField(
                                binding.FindPropertyRelative("_textValue"),
                                new GUIContent("文本"));
                            break;
                        case UIStateBindingType.CanvasGroupAlpha:
                            EditorGUILayout.PropertyField(
                                binding.FindPropertyRelative("_floatValue"),
                                new GUIContent("透明度"));
                            break;
                        case UIStateBindingType.RectTransformAnchoredPosition:
                            EditorGUILayout.PropertyField(
                                binding.FindPropertyRelative("_vector2Value"),
                                new GUIContent("锚点位置"));
                            break;
                    }
                }
            }

            if (GUILayout.Button("添加绑定"))
            {
                bindings.arraySize++;
            }
        }

        private void DrawLinks()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("状态联动", EditorStyles.boldLabel);
            for (var linkIndex = 0; linkIndex < _links.arraySize; linkIndex++)
            {
                var link = _links.GetArrayElementAtIndex(linkIndex);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.PropertyField(
                        link.FindPropertyRelative("_sourceGroup"),
                        GUIContent.none);
                    EditorGUILayout.LabelField("=", GUILayout.Width(14f));
                    EditorGUILayout.PropertyField(
                        link.FindPropertyRelative("_sourceState"),
                        GUIContent.none);
                    EditorGUILayout.LabelField("→", GUILayout.Width(18f));
                    EditorGUILayout.PropertyField(
                        link.FindPropertyRelative("_targetGroup"),
                        GUIContent.none);
                    EditorGUILayout.LabelField("=", GUILayout.Width(14f));
                    EditorGUILayout.PropertyField(
                        link.FindPropertyRelative("_targetState"),
                        GUIContent.none);
                    if (GUILayout.Button("×", GUILayout.Width(24f)))
                    {
                        _links.DeleteArrayElementAtIndex(linkIndex);
                        break;
                    }
                }
            }

            if (GUILayout.Button("添加状态联动"))
            {
                _links.arraySize++;
            }
        }

        private static void DrawStateIndexPopup(
            string label,
            SerializedProperty index,
            SerializedProperty states)
        {
            var names = new string[states.arraySize + 1];
            names[0] = "无状态";
            for (var i = 0; i < states.arraySize; i++)
            {
                names[i + 1] = states.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("_name").stringValue;
            }

            var selected = Mathf.Clamp(index.intValue + 1, 0, names.Length - 1);
            var next = EditorGUILayout.Popup(label, selected, names);
            index.intValue = next - 1;
        }

        private void DrawPreviewButtons()
        {
            if (GUILayout.Button("应用当前状态"))
            {
                ApplyPreview((UIStateController)target);
            }

            if (GUILayout.Button("恢复并应用默认状态"))
            {
                var controller = (UIStateController)target;
                Undo.RecordObject(controller, "Reset UI state");
                for (var i = 0; i < controller.Groups.Count; i++)
                {
                    controller.ResetState(controller.Groups[i].Name);
                }

                ApplyPreview(controller);
            }
        }

        private void DrawValidation()
        {
            var messages = new System.Collections.Generic.List<string>();
            var groupNames = new System.Collections.Generic.HashSet<string>();
            for (var groupIndex = 0; groupIndex < _groups.arraySize; groupIndex++)
            {
                var group = _groups.GetArrayElementAtIndex(groupIndex);
                var groupName = group.FindPropertyRelative("_name").stringValue;
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    messages.Add($"状态组 {groupIndex} 没有名称。");
                }
                else if (!groupNames.Add(groupName))
                {
                    messages.Add($"状态组名称重复：{groupName}。");
                }

                var states = group.FindPropertyRelative("_states");
                var stateNames = new System.Collections.Generic.HashSet<string>();
                for (var stateIndex = 0; stateIndex < states.arraySize; stateIndex++)
                {
                    var state = states.GetArrayElementAtIndex(stateIndex);
                    var stateName = state.FindPropertyRelative("_name").stringValue;
                    if (string.IsNullOrWhiteSpace(stateName))
                    {
                        messages.Add($"{groupName} 的状态 {stateIndex} 没有名称。");
                    }
                    else if (!stateNames.Add(stateName))
                    {
                        messages.Add($"{groupName} 的状态名称重复：{stateName}。");
                    }
                }
            }

            for (var linkIndex = 0; linkIndex < _links.arraySize; linkIndex++)
            {
                var link = _links.GetArrayElementAtIndex(linkIndex);
                var sourceGroup = link.FindPropertyRelative("_sourceGroup").stringValue;
                var sourceState = link.FindPropertyRelative("_sourceState").stringValue;
                var targetGroup = link.FindPropertyRelative("_targetGroup").stringValue;
                var targetState = link.FindPropertyRelative("_targetState").stringValue;
                if (!HasState(sourceGroup, sourceState) ||
                    !HasState(targetGroup, targetState))
                {
                    messages.Add(
                        $"联动 {linkIndex} 无效：{sourceGroup}={sourceState} → " +
                        $"{targetGroup}={targetState}。");
                }
            }

            for (var i = 0; i < messages.Count; i++)
            {
                EditorGUILayout.HelpBox(messages[i], MessageType.Warning);
            }
        }

        private bool HasState(string groupName, string stateName)
        {
            for (var groupIndex = 0; groupIndex < _groups.arraySize; groupIndex++)
            {
                var group = _groups.GetArrayElementAtIndex(groupIndex);
                if (!string.Equals(
                        group.FindPropertyRelative("_name").stringValue,
                        groupName,
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                var states = group.FindPropertyRelative("_states");
                for (var stateIndex = 0; stateIndex < states.arraySize; stateIndex++)
                {
                    if (string.Equals(
                            states.GetArrayElementAtIndex(stateIndex)
                                .FindPropertyRelative("_name").stringValue,
                            stateName,
                            System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ApplyPreview(UIStateController controller)
        {
            var targets = new System.Collections.Generic.List<UnityEngine.Object>();
            controller.GetReferencedObjects(targets);
            for (var i = 0; i < targets.Count; i++)
            {
                Undo.RecordObject(targets[i], "Apply UI state");
            }

            controller.ApplyAll();
            EditorUtility.SetDirty(controller);
            for (var i = 0; i < targets.Count; i++)
            {
                EditorUtility.SetDirty(targets[i]);
            }
        }
    }
}
