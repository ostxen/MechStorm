using System;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 保存已加载 Clip 及其释放回调，Release 或 Dispose 时执行一次资源归还。
    /// </summary>
    public sealed class AnimationClipLease : IDisposable
    {
        private Action _release;
        private bool _isReleased;

        public AnimationClipLease(
            string animationKey,
            AnimationClip clip,
            Action release = null)
        {
            if (string.IsNullOrEmpty(animationKey))
            {
                throw new ArgumentException(
                    "动画 Key 不能为空。",
                    nameof(animationKey));
            }

            AnimationKey = animationKey;
            Clip = clip != null
                ? clip
                : throw new ArgumentNullException(nameof(clip));
            _release = release;
        }

        /// <summary>
        /// 用于状态注册和播放查找的稳定动画标识。
        /// </summary>
        public string AnimationKey { get; }

        public AnimationClip Clip { get; }

        public bool IsReleased => _isReleased;

        public void Release()
        {
            if (_isReleased)
            {
                return;
            }

            _isReleased = true;
            var release = _release;
            _release = null;
            release?.Invoke();
        }

        public void Dispose()
        {
            Release();
        }
    }
}
