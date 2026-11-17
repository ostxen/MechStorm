using System;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>初始化时应用的层配置；未配置时 Base 权重为 1，上层为 0。</summary>
    [Serializable]
    public sealed class AnimationLayerSettings
    {
        [Range(0f, 1f)] public float Weight;
        public AvatarMask Mask;
        public bool Additive;
    }
}
