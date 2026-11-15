using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 从当前权重启动线性过渡，将目标节点淡入至 1，其余参与节点淡出至 0 后暂停。
    /// </summary>
    public sealed class AnimationFadeGroup
    {
        private readonly List<FadeOutEntry> _fadeOutNodes = new List<FadeOutEntry>();
        private AnimationPlayableNode _fadeInNode;
        private float _fadeInStartWeight;
        private float _duration;
        private float _elapsed;

        public bool IsActive => _fadeInNode != null;

        public float Progress => _duration <= 0f
            ? 1f
            : Mathf.Clamp01(_elapsed / _duration);

        public void Start(
            AnimationPlayableNode fadeInNode,
            IReadOnlyList<AnimationPlayableNode> fadeOutNodes,
            float duration)
        {
            if (fadeInNode == null)
            {
                throw new ArgumentNullException(nameof(fadeInNode));
            }

            if (fadeOutNodes == null) throw new ArgumentNullException(nameof(fadeOutNodes));
            if (float.IsNaN(duration) || float.IsInfinity(duration))
                throw new ArgumentOutOfRangeException(nameof(duration));

            Cancel(false);

            _fadeInNode = fadeInNode;
            _fadeInStartWeight = fadeInNode.Weight;
            _duration = Mathf.Max(0f, duration);
            _elapsed = 0f;

            for (int i = 0; i < fadeOutNodes.Count; i++)
            {
                var node = fadeOutNodes[i];
                if (node == null || node == fadeInNode || node.Weight <= 0f)
                {
                    continue;
                }

                _fadeOutNodes.Add(new FadeOutEntry(node, node.Weight));
                if (node.PlaybackState != AnimationPlaybackState.Completed) node.Play();
            }

            fadeInNode.Play();
            if (_duration <= 0f)
            {
                ApplyProgress(1f);
                Complete();
            }
        }

        public void Update(float deltaTime)
        {
            if (!IsActive)
            {
                return;
            }

            _elapsed += Mathf.Max(0f, deltaTime);
            var progress = Progress;
            ApplyProgress(progress);

            if (progress >= 1f)
            {
                Complete();
            }
        }

        public void Cancel(bool stopNodes = true)
        {
            if (_fadeInNode != null && stopNodes)
            {
                _fadeInNode.Pause();
            }

            if (stopNodes)
            {
                for (int i = 0; i < _fadeOutNodes.Count; i++)
                {
                    var node = _fadeOutNodes[i].Node;
                    node.Pause();
                    node.SetWeight(0f);
                }
            }

            _fadeInNode = null;
            _fadeOutNodes.Clear();
            _elapsed = 0f;
            _duration = 0f;
        }

        private void ApplyProgress(float progress)
        {
            if (_fadeInNode != null)
            {
                _fadeInNode.SetWeight(Mathf.Lerp(
                    _fadeInStartWeight,
                    1f,
                    progress));
            }

            for (int i = 0; i < _fadeOutNodes.Count; i++)
            {
                var entry = _fadeOutNodes[i];
                entry.Node.SetWeight(Mathf.Lerp(entry.StartWeight, 0f, progress));
            }
        }

        private void Complete()
        {
            if (_fadeInNode != null)
            {
                _fadeInNode.SetWeight(1f);
            }

            for (int i = 0; i < _fadeOutNodes.Count; i++)
            {
                var node = _fadeOutNodes[i].Node;
                node.SetWeight(0f);
                node.Pause();
            }

            _fadeInNode = null;
            _fadeOutNodes.Clear();
            _elapsed = 0f;
            _duration = 0f;
        }

        private readonly struct FadeOutEntry
        {
            public FadeOutEntry(AnimationPlayableNode node, float startWeight)
            {
                Node = node;
                StartWeight = startWeight;
            }

            public AnimationPlayableNode Node { get; }
            public float StartWeight { get; }
        }
    }
}
