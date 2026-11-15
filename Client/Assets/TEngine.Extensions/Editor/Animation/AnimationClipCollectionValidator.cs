using System;
using System.Collections.Generic;
using TEngine.Extensions.SimpleAnimation;
using UnityEditor;
using UnityEngine;

namespace TEngine.Extensions.Editor.Animation
{
    internal enum AnimationValidationSeverity
    {
        Warning,
        Error
    }

    internal sealed class AnimationValidationIssue
    {
        public AnimationValidationIssue(
            AnimationValidationSeverity severity,
            string message)
        {
            Severity = severity;
            Message = message;
        }

        public AnimationValidationSeverity Severity { get; }

        public string Message { get; }
    }

    /// <summary>检查动画集合中的标识、资源引用和播放参数，返回可在 Inspector 或菜单显示的问题。</summary>
    internal static class AnimationClipCollectionValidator
    {
        public static List<AnimationValidationIssue> Validate(
            AnimationClipLibrary library)
        {
            var issues = new List<AnimationValidationIssue>();
            if (library == null)
            {
                AddError(issues, "AnimationClipLibrary 为空。");
                return issues;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < library.Entries.Count; i++)
            {
                var entry = library.Entries[i];
                if (entry == null)
                {
                    AddError(issues, $"Entries[{i}] 为空。");
                    continue;
                }

                ValidateKey(issues, keys, entry.AnimationKey, i);
                if (entry.Clip == null)
                {
                    AddError(issues, $"Entries[{i}] ({entry.AnimationKey}) 缺少 AnimationClip。");
                }

                ValidateLayer(issues, entry.Layer, i);
                ValidateWrapMode(issues, entry.WrapMode, i);
            }

            return issues;
        }

        public static List<AnimationValidationIssue> Validate(
            AnimationClipCatalog catalog)
        {
            var issues = new List<AnimationValidationIssue>();
            if (catalog == null)
            {
                AddError(issues, "AnimationClipCatalog 为空。");
                return issues;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                var entry = catalog.Entries[i];
                if (entry == null)
                {
                    AddError(issues, $"Entries[{i}] 为空。");
                    continue;
                }

                ValidateKey(issues, keys, entry.AnimationKey, i);
                if (string.IsNullOrWhiteSpace(entry.Location))
                {
                    AddError(
                        issues,
                        $"Entries[{i}] ({entry.AnimationKey}) 缺少 YooAsset Location。");
                }

                if (entry.PackageName != null &&
                    entry.PackageName.IndexOf('/') >= 0)
                {
                    AddWarning(
                        issues,
                        $"Entries[{i}] ({entry.AnimationKey}) 的 PackageName 看起来不是包名：{entry.PackageName}");
                }

                ValidateLayer(issues, entry.Layer, i);
                ValidateWrapMode(issues, entry.WrapMode, i);
            }

            return issues;
        }

        public static List<AnimationValidationIssue> Validate(
            AnimationPlaybackOptionsSet set)
        {
            var issues = new List<AnimationValidationIssue>();
            if (set == null)
            {
                AddError(issues, "AnimationPlaybackOptionsSet 为空。");
                return issues;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < set.Requests.Count; i++)
            {
                var request = set.Requests[i];
                if (request == null)
                {
                    AddError(issues, $"Requests[{i}] 为空。");
                    continue;
                }

                ValidateKey(
                    issues,
                    keys,
                    request.AnimationKey,
                    i,
                    "Requests");
                if (!request.IsValid)
                {
                    AddError(
                        issues,
                        $"Requests[{i}] ({request.AnimationKey}) 参数无效。");
                }
            }

            return issues;
        }

        private static void ValidateKey(
            List<AnimationValidationIssue> issues,
            HashSet<string> keys,
            string key,
            int index,
            string collectionName = "Entries")
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                AddError(
                    issues,
                    $"{collectionName}[{index}] 缺少 AnimationKey。");
                return;
            }

            if (!keys.Add(key))
            {
                AddError(issues, $"AnimationKey 重复：{key}");
            }
        }

        private static void ValidateLayer(
            List<AnimationValidationIssue> issues,
            int layer,
            int index)
        {
            if (layer < 0 || layer >= PlayableAnimationController.MaxLayerCount)
            {
                AddError(
                    issues,
                    $"Entries[{index}] 的 Layer {layer} 超出 0–{PlayableAnimationController.MaxLayerCount - 1}。");
            }
        }

        private static void ValidateWrapMode(List<AnimationValidationIssue> issues, WrapMode mode, int index)
        {
            if (!AnimationClipNode.IsSupportedWrapMode(mode))
                AddError(issues, $"Entries[{index}] 仅支持 Once 或 Loop。");
        }

        private static void AddError(
            List<AnimationValidationIssue> issues,
            string message)
        {
            issues.Add(new AnimationValidationIssue(
                AnimationValidationSeverity.Error,
                message));
        }

        private static void AddWarning(
            List<AnimationValidationIssue> issues,
            string message)
        {
            issues.Add(new AnimationValidationIssue(
                AnimationValidationSeverity.Warning,
                message));
        }
    }

    internal static class AnimationClipCollectionValidationMenu
    {
        [MenuItem("TEngine/Animation/Validate Selected Collection")]
        private static void ValidateSelectedCollection()
        {
            var asset = Selection.activeObject;
            var issues = Validate(asset);
            if (issues == null)
            {
                EditorUtility.DisplayDialog(
                    "Animation Validation",
                    "请选择 AnimationClipLibrary、AnimationClipCatalog 或 AnimationPlaybackOptionsSet。",
                    "OK");
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(asset);
            var errorCount = Count(
                issues,
                AnimationValidationSeverity.Error);
            var warningCount = Count(
                issues,
                AnimationValidationSeverity.Warning);
            var message = issues.Count == 0
                ? "校验通过。"
                : $"Errors: {errorCount}\nWarnings: {warningCount}\n\n" +
                  string.Join("\n", ToMessages(issues));
            Debug.Log(
                $"[SimplePlayable] {assetPath}\n{message}",
                asset);
            EditorUtility.DisplayDialog(
                "Animation Validation",
                message,
                "OK");
        }

        [MenuItem("TEngine/Animation/Validate Selected Collection", true)]
        private static bool ValidateSelectedCollectionEnabled()
        {
            return Selection.activeObject is AnimationClipLibrary ||
                   Selection.activeObject is AnimationClipCatalog ||
                   Selection.activeObject is AnimationPlaybackOptionsSet;
        }

        public static List<AnimationValidationIssue> Validate(
            UnityEngine.Object asset)
        {
            var library = asset as AnimationClipLibrary;
            if (library != null)
            {
                return AnimationClipCollectionValidator.Validate(library);
            }

            var catalog = asset as AnimationClipCatalog;
            if (catalog != null)
            {
                return AnimationClipCollectionValidator.Validate(catalog);
            }

            var requestSet = asset as AnimationPlaybackOptionsSet;
            return requestSet == null
                ? null
                : AnimationClipCollectionValidator.Validate(requestSet);
        }

        private static int Count(
            List<AnimationValidationIssue> issues,
            AnimationValidationSeverity severity)
        {
            var count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }

        private static IEnumerable<string> ToMessages(
            List<AnimationValidationIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                var prefix = issues[i].Severity ==
                    AnimationValidationSeverity.Error
                    ? "Error"
                    : "Warning";
                yield return $"[{prefix}] {issues[i].Message}";
            }
        }
    }
}
