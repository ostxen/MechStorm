using System;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 将 Animation Job 接入动画节点树，提供输入连接和 Job 数据访问。
    /// </summary>
    public class AnimationJobNode<TJob> : AnimationPlayableNode
        where TJob : struct, IAnimationJob
    {
        private readonly AnimationScriptPlayable _scriptPlayable;

        protected AnimationJobNode(PlayableGraph graph, TJob job, int inputCount = 0)
            : base(graph)
        {
            _scriptPlayable = AnimationScriptPlayable.Create(
                graph,
                job,
                inputCount);
            Output = _scriptPlayable;
        }

        /// <summary>
        /// 将一个动画节点接入 Job 的输入端口。
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
