using System;
using System.Collections.Generic;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 记录采集时的 Graph、状态、事件队列和资源数量，供调试界面与故障记录读取。
    /// </summary>
    public sealed class AnimationSnapshot
    {
        internal AnimationSnapshot(
            string graphName,
            DirectorUpdateMode updateMode,
            bool graphPlaying,
            bool graphDisposed,
            int playableRootCount,
            int stateCount,
            int leaseCount,
            int queuedEventCount,
            IReadOnlyList<AnimationStateSnapshot> states,
            IReadOnlyList<AnimationBlendTreeSnapshot> blendTrees)
        {
            GraphName = graphName;
            UpdateMode = updateMode;
            GraphPlaying = graphPlaying;
            GraphDisposed = graphDisposed;
            PlayableRootCount = playableRootCount;
            StateCount = stateCount;
            LeaseCount = leaseCount;
            QueuedEventCount = queuedEventCount;
            States = states;
            BlendTrees = blendTrees;
            CapturedAtUtcTicks = DateTime.UtcNow.Ticks;
        }

        public string GraphName { get; }

        public DirectorUpdateMode UpdateMode { get; }

        public bool GraphPlaying { get; }

        public bool GraphDisposed { get; }

        public int PlayableRootCount { get; }

        public int StateCount { get; }

        public int LeaseCount { get; }

        public int QueuedEventCount { get; }

        public long CapturedAtUtcTicks { get; }

        public IReadOnlyList<AnimationStateSnapshot> States { get; }

        public IReadOnlyList<AnimationBlendTreeSnapshot> BlendTrees { get; }

        public int BlendTreeCount => BlendTrees.Count;
    }

    public readonly struct AnimationBlendTreeSnapshot
    {
        internal AnimationBlendTreeSnapshot(AnimationBlendTreeHandle tree)
        {
            Key = tree.Key;
            Layer = tree.Layer;
            Weight = tree.Weight;
            PlaybackState = tree.PlaybackState;
        }

        public string Key { get; }
        public int Layer { get; }
        public float Weight { get; }
        public AnimationPlaybackState PlaybackState { get; }
    }

    public readonly struct AnimationStateSnapshot
    {
        internal AnimationStateSnapshot(AnimationStateHandle state)
        {
            Key = state.Key;
            Layer = state.Layer;
            Length = state.Length;
            Time = state.Time;
            NormalizedTime = state.NormalizedTime;
            Speed = state.Speed;
            Weight = state.Weight;
            IsPlaying = state.IsPlaying;
            IsComplete = state.IsComplete;
            PlaybackState = state.PlaybackState;
        }

        public string Key { get; }

        public int Layer { get; }

        public float Length { get; }

        public float Time { get; }

        public float NormalizedTime { get; }

        public float Speed { get; }

        public float Weight { get; }

        public bool IsPlaying { get; }

        public bool IsComplete { get; }

        public AnimationPlaybackState PlaybackState { get; }
    }
}
