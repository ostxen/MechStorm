using UnityEngine;

namespace TEngine
{
    /// <summary>
    /// Bézier 样条生成规则集合。
    /// 根据起点、终点和形状参数计算控制点，创建或直接求值动态路径。
    /// </summary>
    public static class BezierSplineGenerator
    {
        /// <summary>
        /// 根据起点、终点和弧线参数创建一段三阶 Bézier 样条。
        /// </summary>
        public static BezierSpline Create(Vector3 start, Vector3 end, float arcHeight, float sideOffset)
        {
            GetControlPoints(start, end, arcHeight, sideOffset, out var p0, out var p1, out var p2, out var p3);
            return new BezierSplineBuilder()
                .AddCubicSegment(p0, p1, p2, p3)
                .Build();
        }

        /// <summary>
        /// 计算动态轨迹的四个控制点。侧向基准会在运动方向接近竖直时自动切换。
        /// </summary>
        public static void GetControlPoints(
            Vector3 start,
            Vector3 end,
            float arcHeight,
            float sideOffset,
            out Vector3 p0,
            out Vector3 p1,
            out Vector3 p2,
            out Vector3 p3)
        {
            var delta = end - start;
            var distance = delta.magnitude;
            var forward = distance > 0.0001f ? delta / distance : Vector3.forward;
            var verticality = Mathf.Abs(Vector3.Dot(forward, Vector3.up));
            var fallbackWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 0.98f, verticality));
            var referenceUp = Vector3.Slerp(Vector3.up, Vector3.left, fallbackWeight).normalized;
            var side = Vector3.Cross(referenceUp, forward);
            if (side.sqrMagnitude < 0.000001f)
            {
                side = Vector3.Cross(Vector3.forward, forward);
            }
            side.Normalize();
            var bend = Vector3.up * arcHeight + side * sideOffset;
            var handleDistance = distance * 0.3f;

            p0 = start;
            p1 = start + forward * handleDistance + bend;
            p2 = end - forward * handleDistance + bend;
            p3 = end;
        }

        /// <summary>直接求取动态轨迹在归一化参数 t 处的位置。</summary>
        public static Vector3 Evaluate(
            Vector3 start,
            Vector3 end,
            float t,
            float arcHeight,
            float sideOffset)
        {
            GetControlPoints(start, end, arcHeight, sideOffset, out var p0, out var p1, out var p2, out var p3);
            return EvaluateCubic(p0, p1, p2, p3, Mathf.Clamp01(t));
        }

        /// <summary>直接求取动态轨迹在归一化参数 t 处的切向量。</summary>
        public static Vector3 EvaluateTangent(
            Vector3 start,
            Vector3 end,
            float t,
            float arcHeight,
            float sideOffset)
        {
            GetControlPoints(start, end, arcHeight, sideOffset, out var p0, out var p1, out var p2, out var p3);
            return EvaluateCubicDerivative(p0, p1, p2, p3, Mathf.Clamp01(t));
        }

        /// <summary>使用 Bernstein 基函数求取单段三阶 Bézier 位置。</summary>
        public static Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var u = 1f - t;
            return u * u * u * p0
                + 3f * u * u * t * p1
                + 3f * u * t * t * p2
                + t * t * t * p3;
        }

        /// <summary>求取单段三阶 Bézier 对参数 t 的一阶导数。</summary>
        public static Vector3 EvaluateCubicDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var u = 1f - t;
            return 3f * u * u * (p1 - p0)
                + 6f * u * t * (p2 - p1)
                + 3f * t * t * (p3 - p2);
        }
    }
}
