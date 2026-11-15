using System;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 提供已绑定动画的时间、速度、权重和完成状态访问入口；有效期随所属 Controller 的绑定。
    /// </summary>
    public sealed class AnimationStateHandle
    {
        internal AnimationStateHandle(string key, int layer, AnimationClipNode node)
        {
            Key = key;
            Layer = layer;
            Node = node;
        }

        internal AnimationClipNode Node { get; }

        public string Key { get; }

        public int Layer { get; }

        public bool IsValid => Node != null && !Node.IsDisposed;

        private AnimationClipNode ValidNode => IsValid
            ? Node
            : throw new ObjectDisposedException(nameof(AnimationStateHandle), "动画绑定已失效。");

        /// <summary>为本次绑定登记标记；重新绑定后需重新登记。</summary>
        public void AddMarker(AnimationEventMarker marker) => ValidNode.EventDispatcher.AddMarker(marker);

        public void ClearMarkers() => ValidNode.EventDispatcher.Clear();

        public float Length => ValidNode.Length;

        public float Time
        {
            get => ValidNode.Time;
            set => ValidNode.Time = value;
        }

        public float NormalizedTime
        {
            get => ValidNode.NormalizedTime;
            set => ValidNode.NormalizedTime = value;
        }

        public float Speed
        {
            get => ValidNode.Speed;
            set => ValidNode.Speed = value;
        }

        public float Weight => ValidNode.Weight;

        public bool IsPlaying => IsValid && Node.IsPlaying;

        public bool IsComplete => IsValid && Node.IsComplete;

        public AnimationPlaybackState PlaybackState =>
            !IsValid
                ? AnimationPlaybackState.Stopped
                : Node.PlaybackState;
    }
}
