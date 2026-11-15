using System;
using System.Collections.Generic;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 按加入顺序播放节点，可显式推进或在当前项完成后的下一次采样前自动推进。
    /// </summary>
    public sealed class AnimationSequenceNode : AnimationPlayableNode
    {
        private readonly List<AnimationPlayableNode> _entries = new List<AnimationPlayableNode>();
        private int _currentIndex = -1;

        public AnimationSequenceNode(PlayableGraph graph)
            : base(graph)
        {
            var mixer = UnityEngine.Animations.AnimationMixerPlayable.Create(graph, 0);
            Output = mixer;
        }

        public int CurrentIndex => _currentIndex;

        /// <summary>在当前项 Completed 后，于下一次采样前推进；Loop 项不会自动结束。</summary>
        public bool AutoAdvanceOnComplete { get; set; }

        public int AddEntry(AnimationPlayableNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            var mixer = (UnityEngine.Animations.AnimationMixerPlayable)Output;
            var index = _entries.Count;
            mixer.SetInputCount(index + 1);
            node.ConnectTo(mixer, index, 0f);
            AddChildReference(node);
            _entries.Add(node);
            return index;
        }

        public void PlayEntry(int index)
        {
            ThrowIfDisposed();
            if (index < 0 || index >= _entries.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].SetWeight(i == index ? 1f : 0f);
                if (i == index)
                {
                    _entries[i].Stop();
                    _entries[i].Play();
                }
                else
                {
                    _entries[i].Stop();
                }
            }

            _currentIndex = index;
            base.Play();
        }

        public override void Play()
        {
            ThrowIfDisposed();
            if (PlaybackState == AnimationPlaybackState.Completed) return;
            if (_currentIndex < 0)
            {
                if (_entries.Count > 0) PlayEntry(0);
                return;
            }
            base.Play();
            _entries[_currentIndex].Play();
        }

        public override void Pause()
        {
            if (IsDisposed) return;
            if (_currentIndex >= 0) _entries[_currentIndex].Pause();
            base.Pause();
        }

        public override void Update(float deltaTime)
        {
            if (IsDisposed || !IsActive) return;
            if (AutoAdvanceOnComplete && _currentIndex >= 0 &&
                _entries[_currentIndex].PlaybackState == AnimationPlaybackState.Completed)
            {
                if (_currentIndex + 1 < _entries.Count)
                    PlayEntry(_currentIndex + 1);
                else
                {
                    base.Pause();
                    PlaybackState = AnimationPlaybackState.Completed;
                }
            }
            base.Update(deltaTime);
        }

        public override void Reset()
        {
            if (IsDisposed) return;
            Stop();
            foreach (var entry in _entries) entry.Reset();
            base.Reset();
        }

        public bool MoveNext()
        {
            if (_entries.Count == 0 || _currentIndex + 1 >= _entries.Count)
            {
                Stop();
                return false;
            }

            PlayEntry(_currentIndex + 1);
            return true;
        }

        public override void Stop()
        {
            if (IsDisposed) return;
            base.Stop();
            _currentIndex = -1;
            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].SetWeight(0f);
                _entries[i].Stop();
            }
        }
    }
}
