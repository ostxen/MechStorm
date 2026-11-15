using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 播放单个 AnimationClip，提供时间和速度控制，并在采样后检查完成状态与时间轴事件。
    /// </summary>
    public sealed class AnimationClipNode : AnimationPlayableNode
    {
        private readonly AnimationClipPlayable _clipPlayable;
        private float _previousTime;
        private bool _preserveTimeOnNextPlay;

        public AnimationClipNode(
            PlayableGraph graph,
            AnimationClip clip,
            WrapMode wrapMode = WrapMode.Loop,
            bool restartOnPlay = true)
            : base(graph)
        {
            Clip = clip != null
                ? clip
                : throw new ArgumentNullException(nameof(clip));

            ValidateWrapMode(wrapMode);
            WrapMode = wrapMode;
            RestartOnPlay = restartOnPlay;
            _clipPlayable = AnimationClipPlayable.Create(graph, clip);
            _clipPlayable.SetApplyFootIK(false);
            _clipPlayable.SetApplyPlayableIK(false);

            if (wrapMode == WrapMode.Once)
            {
                _clipPlayable.SetDuration(clip.length);
            }

            Output = _clipPlayable;
            Output.Pause();
            _previousTime = 0f;
        }

        public AnimationClip Clip { get; }

        public WrapMode WrapMode { get; private set; }

        public bool RestartOnPlay { get; set; }

        public float Length => Clip.length;

        public void ConfigurePlayback(
            WrapMode wrapMode,
            bool restartOnPlay)
        {
            ValidateWrapMode(wrapMode);
            WrapMode = wrapMode;
            RestartOnPlay = restartOnPlay;
            _clipPlayable.SetDuration(
                wrapMode == WrapMode.Once
                    ? Clip.length
                    : double.PositiveInfinity);
        }

        public float Time
        {
            get => (float)Output.GetTime();
            set
            {
                ThrowIfDisposed();
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(nameof(value));
                var time = Mathf.Max(0f, value);
                Output.SetTime(time);
                Output.SetDone(false);
                _previousTime = time;
            }
        }

        public float NormalizedTime
        {
            get => Length <= 0f ? 0f : Time / Length;
            set => Time = Length * Mathf.Max(0f, value);
        }

        public float Speed
        {
            get => (float)Output.GetSpeed();
            set
            {
                if (float.IsNaN(value) ||
                    float.IsInfinity(value) ||
                    value < 0f)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "当前动画节点只支持有限的非负播放速度。");
                }

                Output.SetSpeed(value);
            }
        }

        public bool IsComplete =>
            !IsDisposed &&
            WrapMode == WrapMode.Once &&
            Length > 0f &&
            Time >= Length;

        public AnimationEventDispatcher EventDispatcher { get; } =
            new AnimationEventDispatcher();

        public AnimationEventQueue EventQueue { get; set; }

        /// <summary>
        /// 稳定的业务动画标识；用于状态查找、事件和调试。
        /// </summary>
        public string AnimationKey { get; set; }

        internal AnimationStateHandle StateHandle { get; set; }
        internal int PlayVersion { get; private set; }

        public static bool IsSupportedWrapMode(WrapMode mode) =>
            mode == WrapMode.Once || mode == WrapMode.Loop;

        private static void ValidateWrapMode(WrapMode mode)
        {
            if (!IsSupportedWrapMode(mode))
                throw new ArgumentOutOfRangeException(nameof(mode), "仅支持 Once 和 Loop。");
        }

        public void PlayFromStart()
        {
            ThrowIfDisposed();
            PlayVersion++;
            _preserveTimeOnNextPlay = false;
            Time = 0f;
            _previousTime = 0f;
            base.Play();
        }

        internal void PrepareForPlay(bool setTime, float time)
        {
            if (!setTime)
            {
                _preserveTimeOnNextPlay = false;
                return;
            }

            Time = time;
            PlayVersion++;
            _previousTime = time;
            _preserveTimeOnNextPlay = true;
        }

        public override void Play()
        {
            ThrowIfDisposed();
            if (PlaybackState == AnimationPlaybackState.Completed &&
                !RestartOnPlay && !_preserveTimeOnNextPlay)
                return;
            if (_preserveTimeOnNextPlay)
            {
                _preserveTimeOnNextPlay = false;
            }
            else if (RestartOnPlay && PlaybackState != AnimationPlaybackState.Paused && !IsActive)
            {
                PlayVersion++;
                Time = 0f;
                _previousTime = 0f;
            }

            base.Play();
        }

        public override void Pause()
        {
            if (IsDisposed || !IsActive || PlaybackState == AnimationPlaybackState.Completed) return;
            base.Pause();
        }

        public override void Stop()
        {
            if (IsDisposed) return;
            PlayVersion++;
            base.Stop();
            _previousTime = 0f;
            _preserveTimeOnNextPlay = false;
            PlaybackState = AnimationPlaybackState.Stopped;
        }

        public override void Reset()
        {
            if (IsDisposed) return;
            PlayVersion++;
            base.Reset();
            _previousTime = 0f;
            _preserveTimeOnNextPlay = false;
        }

        public override void PostUpdate(float deltaTime)
        {
            if (IsDisposed || !IsActive) return;
            var currentTime = Time;
            if (EventQueue != null)
            {
                EventDispatcher.Dispatch(
                    _previousTime,
                    currentTime,
                    Length,
                    WrapMode != WrapMode.Once,
                    marker => EventQueue.Enqueue(AnimationKey, marker));
            }

            _previousTime = currentTime;
            if (IsComplete && IsActive)
            {
                Output.Pause();
                IsActive = false;
                PlaybackState = AnimationPlaybackState.Completed;
                if (StateHandle != null)
                    EventQueue?.EnqueueCompletion(StateHandle, PlayVersion);
            }

            base.PostUpdate(deltaTime);
        }
    }
}
