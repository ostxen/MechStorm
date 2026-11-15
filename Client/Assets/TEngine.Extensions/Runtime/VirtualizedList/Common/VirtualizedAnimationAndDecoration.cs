using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.VirtualizedList
{
    /// <summary>
    /// 简单 CanvasGroup 淡入动画。复杂动画可直接实现 IVirtualizedItemAnimator。
    /// </summary>
    public sealed class VirtualizedFadeAnimator : MonoBehaviour, IVirtualizedItemAnimator
    {
        public float Duration = 0.15f;
        private readonly List<CanvasGroup> _groups = new();

        public void OnShown(MonoBehaviour item, int index)
        {
            var group = item.GetComponent<CanvasGroup>() ??
                        item.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            if (!_groups.Contains(group))
            {
                _groups.Add(group);
            }
        }

        public void OnRecycled(MonoBehaviour item, int index)
        {
            var group = item.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                _groups.Remove(group);
            }
        }

        private void Update()
        {
            var delta = Duration <= 0f ? 1f : Time.unscaledDeltaTime / Duration;
            for (var i = _groups.Count - 1; i >= 0; i--)
            {
                var group = _groups[i];
                if (group == null)
                {
                    _groups.RemoveAt(i);
                    continue;
                }

                group.alpha = Mathf.Min(1f, group.alpha + delta);
                if (group.alpha >= 1f)
                {
                    _groups.RemoveAt(i);
                }
            }
        }
    }

    public sealed class DelegateVirtualizedDecoration : IVirtualizedItemDecoration
    {
        private readonly Func<int, Rect, Rect> _decorator;

        public DelegateVirtualizedDecoration(Func<int, Rect, Rect> decorator)
        {
            _decorator = decorator ?? throw new ArgumentNullException(nameof(decorator));
        }

        public Rect Decorate(int index, Rect layoutRect)
        {
            return _decorator(index, layoutRect);
        }
    }
}
