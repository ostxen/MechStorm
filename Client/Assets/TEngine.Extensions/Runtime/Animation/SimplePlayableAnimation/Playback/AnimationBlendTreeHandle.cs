using System;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>整棵树的状态句柄。树没有唯一的 Clip 时间、长度或完成事件。</summary>
    public sealed class AnimationBlendTreeHandle
    {
        internal AnimationBlendTreeHandle(string key, int layer, AnimationBlendTree node)
        {
            Key = key;
            Layer = layer;
            Node = node;
        }

        internal AnimationBlendTree Node { get; }
        public string Key { get; }
        public int Layer { get; }
        public bool IsValid => !Node.IsDisposed;
        public bool IsPlaying => IsValid && Node.IsActive;
        public AnimationPlaybackState PlaybackState =>
            IsValid ? Node.PlaybackState : AnimationPlaybackState.Stopped;
        public float Weight => Root.Weight;

        /// <summary>可通过 ChildNodes 访问嵌套树并设置参数；不要手动断开或释放 Controller 拥有的节点。</summary>
        public AnimationBlendTree Root => IsValid
            ? Node : throw new ObjectDisposedException(nameof(AnimationBlendTreeHandle));

        public void SetParameter(float parameter)
        {
            if (!(Root is AnimationBlendTree1D tree))
                throw new InvalidOperationException("一维参数只能用于 1D 树。");
            tree.Parameter = parameter;
        }

        public void SetParameter(Vector2 parameter)
        {
            if (!(Root is AnimationBlendTree2D tree))
                throw new InvalidOperationException("二维参数只能用于 2D 树。");
            tree.Parameter = parameter;
        }
    }
}
