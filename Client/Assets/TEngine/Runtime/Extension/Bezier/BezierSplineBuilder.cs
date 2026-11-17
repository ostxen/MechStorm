using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine
{
    /// <summary>
    /// Bézier 样条构建器，负责在创建阶段组装多段曲线。
    /// </summary>
    public sealed class BezierSplineBuilder
    {
        private readonly List<BezierSplinePoint> _points = new List<BezierSplinePoint>();
        private bool _closed;
        private int _arcLengthSamplesPerSegment = 24;

        /// <summary>
        /// 设置生成样条是否闭合。
        /// </summary>
        public BezierSplineBuilder SetClosed(bool closed)
        {
            _closed = closed;
            return this;
        }

        /// <summary>
        /// 设置弧长采样精度。
        /// </summary>
        public BezierSplineBuilder SetArcLengthSamplesPerSegment(int samples)
        {
            _arcLengthSamplesPerSegment = Mathf.Max(4, samples);
            return this;
        }

        /// <summary>
        /// 追加一段三阶 Bézier。后续段必须从当前末尾锚点开始。
        /// </summary>
        public BezierSplineBuilder AddCubicSegment(
            Vector3 start,
            Vector3 startControl,
            Vector3 endControl,
            Vector3 end)
        {
            if (_points.Count == 0)
            {
                _points.Add(new BezierSplinePoint { Position = start });
            }
            else if ((_points[_points.Count - 1].Position - start).sqrMagnitude > 0.000001f)
            {
                throw new InvalidOperationException("The cubic segment must start at the builder's last point.");
            }

            var startPoint = _points[_points.Count - 1];
            startPoint.Mode = BezierPointMode.BezierCorner;
            startPoint.OutTangent = startControl - start;
            _points.Add(new BezierSplinePoint
            {
                Position = end,
                InTangent = endControl - end,
                Mode = BezierPointMode.BezierCorner,
            });
            return this;
        }

        /// <summary>
        /// 构建一条独立的 Bézier 样条。构建器之后仍可继续追加段。
        /// </summary>
        public BezierSpline Build()
        {
            var points = new List<BezierSplinePoint>(_points.Count);
            for (var i = 0; i < _points.Count; i++)
            {
                var source = _points[i];
                points.Add(new BezierSplinePoint
                {
                    Position = source.Position,
                    InTangent = source.InTangent,
                    OutTangent = source.OutTangent,
                    Mode = source.Mode,
                });
            }

            return new BezierSpline(points, _closed, _arcLengthSamplesPerSegment);
        }
    }
}
