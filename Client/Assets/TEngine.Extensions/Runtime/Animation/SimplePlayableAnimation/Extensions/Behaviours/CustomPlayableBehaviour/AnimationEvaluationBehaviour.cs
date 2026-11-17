using System;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 在 Graph.Evaluate 时将 FrameData 转发给已登记的运行时回调。
    /// </summary>
    public sealed class AnimationEvaluationBehaviour : PlayableBehaviour
    {
        public Action<FrameData> OnPrepareFrame;

        public override void PrepareFrame(Playable playable, FrameData info)
        {
            OnPrepareFrame?.Invoke(info);
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            OnPrepareFrame = null;
        }
    }
}
