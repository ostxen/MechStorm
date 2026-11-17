using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 将节点树输出到 Animator，统一调度采样前更新、Graph 采样、采样后更新和释放。
    /// </summary>
    public sealed class PlayableAnimationGraph : IDisposable
    {
        private AnimationPlayableOutput _output;
        private AnimationPlayableNode _root;
        private bool _isDisposed;
        private readonly DirectorUpdateMode _updateMode;

        public PlayableAnimationGraph(
            Animator animator,
            string graphName = null,
            DirectorUpdateMode updateMode = DirectorUpdateMode.Manual)
        {
            Animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));

            GraphName = string.IsNullOrEmpty(graphName)
                ? animator.name + ".SimplePlayable"
                : graphName;
            _updateMode = updateMode;
            Graph = PlayableGraph.Create(GraphName);
            Graph.SetTimeUpdateMode(updateMode);
            _output = AnimationPlayableOutput.Create(Graph, "Animation", animator);
        }

        public Animator Animator { get; }

        public PlayableGraph Graph { get; }

        public string GraphName { get; }

        public DirectorUpdateMode UpdateMode => _updateMode;

        public bool IsDisposed => _isDisposed;

        public bool IsPlaying => Graph.IsValid() && Graph.IsPlaying();

        public AnimationPlayableNode Root => _root;

        public void SetRoot(AnimationPlayableNode root)
        {
            ThrowIfDisposed();
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (ReferenceEquals(_root, root))
            {
                return;
            }

            _root?.Dispose();
            _output.SetSourcePlayable(root.Output);
            _root = root;
        }

        public void Play()
        {
            ThrowIfDisposed();
            Graph.Play();
            _root?.Play();
        }

        public void Pause()
        {
            if (_isDisposed)
            {
                return;
            }

            _root?.Pause();
            Graph.Stop();
        }

        public void Stop()
        {
            if (_isDisposed)
            {
                return;
            }

            _root?.Stop();
            Graph.Stop();
        }

        public void Evaluate(float deltaTime)
        {
            ThrowIfDisposed();
            if (!Graph.IsPlaying())
            {
                return;
            }

            _root?.Update(deltaTime);
            Graph.Evaluate(Mathf.Max(0f, deltaTime));
            _root?.PostUpdate(deltaTime);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _root?.Dispose();
            _root = null;

            if (Graph.IsValid())
            {
                Graph.Destroy();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(PlayableAnimationGraph));
            }
        }
    }
}
