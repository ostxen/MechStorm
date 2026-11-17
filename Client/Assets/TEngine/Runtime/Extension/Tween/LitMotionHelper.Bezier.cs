using System;
using UnityEngine;

namespace TEngine
{
    public sealed partial class LitMotionHelper
    {
        /// <summary>按控制点数组移动目标。</summary>
        public long MoveBezierPath(Transform target, Vector3[] path, float duration, Ease ease = Ease.Default, int cycles = 1,
            CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false)
        {
            if (target == null || path == null || path.Length == 0) return 0;
            var points = (Vector3[])path.Clone();
            var work = new Vector3[points.Length];
            return Add(target, 0f, 1f, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime,
                t => target.position = EvaluateBezier(points, work, t));
        }

        /// <summary>
        /// 按样条参数移动目标。样条坐标与目标位置必须处于同一坐标空间。
        /// </summary>
        public long MoveBezierPath(Transform target, BezierSpline path, float duration, Ease ease = Ease.Default, int cycles = 1,
            CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0, bool useUnscaledTime = false,
            bool orientToPath = false)
        {
            if (target == null || path == null || path.SegmentCount == 0) return 0;
            return Add(target, 0f, 1f, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, t =>
            {
                if (path == null) return;
                target.position = path.EvaluateLocalPositionUniform(t);
                if (orientToPath)
                {
                    var tangent = path.EvaluateLocalTangentUniform(t);
                    if (tangent.sqrMagnitude > 0.000001f)
                        target.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                }
            });
        }

        /// <summary>根据起点、终点和弧线参数生成路径并移动目标。</summary>
        public long MoveBezierPath(Transform target, Vector3 start, Vector3 end, float duration,
            float arcHeight = 2f, float sideOffset = 0f, Ease ease = Ease.Default, int cycles = 1,
            CycleMode cycleMode = CycleMode.Restart, float startDelay = 0, float endDelay = 0,
            bool useUnscaledTime = false, bool orientToPath = false)
        {
            if (target == null) return 0;
            var spline = BezierSplineGenerator.Create(start, end, arcHeight, sideOffset);
            return Add(target, 0f, 1f, duration, ease, cycles, cycleMode, startDelay, endDelay, useUnscaledTime, t =>
            {
                target.position = spline.Evaluate(t);
                if (orientToPath)
                {
                    var tangent = spline.EvaluateTangent(t);
                    if (tangent.sqrMagnitude > 0.000001f)
                        target.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                }
            });
        }

        private static Vector3 EvaluateBezier(Vector3[] points, Vector3[] work, float t)
        {
            Array.Copy(points, work, points.Length);
            for (var level = points.Length - 1; level > 0; level--)
                for (var i = 0; i < level; i++)
                    work[i] = Vector3.LerpUnclamped(work[i], work[i + 1], t);
            return work[0];
        }
    }
}
