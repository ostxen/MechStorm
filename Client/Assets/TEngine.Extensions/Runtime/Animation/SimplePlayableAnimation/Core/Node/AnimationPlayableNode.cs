using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 管理 Playable 的父子连接、输入权重和播放状态，并在释放时递归释放子节点。
    /// </summary>
    public abstract class AnimationPlayableNode : IDisposable
    {
        private Playable _parent;
        private int _parentInputPort = -1;
        private bool _isDisposed;
        private float _weight;

        protected AnimationPlayableNode(PlayableGraph graph)
        {
            if (!graph.IsValid())
            {
                throw new ArgumentException("PlayableGraph 无效。", nameof(graph));
            }

            Graph = graph;
            Children = new List<AnimationPlayableNode>();
        }

        /// <summary>
        /// 此节点所属的 Graph。
        /// </summary>
        protected PlayableGraph Graph { get; }

        /// <summary>
        /// 此节点输出的 Playable。
        /// </summary>
        public Playable Output { get; protected set; }

        /// <summary>
        /// 子节点。复合节点使用此列表维护所有权。
        /// </summary>
        protected List<AnimationPlayableNode> Children { get; }

        public bool IsDisposed => _isDisposed;

        public bool IsConnected => _parent.IsValid() && _parentInputPort >= 0;

        public int InputPort => _parentInputPort;

        public bool IsPlaying =>
            Output.IsValid() &&
            Output.GetPlayState() == PlayState.Playing;

        public bool IsDone => Output.IsValid() && Output.IsDone();

        public bool IsActive { get; protected set; }

        public float Weight => _weight;

        public AnimationPlaybackState PlaybackState { get; protected set; } = AnimationPlaybackState.Stopped;

        /// <summary>
        /// 将节点连接到父 Playable 的指定输入端口。
        /// </summary>
        public void ConnectTo(Playable parent, int inputPort, float weight = 0f)
        {
            ThrowIfDisposed();

            if (!parent.IsValid())
            {
                throw new ArgumentException("父 Playable 无效。", nameof(parent));
            }

            if (inputPort < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputPort));
            }

            if (IsConnected)
            {
                throw new InvalidOperationException("节点已经连接到父 Playable。");
            }

            Graph.Connect(Output, 0, parent, inputPort);
            _parent = parent;
            _parentInputPort = inputPort;
            SetWeight(weight);
        }

        public void Disconnect()
        {
            if (!IsConnected)
            {
                return;
            }

            _parent.DisconnectInput(_parentInputPort);
            _parent = Playable.Null;
            _parentInputPort = -1;
        }

        public void SetWeight(float weight)
        {
            _weight = Mathf.Clamp01(weight);
            if (IsConnected)
            {
                _parent.SetInputWeight(_parentInputPort, _weight);
            }
        }

        public virtual void Play()
        {
            ThrowIfDisposed();
            Output.Play();
            IsActive = true;
            PlaybackState = AnimationPlaybackState.Playing;
        }

        public virtual void Pause()
        {
            if (IsDisposed)
            {
                return;
            }

            Output.Pause();
            IsActive = false;
            PlaybackState = AnimationPlaybackState.Paused;
        }

        public virtual void Stop()
        {
            if (IsDisposed)
            {
                return;
            }

            Output.Pause();
            Output.SetTime(0d);
            IsActive = false;
            PlaybackState = AnimationPlaybackState.Stopped;
        }

        public virtual void Reset()
        {
            if (IsDisposed)
            {
                return;
            }

            Output.Pause();
            Output.SetTime(0d);
            Output.SetSpeed(1d);
            SetWeight(0f);
            IsActive = false;
            PlaybackState = AnimationPlaybackState.Stopped;
        }

        /// <summary>
        /// 在 Graph.Evaluate 前更新淡入淡出和参数权重。
        /// </summary>
        public virtual void Update(float deltaTime)
        {
            if (IsDisposed)
            {
                return;
            }

            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Update(deltaTime);
            }
        }

        /// <summary>
        /// 在 Graph.Evaluate 后更新完成状态并收集边界事件。
        /// </summary>
        public virtual void PostUpdate(float deltaTime)
        {
            if (IsDisposed)
            {
                return;
            }

            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].PostUpdate(deltaTime);
            }
        }

        public virtual void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Disconnect();

            for (int i = Children.Count - 1; i >= 0; i--)
            {
                Children[i].Dispose();
            }

            Children.Clear();

            if (Output.IsValid() && Graph.IsValid())
            {
                Graph.DestroyPlayable(Output);
            }

            Output = Playable.Null;
            PlaybackState = AnimationPlaybackState.Stopped;
            IsActive = false;
        }

        protected void AddChildReference(AnimationPlayableNode child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            if (!child.Graph.Equals(Graph))
            {
                throw new InvalidOperationException("父子节点必须属于同一个 PlayableGraph。");
            }

            Children.Add(child);
        }

        protected void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
