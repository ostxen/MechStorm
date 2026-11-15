using System;
using System.Collections.Generic;
using GBG.PlayableGraphMonitor.Editor;
using TEngine.Extensions.SimpleAnimation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace TEngine.Extensions.Editor.Animation
{
    /// <summary>
    /// 将当前选中对象的动画标识、层和权重定期同步到 PlayableGraph Monitor，辅助定位播放状态。
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayableGraphMonitorAdapter
    {
        private const double RefreshIntervalSeconds = 0.25d;
        private static double _nextRefreshTime;

        static PlayableGraphMonitorAdapter()
        {
            Selection.selectionChanged += RefreshLabels;
            EditorApplication.update += RefreshLabelsIfNeeded;
            AssemblyReloadEvents.beforeAssemblyReload += Unregister;
        }

        [MenuItem("TEngine/Animation/Open PlayableGraph Monitor")]
        private static void OpenMonitor()
        {
            PlayableGraphMonitorWindow.Open();
            RefreshLabels();
        }

        [MenuItem("TEngine/Animation/Refresh PlayableGraph Labels")]
        private static void RefreshLabelsMenu()
        {
            RefreshLabels();
        }

        private static void RefreshLabelsIfNeeded()
        {
            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
            {
                return;
            }

            RefreshLabels();
        }

        private static void RefreshLabels()
        {
            _nextRefreshTime =
                EditorApplication.timeSinceStartup + RefreshIntervalSeconds;

            var controller = FindSelectedController();
            IReadOnlyDictionary<PlayableHandle, string> labels =
                controller == null
                    ? null
                    : controller.BuildMonitorLabelTable();
            PlayableGraphMonitorWindow.TrySetNodeExtraLabelTable(labels);
        }

        private static PlayableAnimationController FindSelectedController()
        {
            var component = Selection.activeObject as Component;
            if (component != null)
            {
                var selectedController =
                    component.GetComponentInParent<PlayableAnimationController>();
                if (selectedController != null)
                {
                    return selectedController;
                }
            }

            return Selection.activeGameObject == null
                ? null
                : Selection.activeGameObject.GetComponentInParent<
                    PlayableAnimationController>();
        }

        private static void Unregister()
        {
            Selection.selectionChanged -= RefreshLabels;
            EditorApplication.update -= RefreshLabelsIfNeeded;
            AssemblyReloadEvents.beforeAssemblyReload -= Unregister;
        }
    }
}
