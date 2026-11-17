using System.Collections.Generic;
using TEngine.Extensions.SimpleAnimation;
using UnityEditor;
using UnityEngine;

namespace TEngine.Extensions.Editor.Animation
{
    /// <summary>编辑 Clip 引用集合，并显示标识与资源引用的校验结果。</summary>
    [CustomEditor(typeof(AnimationClipLibrary))]
    internal sealed class AnimationClipLibraryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawValidation(
                AnimationClipCollectionValidator.Validate(
                    (AnimationClipLibrary)target));
        }

        private static void DrawValidation(
            List<AnimationValidationIssue> issues)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "SimplePlayable Validation",
                EditorStyles.boldLabel);
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("校验通过。", MessageType.Info);
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                var type = issues[i].Severity ==
                    AnimationValidationSeverity.Error
                    ? MessageType.Error
                    : MessageType.Warning;
                EditorGUILayout.HelpBox(issues[i].Message, type);
            }
        }
    }

    /// <summary>编辑动画加载目录，并显示资源地址与标识的校验结果。</summary>
    [CustomEditor(typeof(AnimationClipCatalog))]
    internal sealed class AnimationClipCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var issues = AnimationClipCollectionValidator.Validate(
                (AnimationClipCatalog)target);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "SimplePlayable Validation",
                EditorStyles.boldLabel);
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("校验通过。", MessageType.Info);
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                var type = issues[i].Severity ==
                    AnimationValidationSeverity.Error
                    ? MessageType.Error
                    : MessageType.Warning;
                EditorGUILayout.HelpBox(issues[i].Message, type);
            }
        }
    }

    /// <summary>编辑可复用播放方案，并显示标识与播放参数的校验结果。</summary>
    [CustomEditor(typeof(AnimationPlaybackOptionsSet))]
    internal sealed class AnimationPlaybackOptionsSetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var issues = AnimationClipCollectionValidator.Validate(
                (AnimationPlaybackOptionsSet)target);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "SimplePlayable Validation",
                EditorStyles.boldLabel);
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("校验通过。", MessageType.Info);
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                var type = issues[i].Severity ==
                    AnimationValidationSeverity.Error
                    ? MessageType.Error
                    : MessageType.Warning;
                EditorGUILayout.HelpBox(issues[i].Message, type);
            }
        }
    }
}
