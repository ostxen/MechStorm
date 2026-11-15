using System;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>参数驱动的混合树。拥有全部子节点；树内权重由参数决定，树间切换由父 Mixer 执行。</summary>
    public abstract class AnimationBlendTree : AnimationMixerNode
    {
        private readonly int _sampleCount;

        protected AnimationBlendTree(PlayableGraph graph, int sampleCount) : base(graph, sampleCount)
        {
            _sampleCount = sampleCount;
        }

        protected abstract void RecalculateWeights();

        public sealed override int AddChild(AnimationPlayableNode child, float initialWeight = 0f)
        {
            ThrowIfDisposed();
            if (ChildNodes.Count >= _sampleCount)
                throw new InvalidOperationException("子节点不能多于样本。");
            var index = base.AddChild(child, initialWeight);
            RecalculateWeights();
            return index;
        }

        public sealed override void Play()
        {
            ThrowIfDisposed();
            if (ChildNodes.Count != _sampleCount)
                throw new InvalidOperationException("播放前必须为每个样本添加一个子节点。");
            RecalculateWeights();
            base.Play();
        }

        public sealed override void CrossFade(int index, float duration = 0.15f)
        {
            throw new NotSupportedException("树内权重由参数控制；请在父 Mixer 或 Controller 上切换整棵树。");
        }
    }
}
