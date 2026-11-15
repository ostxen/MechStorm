using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 在动画流中为指定骨骼叠加局部位置偏移。
    /// </summary>
    public struct TransformOffsetAnimationJob : IAnimationJob
    {
        public NativeArray<TransformStreamHandle> Handles;
        public NativeArray<Vector3> Offsets;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isValid || !Handles.IsCreated || !Offsets.IsCreated)
            {
                return;
            }

            var count = Mathf.Min(Handles.Length, Offsets.Length);
            for (int i = 0; i < count; i++)
            {
                var handle = Handles[i];
                if (!handle.IsValid(stream))
                {
                    continue;
                }

                var localPosition = handle.GetLocalPosition(stream);
                handle.SetLocalPosition(stream, localPosition + Offsets[i]);
            }
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
        }
    }
}
