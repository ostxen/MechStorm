using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 将多个子节点混合为一个输出，支持手动权重和目标淡入、其余淡出的切换。
    /// </summary>
    public class AnimationMixerNode : AnimationPlayableNode
    {
        private readonly AnimationFadeGroup _fadeGroup = new AnimationFadeGroup();
        private readonly List<AnimationPlayableNode> _children = new List<AnimationPlayableNode>();
        private AnimationMixerPlayable _mixer;

        public AnimationMixerNode(PlayableGraph graph, int inputCapacity = 0)
            : base(graph)
        {
            _mixer = AnimationMixerPlayable.Create(graph, Mathf.Max(0, inputCapacity));
            Output = _mixer;
        }

        public IReadOnlyList<AnimationPlayableNode> ChildNodes => _children;

        public AnimationPlayableNode CurrentNode { get; private set; }

        public virtual int AddChild(AnimationPlayableNode child, float initialWeight = 0f)
        {
            ThrowIfDisposed();
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            var index = _children.Count;
            _mixer.SetInputCount(index + 1);
            child.ConnectTo(_mixer, index, initialWeight);
            AddChildReference(child);
            _children.Add(child);
            return index;
        }

        public virtual void CrossFade(int index, float duration = 0.15f)
        {
            ThrowIfDisposed();
            ValidateIndex(index);

            var target = _children[index];
            var fadeOutNodes = new List<AnimationPlayableNode>(_children.Count);
            for (int i = 0; i < _children.Count; i++)
            {
                if (i != index && _children[i].Weight > 0f)
                {
                    fadeOutNodes.Add(_children[i]);
                }
            }

            CurrentNode = target;
            _fadeGroup.Start(target, fadeOutNodes, duration);
            base.Play();
        }

        public void SetChildWeight(int index, float weight)
        {
            ValidateIndex(index);
            _children[index].SetWeight(weight);
        }

        public float GetChildWeight(int index)
        {
            ValidateIndex(index);
            return _children[index].Weight;
        }

        public void ClearChildren()
        {
            ThrowIfDisposed();
            _fadeGroup.Cancel();
            CurrentNode = null;

            for (int i = _children.Count - 1; i >= 0; i--)
            {
                _children[i].Dispose();
            }

            _children.Clear();
            Children.Clear();
            var mixer = (AnimationMixerPlayable)Output;
            mixer.SetInputCount(0);
        }

        public override void Play()
        {
            base.Play();
            for (int i = 0; i < _children.Count; i++)
            {
                if ((_children[i].Weight > 0f || (_fadeGroup.IsActive && _children[i] == CurrentNode)) &&
                    _children[i].PlaybackState != AnimationPlaybackState.Completed)
                {
                    _children[i].Play();
                }
            }
        }

        public override void Pause()
        {
            if (IsDisposed) return;
            base.Pause();
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].Pause();
            }
        }

        public override void Stop()
        {
            if (IsDisposed) return;
            _fadeGroup.Cancel();
            base.Stop();
            CurrentNode = null;
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].SetWeight(0f);
                _children[i].Stop();
            }
        }

        public override void Update(float deltaTime)
        {
            if (IsDisposed) return;
            if (IsActive) _fadeGroup.Update(deltaTime);
            base.Update(deltaTime);
        }

        public override void Reset()
        {
            if (IsDisposed) return;
            Stop();
            foreach (var child in _children) child.Reset();
            base.Reset();
        }

        protected void SetAllChildWeightsToZero()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].SetWeight(0f);
                if (IsActive) _children[i].Pause();
            }
        }

        protected void ApplyNormalizedWeights(IList<float> weights)
        {
            if (weights == null || weights.Count < _children.Count)
            {
                throw new ArgumentException("权重数量不能少于子节点数量。", nameof(weights));
            }

            var totalWeight = 0d;
            for (int i = 0; i < _children.Count; i++)
            {
                BlendParameterValidation.Check(weights[i]);
                totalWeight += Mathf.Max(0f, weights[i]);
            }

            if (totalWeight <= Mathf.Epsilon)
            {
                SetAllChildWeightsToZero();
                return;
            }

            var inverseTotalWeight = 1d / totalWeight;
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].SetWeight((float)(Mathf.Max(0f, weights[i]) * inverseTotalWeight));
                if (IsActive)
                {
                    if (_children[i].Weight > 0f && !_children[i].IsActive &&
                        _children[i].PlaybackState != AnimationPlaybackState.Completed)
                        _children[i].Play();
                    else if (_children[i].Weight == 0f)
                        _children[i].Pause();
                }
            }
        }

        protected void ValidateIndex(int index)
        {
            if (index < 0 || index >= _children.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
