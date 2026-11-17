using System;
using System.Collections.Generic;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 管理候选子节点，根据派生类返回的索引选择目标，并通过淡入淡出切换输出。
    /// </summary>
    public abstract class AnimationSelectorNode : AnimationPlayableNode
    {
        private readonly AnimationFadeGroup _fadeGroup =
            new AnimationFadeGroup();

        protected AnimationSelectorNode(PlayableGraph graph, int inputCapacity = 0)
            : base(graph)
        {
            _outputMixer = UnityEngine.Animations.AnimationMixerPlayable.Create(
                graph,
                Math.Max(0, inputCapacity));
            Output = _outputMixer;
        }

        private readonly UnityEngine.Animations.AnimationMixerPlayable _outputMixer;

        public int SelectedIndex { get; protected set; } = -1;

        public float SelectionFadeDuration { get; set; } = 0.15f;

        public int AddChild(AnimationPlayableNode child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            var index = Children.Count;
            _outputMixer.SetInputCount(index + 1);
            child.ConnectTo(_outputMixer, index, 0f);
            AddChildReference(child);
            return index;
        }

        public void SelectNext(float fadeDuration = -1f)
        {
            if (Children.Count == 0)
            {
                SelectedIndex = -1;
                return;
            }

            var index = SelectIndex();
            if (index >= 0)
            {
                Select(index, fadeDuration);
            }
        }

        public void Select(int index, float fadeDuration = -1f)
        {
            if (index < 0 || index >= Children.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var fadeOutNodes = new List<AnimationPlayableNode>(
                Children.Count);
            for (int i = 0; i < Children.Count; i++)
            {
                if (i != index && Children[i].Weight > 0f)
                {
                    fadeOutNodes.Add(Children[i]);
                }
            }

            var duration = fadeDuration < 0f
                ? SelectionFadeDuration
                : fadeDuration;
            _fadeGroup.Start(Children[index], fadeOutNodes, duration);
            SelectedIndex = index;
            IsActive = true;
            PlaybackState = AnimationPlaybackState.Playing;
        }

        public override void Update(float deltaTime)
        {
            _fadeGroup.Update(deltaTime);
            base.Update(deltaTime);
        }

        public override void Stop()
        {
            _fadeGroup.Cancel();
            base.Stop();
            SelectedIndex = -1;
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].SetWeight(0f);
                Children[i].Stop();
            }
        }

        public override void Reset()
        {
            _fadeGroup.Cancel();
            base.Reset();
            SelectedIndex = -1;
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Reset();
                Children[i].SetWeight(0f);
            }
        }

        public override void Dispose()
        {
            _fadeGroup.Cancel();
            base.Dispose();
        }

        protected abstract int SelectIndex();
    }
}
