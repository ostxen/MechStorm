using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 保存可在 Inspector 配置的播放方案，通过动画标识查找并交给 Controller 播放。
    /// </summary>
    [CreateAssetMenu(
        fileName = "AnimationPlayOptionsSet",
        menuName = "TEngine/Animation/Play Options Set")]
    public sealed class AnimationPlaybackOptionsSet : ScriptableObject
    {
        [SerializeField]
        [FormerlySerializedAs("_transitions")]
        private List<AnimationPlaybackOptions> _requests =
            new List<AnimationPlaybackOptions>();

        public IReadOnlyList<AnimationPlaybackOptions> Requests =>
            _requests;

        /// <summary>按区分大小写的动画标识查找首个匹配方案。</summary>
        public bool TryGet(
            string animationKey,
            out AnimationPlaybackOptions request)
        {
            for (int i = 0; i < _requests.Count; i++)
            {
                var candidate = _requests[i];
                if (candidate != null &&
                    string.Equals(
                        candidate.AnimationKey,
                        animationKey,
                        StringComparison.Ordinal))
                {
                    request = candidate;
                    return true;
                }
            }

            request = null;
            return false;
        }
    }
}
