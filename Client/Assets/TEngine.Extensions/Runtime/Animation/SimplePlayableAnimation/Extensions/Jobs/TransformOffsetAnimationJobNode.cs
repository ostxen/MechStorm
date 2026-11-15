using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 创建 TransformOffsetAnimationJob 的节点，并负责 NativeArray 释放。
    /// </summary>
    public sealed class TransformOffsetAnimationJobNode
        : AnimationJobNode<TransformOffsetAnimationJob>
    {
        private NativeArray<TransformStreamHandle> _handles;
        private NativeArray<Vector3> _offsets;

        public TransformOffsetAnimationJobNode(
            PlayableGraph graph,
            Animator animator,
            Transform[] targets,
            Vector3[] offsets)
            : base(
                graph,
                CreateJob(graph, animator, targets, offsets, out var handles, out var nativeOffsets))
        {
            _handles = handles;
            _offsets = nativeOffsets;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_handles.IsCreated)
            {
                _handles.Dispose();
            }

            if (_offsets.IsCreated)
            {
                _offsets.Dispose();
            }
        }

        private static TransformOffsetAnimationJob CreateJob(
            PlayableGraph graph,
            Animator animator,
            Transform[] targets,
            Vector3[] offsets,
            out NativeArray<TransformStreamHandle> handles,
            out NativeArray<Vector3> nativeOffsets)
        {
            if (animator == null)
            {
                throw new ArgumentNullException(nameof(animator));
            }

            if (targets == null || offsets == null || targets.Length != offsets.Length)
            {
                throw new ArgumentException("目标骨骼和偏移数量必须一致。");
            }

            handles = new NativeArray<TransformStreamHandle>(
                targets.Length,
                Allocator.Persistent);
            nativeOffsets = new NativeArray<Vector3>(
                offsets,
                Allocator.Persistent);

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    handles[i] = default;
                    continue;
                }

                handles[i] = animator.BindStreamTransform(targets[i]);
            }

            return new TransformOffsetAnimationJob
            {
                Handles = handles,
                Offsets = nativeOffsets
            };
        }
    }
}
