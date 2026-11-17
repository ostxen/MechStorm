using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 叠加多个动画层，通过层权重、AvatarMask 和 Additive 设置控制各层的姿势贡献。
    /// </summary>
    public sealed class AnimationLayerMixerNode : AnimationPlayableNode
    {
        private readonly AnimationLayerMixerPlayable _layerMixer;
        private readonly List<AnimationMixerNode> _layers =
            new List<AnimationMixerNode>();

        public AnimationLayerMixerNode(PlayableGraph graph, int layerCapacity = 1)
            : base(graph)
        {
            _layerMixer = AnimationLayerMixerPlayable.Create(
                graph,
                Mathf.Max(1, layerCapacity));
            Output = _layerMixer;
            _layerMixer.SetInputWeight(0, 1f);
        }

        public IReadOnlyList<AnimationMixerNode> Layers => _layers;

        public int AddLayer(
            AnimationMixerNode layer,
            float weight = 1f,
            AvatarMask mask = null,
            bool additive = false)
        {
            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            var index = _layers.Count;
            _layerMixer.SetInputCount(index + 1);
            layer.ConnectTo(_layerMixer, index, weight);
            if (mask != null)
            {
                _layerMixer.SetLayerMaskFromAvatarMask((uint)index, mask);
            }

            _layerMixer.SetLayerAdditive((uint)index, additive);
            AddChildReference(layer);
            _layers.Add(layer);
            return index;
        }

        public void SetLayerWeight(int index, float weight)
        {
            ValidateLayer(index);
            if (float.IsNaN(weight) || float.IsInfinity(weight))
                throw new ArgumentOutOfRangeException(nameof(weight));
            _layers[index].SetWeight(weight);
        }

        public float GetLayerWeight(int index)
        {
            ValidateLayer(index);
            return _layerMixer.GetInputWeight(index);
        }

        public void SetLayerAdditive(int index, bool additive)
        {
            ValidateLayer(index);
            _layerMixer.SetLayerAdditive((uint)index, additive);
        }

        public void SetLayerMask(int index, AvatarMask mask)
        {
            ValidateLayer(index);
            if (mask == null)
                throw new ArgumentNullException(nameof(mask));
            _layerMixer.SetLayerMaskFromAvatarMask((uint)index, mask);
        }

        public override void Play()
        {
            base.Play();
            foreach (var layer in _layers)
                if (!layer.IsActive) layer.Play();
        }

        public override void Pause()
        {
            if (IsDisposed) return;
            foreach (var layer in _layers) layer.Pause();
            base.Pause();
        }

        public override void Stop()
        {
            if (IsDisposed) return;
            foreach (var layer in _layers) layer.Stop();
            base.Stop();
        }

        public override void Reset()
        {
            if (IsDisposed) return;
            foreach (var layer in _layers) layer.Reset();
            base.Reset();
        }

        private void ValidateLayer(int index)
        {
            ThrowIfDisposed();
            if (index < 0 || index >= _layers.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
