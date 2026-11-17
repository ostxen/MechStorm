using System;
using UnityEngine;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>可复用的代码配置。每次注册都创建独立的节点树，不持有资源 Lease。</summary>
    public abstract class AnimationBlendTreeDefinition
    {
        internal abstract AnimationBlendTree CreateNode(PlayableGraph graph, Func<string, AnimationClip> resolveClip);
    }

    internal sealed class AnimationBlendTreeMotion
    {
        private readonly string _clipKey;
        private readonly AnimationBlendTreeDefinition _tree;

        public AnimationBlendTreeMotion(string clipKey)
        {
            if (string.IsNullOrWhiteSpace(clipKey))
                throw new ArgumentException("样本 Clip Key 不能为空。", nameof(clipKey));
            _clipKey = clipKey;
        }

        public AnimationBlendTreeMotion(AnimationBlendTreeDefinition tree)
        {
            _tree = tree ?? throw new ArgumentNullException(nameof(tree));
        }

        public AnimationPlayableNode CreateNode(PlayableGraph graph, Func<string, AnimationClip> resolveClip)
        {
            if (_tree != null) return _tree.CreateNode(graph, resolveClip);
            // 树是持续混合状态，叶子独立循环，不复用 Controller 中已连接的 Clip 节点。
            return new AnimationClipNode(graph, resolveClip(_clipKey), WrapMode.Loop, false)
            {
                AnimationKey = _clipKey
            };
        }
    }
}
