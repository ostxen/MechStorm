using System;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 将动画输入接入采样回调节点，通过 Behaviour 接收 Graph 评估通知。
    /// </summary>
    public sealed class AnimationEvaluationBehaviourNode
        : AnimationPlayableBehaviourNode<AnimationEvaluationBehaviour>
    {
        public AnimationEvaluationBehaviourNode(
            PlayableGraph graph,
            Action<FrameData> onPrepareFrame = null,
            int inputCount = 0)
            : base(graph, inputCount)
        {
            Behaviour.OnPrepareFrame = onPrepareFrame;
        }

        public void SetPrepareFrameCallback(Action<FrameData> callback)
        {
            Behaviour.OnPrepareFrame = callback;
        }
    }
}
