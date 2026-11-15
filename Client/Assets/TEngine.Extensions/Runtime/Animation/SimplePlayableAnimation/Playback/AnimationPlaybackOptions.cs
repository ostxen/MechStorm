using System;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 保存动画标识和播放参数，供 Controller 播放已绑定的动画。
    /// </summary>
    [Serializable]
    public sealed class AnimationPlaybackOptions
    {
        [SerializeField]
        private string _animationKey;

        [SerializeField]
        private float _fadeDuration = 0.15f;

        [SerializeField]
        private float _speed = 1f;

        [SerializeField]
        private WrapMode _wrapMode = WrapMode.Loop;

        [SerializeField]
        private bool _restartOnPlay = true;

        [SerializeField]
        private bool _useNormalizedStartTime;

        [SerializeField]
        [Range(0f, 1f)]
        private float _normalizedStartTime;

        public AnimationPlaybackOptions()
        {
        }

        public AnimationPlaybackOptions(string animationKey)
        {
            _animationKey = animationKey;
        }

        /// <summary>要播放的动画标识，对应 Controller 已绑定的状态。</summary>
        public string AnimationKey
        {
            get => _animationKey;
            set => _animationKey = value;
        }

        /// <summary>切换到目标状态的淡入淡出时长，单位为秒。</summary>
        public float FadeDuration
        {
            get => _fadeDuration;
            set => _fadeDuration = Mathf.Max(0f, value);
        }

        /// <summary>应用到目标状态的播放速度倍率。</summary>
        public float Speed
        {
            get => _speed;
            set => _speed = value;
        }

        /// <summary>应用到目标状态的播放模式。</summary>
        public WrapMode WrapMode
        {
            get => _wrapMode;
            set => _wrapMode = value;
        }

        /// <summary>播放请求是否重置时间，同时设置节点再次激活时的重播行为。</summary>
        public bool RestartOnPlay
        {
            get => _restartOnPlay;
            set => _restartOnPlay = value;
        }

        /// <summary>应用配置时是否设置指定的归一化起播位置。</summary>
        public bool UseNormalizedStartTime
        {
            get => _useNormalizedStartTime;
            set => _useNormalizedStartTime = value;
        }

        /// <summary>相对于 Clip 长度的起播位置，0 为起点，1 为终点。</summary>
        public float NormalizedStartTime
        {
            get => _normalizedStartTime;
            set => _normalizedStartTime = Mathf.Clamp01(value);
        }

        /// <summary>检查动画标识及当前启用的数值参数是否满足配置校验。</summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(AnimationKey) &&
            IsFinite(FadeDuration) &&
            FadeDuration >= 0f &&
            IsFinite(Speed) &&
            Speed >= 0f &&
            AnimationClipNode.IsSupportedWrapMode(WrapMode) &&
            (!UseNormalizedStartTime ||
             (IsFinite(NormalizedStartTime) && NormalizedStartTime >= 0f && NormalizedStartTime <= 1f));

        internal void ApplyTo(AnimationClipNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (!IsValid)
            {
                throw new InvalidOperationException(
                    $"AnimationPlaybackOptions 无效：{AnimationKey}");
            }

            node.ConfigurePlayback(WrapMode, RestartOnPlay);
            node.Speed = Speed;

            if (UseNormalizedStartTime)
            {
                node.PrepareForPlay(
                    true,
                    node.Length * NormalizedStartTime);
            }
            else if (RestartOnPlay)
            {
                node.PrepareForPlay(true, 0f);
            }
            else
            {
                node.PrepareForPlay(false, 0f);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
