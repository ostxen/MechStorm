using System;
using System.Collections.Generic;
using TEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 暂存动画标记，在 Graph 采样完成后按入队顺序派发，并捕获每条记录的派发异常。
    /// </summary>
    public sealed class AnimationEventQueue
    {
        private readonly Queue<AnimationEventRecord> _events =
            new Queue<AnimationEventRecord>();
        private readonly Queue<(AnimationStateHandle state, int version)> _completions =
            new Queue<(AnimationStateHandle state, int version)>();

        public event Action<AnimationEventRecord> EventReceived;
        /// <summary>Once 自然结束后派发；Stop、取消和重绑不会产生完成通知。</summary>
        public event Action<AnimationStateHandle> Completed;

        public int Count => _events.Count + _completions.Count;

        internal void EnqueueCompletion(AnimationStateHandle state, int version)
        {
            _completions.Enqueue((state, version));
        }

        internal void Enqueue(string animationKey, AnimationEventMarker marker)
        {
            _events.Enqueue(new AnimationEventRecord(animationKey, marker));
        }

        public void Flush()
        {
            while (_events.Count > 0)
            {
                var record = _events.Dequeue();
                try
                {
                    EventReceived?.Invoke(record);
                }
                catch (Exception exception)
                {
                    Log.Error($"动画事件回调执行失败：{exception}");
                }
            }

            while (_completions.Count > 0)
            {
                var completion = _completions.Dequeue();
                if (!completion.state.IsValid ||
                    completion.state.Node.PlayVersion != completion.version ||
                    !completion.state.IsComplete)
                    continue;
                try
                {
                    Completed?.Invoke(completion.state);
                }
                catch (Exception exception)
                {
                    Log.Error($"动画完成回调执行失败：{exception}");
                }
            }
        }

        public void Clear()
        {
            _events.Clear();
            _completions.Clear();
        }

        /// <summary>对象池交还或销毁时显式清理订阅，Clear 仅清理待派发事件。</summary>
        public void ClearSubscribers()
        {
            EventReceived = null;
            Completed = null;
        }
    }

    public readonly struct AnimationEventRecord
    {
        public AnimationEventRecord(
            string animationKey,
            AnimationEventMarker marker)
        {
            AnimationKey = animationKey;
            Marker = marker;
        }

        public string AnimationKey { get; }

        public AnimationEventMarker Marker { get; }
    }
}
