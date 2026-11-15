using System;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 将 PlayableBehaviour 接入节点树，在 Graph 采样期间驱动自定义回调。
    /// </summary>
    public abstract class AnimationPlayableBehaviourNode<TBehaviour>
        : AnimationPlayableNode
        where TBehaviour : PlayableBehaviour, new()
    {
        private readonly ScriptPlayable<TBehaviour> _scriptPlayable;

        protected AnimationPlayableBehaviourNode(
            PlayableGraph graph,
            int inputCount = 0)
            : base(graph)
        {
            _scriptPlayable = ScriptPlayable<TBehaviour>.Create(
                graph,
                inputCount);
            Output = _scriptPlayable;
        }

        public TBehaviour Behaviour => _scriptPlayable.GetBehaviour();

        /// <summary>
        /// 将普通动画节点接入行为节点的输入端口。
        /// </summary>
        public int AddInput(AnimationPlayableNode input, float weight = 1f)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var inputPort = _scriptPlayable.GetInputCount();
            _scriptPlayable.SetInputCount(inputPort + 1);
            input.ConnectTo(_scriptPlayable, inputPort, weight);
            AddChildReference(input);
            return inputPort;
        }
    }
}
