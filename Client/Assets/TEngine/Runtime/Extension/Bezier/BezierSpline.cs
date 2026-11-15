using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine
{
    /// <summary>
    /// 锚点两侧控制柄的连接方式。
    /// </summary>
    public enum BezierPointMode
    {
        /// <summary>两侧控制柄均为零，形成尖角。</summary>
        Corner,
        /// <summary>两侧控制柄独立编辑，不强制共线。</summary>
        BezierCorner,
        /// <summary>两侧控制柄保持反向共线，形成 C1 平滑连接。</summary>
        Smooth,
    }

    /// <summary>
    /// 多段 Bézier 样条中的一个锚点及其局部控制柄。
    /// </summary>
    [Serializable]
    public sealed class BezierSplinePoint
    {
        [SerializeField] private Vector3 _position;
        [SerializeField] private Vector3 _inTangent;
        [SerializeField] private Vector3 _outTangent;
        [SerializeField] private BezierPointMode _mode = BezierPointMode.Smooth;
        [NonSerialized] private Action _changed;

        /// <summary>锚点的局部坐标。</summary>
        public Vector3 Position { get => _position; set { _position = value; _changed?.Invoke(); } }
        /// <summary>指向当前锚点的入控制柄偏移量。</summary>
        public Vector3 InTangent { get => _inTangent; set { _inTangent = value; _changed?.Invoke(); } }
        /// <summary>从当前锚点指出的出控制柄偏移量。</summary>
        public Vector3 OutTangent { get => _outTangent; set { _outTangent = value; _changed?.Invoke(); } }
        /// <summary>当前锚点的控制柄模式。</summary>
        public BezierPointMode Mode { get => _mode; set { _mode = value; _changed?.Invoke(); } }

        internal void BindChangeCallback(Action changed) => _changed = changed;
    }

    /// <summary>
    /// Bézier 曲线的核心数据与运行时操作对象。
    /// 管理多段曲线的锚点和控制柄，并提供位置、切线、导数、弧长及编辑操作。
    /// </summary>
    [Serializable]
    public sealed class BezierSpline : ISerializationCallbackReceiver
    {
        [SerializeField] private List<BezierSplinePoint> _points = new List<BezierSplinePoint>();
        [SerializeField] private bool _closed;
        [SerializeField, Min(4)] private int _arcLengthSamplesPerSegment = 24;

        [NonSerialized] private float[] _arcLengthTable;
        [NonSerialized] private float _totalLength;

        /// <summary>创建一条空样条。</summary>
        public BezierSpline()
        {
        }

        internal BezierSpline(List<BezierSplinePoint> points, bool closed, int arcLengthSamplesPerSegment)
        {
            _points = points ?? new List<BezierSplinePoint>();
            _closed = closed;
            _arcLengthSamplesPerSegment = arcLengthSamplesPerSegment;
            Validate();
        }

        /// <summary>样条锚点列表。修改锚点属性会自动使弧长缓存失效。</summary>
        public IReadOnlyList<BezierSplinePoint> Points => _points;
        /// <summary>是否连接最后一个锚点和第一个锚点。</summary>
        public bool Closed
        {
            get => _closed;
            set
            {
                if (_closed == value) return;
                _closed = value;
                InvalidateArcLengthCache();
            }
        }
        /// <summary>当前样条包含的 Bézier 段数量。</summary>
        public int SegmentCount => _points.Count < 2 ? 0 : _closed ? _points.Count : _points.Count - 1;

        /// <summary>按当前弧长采样精度估算的样条总长度。</summary>
        public float Length
        {
            get
            {
                EnsureArcLengthTable();
                return _totalLength;
            }
        }

        /// <summary>规范化点模式并重建内部缓存状态。</summary>
        public void Validate()
        {
            for (var i = 0; i < _points.Count; i++) BindPoint(_points[i]);
            _arcLengthSamplesPerSegment = Mathf.Max(4, _arcLengthSamplesPerSegment);
            for (var i = 0; i < _points.Count; i++)
            {
                NormalizePoint(_points[i]);
            }
            InvalidateArcLengthCache();
        }

        /// <summary>Unity 序列化前回调。</summary>
        public void OnBeforeSerialize()
        {
        }

        /// <summary>Unity 反序列化后恢复回调和缓存状态。</summary>
        public void OnAfterDeserialize()
        {
            if (_points == null)
            {
                _points = new List<BezierSplinePoint>();
            }
            for (var i = 0; i < _points.Count; i++)
            {
                if (_points[i] != null)
                {
                    BindPoint(_points[i]);
                }
            }
            InvalidateArcLengthCache();
        }

        /// <summary>按分段参数求值，t 范围为 0 到 1。</summary>
        public Vector3 EvaluateLocalPosition(float t)
        {
            EvaluateSegment(t, out var segment, out var segmentT);
            if (segment < 0) return _points.Count > 0 ? _points[0].Position : Vector3.zero;
            GetSegment(segment, out var p0, out var p1, out var p2, out var p3);
            return EvaluateCubic(p0, p1, p2, p3, segmentT);
        }

        /// <summary>EvaluateLocalPosition 的通用别名。</summary>
        public Vector3 Evaluate(float t) => EvaluateLocalPosition(t);

        /// <summary>求取参数 t 处的一阶切向量。</summary>
        public Vector3 EvaluateLocalTangent(float t)
        {
            EvaluateSegment(t, out var segment, out var segmentT);
            if (segment < 0) return Vector3.zero;
            GetSegment(segment, out var p0, out var p1, out var p2, out var p3);
            return EvaluateCubicDerivative(p0, p1, p2, p3, segmentT);
        }

        /// <summary>EvaluateLocalTangent 的通用别名。</summary>
        public Vector3 EvaluateTangent(float t) => EvaluateLocalTangent(t);

        /// <summary>求取整条样条归一化参数的 0 至 3 阶导数。</summary>
        public Vector3 EvaluateLocalDerivative(float t, int order)
        {
            if (order < 0 || order > 3) throw new ArgumentOutOfRangeException(nameof(order));
            if (order == 0) return EvaluateLocalPosition(t);
            EvaluateSegment(t, out var segment, out var u);
            if (segment < 0) return Vector3.zero;
            GetSegment(segment, out var p0, out var p1, out var p2, out var p3);
            Vector3 value;
            if (order == 1)
                value = EvaluateCubicDerivative(p0, p1, p2, p3, u);
            else if (order == 2)
                value = 6f * ((1f - u) * (p2 - 2f * p1 + p0) + u * (p3 - 2f * p2 + p1));
            else
                value = 6f * (p3 - 3f * p2 + 3f * p1 - p0);
            return value * Mathf.Pow(SegmentCount, order);
        }

        /// <summary>按近似弧长均匀分布的参数求取位置。</summary>
        public Vector3 EvaluateLocalPositionUniform(float t)
        {
            return EvaluateLocalPosition(FindParameterAtDistance(Mathf.Clamp01(t)));
        }

        /// <summary>按近似弧长均匀分布的参数求取切向量。</summary>
        public Vector3 EvaluateLocalTangentUniform(float t)
        {
            return EvaluateLocalTangent(FindParameterAtDistance(Mathf.Clamp01(t)));
        }

        /// <summary>保留锚点位置，重新计算满足 C2 连续性的控制柄。</summary>
        public void RecalculateC2Tangents()
        {
            var count = _points.Count;
            if (count < 2) return;
            var diagonal = new float[count];
            var rhs = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                diagonal[i] = 4f;
                if (_closed || (i > 0 && i < count - 1))
                    rhs[i] = 6f * (_points[(i + count - 1) % count].Position - 2f * _points[i].Position + _points[(i + 1) % count].Position);
            }

            Vector3[] second;
            if (!_closed)
            {
                diagonal[0] = diagonal[count - 1] = 1f;
                second = SolveSecondDerivatives(diagonal, rhs, false);
            }
            else if (count == 2)
            {
                var value = 6f * (_points[1].Position - _points[0].Position);
                second = new[] { value, -value };
            }
            else
            {
                const float gamma = -4f;
                diagonal[0] -= gamma;
                diagonal[count - 1] -= 1f / gamma;
                second = SolveSecondDerivatives(diagonal, rhs, true);
                var correctionRhs = new Vector3[count];
                correctionRhs[0] = Vector3.one * gamma;
                correctionRhs[count - 1] = Vector3.one;
                var correction = SolveSecondDerivatives(diagonal, correctionRhs, true);
                var factor = (second[0] + second[count - 1] / gamma) / (1f + correction[0].x + correction[count - 1].x / gamma);
                for (var i = 0; i < count; i++) second[i] -= correction[i].x * factor;
            }

            for (var i = 0; i < count; i++)
            {
                _points[i].Mode = BezierPointMode.Smooth;
                _points[i].InTangent = _points[i].OutTangent = Vector3.zero;
            }
            for (var i = 0; i < SegmentCount; i++)
            {
                var next = (i + 1) % count;
                var delta = _points[next].Position - _points[i].Position;
                _points[i].OutTangent = (delta - (2f * second[i] + second[next]) / 6f) / 3f;
                _points[next].InTangent = -(delta + (second[i] + 2f * second[next]) / 6f) / 3f;
            }
            if (!_closed)
            {
                _points[0].InTangent = -_points[0].OutTangent;
                _points[count - 1].OutTangent = -_points[count - 1].InTangent;
            }
            InvalidateArcLengthCache();
        }

        /// <summary>主动使弧长采样缓存失效。</summary>
        public void RebuildCache() => InvalidateArcLengthCache();

        /// <summary>在样条末尾添加一个锚点并返回其索引。</summary>
        public int AddPoint(Vector3 position)
        {
            var point = new BezierSplinePoint { Position = position };
            BindPoint(point);
            _points.Add(point);
            InvalidateArcLengthCache();
            return _points.Count - 1;
        }

        /// <summary>删除指定索引的锚点。</summary>
        public void RemovePointAt(int index)
        {
            if (index < 0 || index >= _points.Count) return;
            _points.RemoveAt(index);
            InvalidateArcLengthCache();
        }

        /// <summary>反转锚点顺序和控制柄方向。</summary>
        public void Reverse()
        {
            _points.Reverse();
            for (var i = 0; i < _points.Count; i++)
            {
                var tangent = _points[i].InTangent;
                _points[i].InTangent = _points[i].OutTangent;
                _points[i].OutTangent = tangent;
            }
            InvalidateArcLengthCache();
        }

        /// <summary>根据相邻锚点重新生成指定点的控制柄。</summary>
        public void ResetTangents(int index)
        {
            if (index < 0 || index >= _points.Count) return;
            var point = _points[index];
            if (point.Mode == BezierPointMode.Corner || _points.Count < 2)
            {
                point.InTangent = point.OutTangent = Vector3.zero;
                return;
            }

            var previousIndex = index == 0 ? (_closed ? _points.Count - 1 : 0) : index - 1;
            var nextIndex = index == _points.Count - 1 ? (_closed ? 0 : _points.Count - 1) : index + 1;
            Vector3 direction;
            if (!_closed && index == 0) direction = _points[1].Position - point.Position;
            else if (!_closed && index == _points.Count - 1) direction = point.Position - _points[_points.Count - 2].Position;
            else direction = _points[nextIndex].Position - _points[previousIndex].Position;
            if (direction.sqrMagnitude < 0.000001f)
            {
                point.InTangent = point.OutTangent = Vector3.zero;
                return;
            }

            var previousDistance = !_closed && index == 0 ? Vector3.Distance(point.Position, _points[1].Position) : Vector3.Distance(point.Position, _points[previousIndex].Position);
            var nextDistance = !_closed && index == _points.Count - 1 ? Vector3.Distance(point.Position, _points[_points.Count - 2].Position) : Vector3.Distance(point.Position, _points[nextIndex].Position);
            var tangent = direction.normalized * (Mathf.Min(previousDistance, nextDistance) / 3f);
            point.InTangent = -tangent;
            point.OutTangent = tangent;
            InvalidateArcLengthCache();
        }

        /// <summary>使用 De Casteljau 分割算法在参数位置插入锚点。</summary>
        public int InsertPointAt(float t)
        {
            if (SegmentCount == 0) return AddPoint(EvaluateLocalPosition(t));
            EvaluateSegment(t, out var segment, out var u);
            var nextIndex = (segment + 1) % _points.Count;
            if (u <= 0.00001f) return segment;
            if (u >= 0.99999f) return nextIndex;
            GetSegment(segment, out var p0, out var p1, out var p2, out var p3);
            var a = Vector3.Lerp(p0, p1, u);
            var b = Vector3.Lerp(p1, p2, u);
            var c = Vector3.Lerp(p2, p3, u);
            var d = Vector3.Lerp(a, b, u);
            var e = Vector3.Lerp(b, c, u);
            var point = new BezierSplinePoint
            {
                Position = Vector3.Lerp(d, e, u),
                InTangent = d - Vector3.Lerp(d, e, u),
                OutTangent = e - Vector3.Lerp(d, e, u),
                Mode = BezierPointMode.BezierCorner,
            };
            BindPoint(point);
            _points[segment].OutTangent = a - p0;
            _points[nextIndex].InTangent = c - p3;
            var insertIndex = Mathf.Min(segment + 1, _points.Count);
            _points.Insert(insertIndex, point);
            InvalidateArcLengthCache();
            return insertIndex;
        }

        /// <summary>设置指定锚点的入或出控制柄。</summary>
        public void SetTangent(int index, bool incoming, Vector3 tangent)
        {
            if (index < 0 || index >= _points.Count) return;
            var point = _points[index];
            if (point.Mode == BezierPointMode.Corner)
            {
                point.InTangent = point.OutTangent = Vector3.zero;
                return;
            }
            if (incoming)
            {
                point.InTangent = tangent;
                if (point.Mode == BezierPointMode.Smooth)
                    point.OutTangent = OppositeTangent(tangent, point.OutTangent.sqrMagnitude > 0.000001f ? point.OutTangent.magnitude : tangent.magnitude);
            }
            else
            {
                point.OutTangent = tangent;
                if (point.Mode == BezierPointMode.Smooth)
                    point.InTangent = OppositeTangent(tangent, point.InTangent.sqrMagnitude > 0.000001f ? point.InTangent.magnitude : tangent.magnitude);
            }
            InvalidateArcLengthCache();
        }

        /// <summary>设置锚点模式，并按模式约束控制柄。</summary>
        public void SetMode(int index, BezierPointMode mode)
        {
            if (index < 0 || index >= _points.Count) return;
            var point = _points[index];
            point.Mode = mode;
            if (mode == BezierPointMode.Corner)
            {
                point.InTangent = point.OutTangent = Vector3.zero;
            }
            else if (mode == BezierPointMode.Smooth)
            {
                NormalizeSmoothPoint(point);
            }
            InvalidateArcLengthCache();
        }

        private void EvaluateSegment(float t, out int segment, out float segmentT)
        {
            if (SegmentCount == 0) { segment = -1; segmentT = 0f; return; }
            var normalized = Mathf.Clamp01(t);
            var scaled = normalized * SegmentCount;
            segment = Mathf.Min(Mathf.FloorToInt(scaled), SegmentCount - 1);
            segmentT = normalized >= 1f ? 1f : scaled - segment;
        }

        private void GetSegment(int index, out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
        {
            var nextIndex = _closed ? (index + 1) % _points.Count : index + 1;
            var current = _points[index];
            var next = _points[nextIndex];
            p0 = current.Position;
            p1 = current.Position + current.OutTangent;
            p2 = next.Position + next.InTangent;
            p3 = next.Position;
        }

        private float FindParameterAtDistance(float normalizedDistance)
        {
            EnsureArcLengthTable();
            if (_totalLength <= 0.000001f || _arcLengthTable == null || _arcLengthTable.Length < 2) return normalizedDistance;
            var targetLength = normalizedDistance * _totalLength;
            var high = _arcLengthTable.Length - 1;
            var low = 0;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (_arcLengthTable[middle] < targetLength) low = middle + 1;
                else high = middle;
            }
            var upper = Mathf.Clamp(low, 1, _arcLengthTable.Length - 1);
            var lower = upper - 1;
            var lowerLength = _arcLengthTable[lower];
            var intervalLength = _arcLengthTable[upper] - lowerLength;
            var interval = intervalLength > 0.000001f ? (targetLength - lowerLength) / intervalLength : 0f;
            return Mathf.Lerp(lower / (float)(_arcLengthTable.Length - 1), upper / (float)(_arcLengthTable.Length - 1), interval);
        }

        private void EnsureArcLengthTable()
        {
            var sampleCount = Mathf.Max(1, SegmentCount * Mathf.Max(4, _arcLengthSamplesPerSegment));
            if (_arcLengthTable != null && _arcLengthTable.Length == sampleCount + 1) return;
            _arcLengthTable = new float[sampleCount + 1];
            var previous = EvaluateLocalPosition(0f);
            for (var i = 1; i <= sampleCount; i++)
            {
                var current = EvaluateLocalPosition(i / (float)sampleCount);
                _arcLengthTable[i] = _arcLengthTable[i - 1] + Vector3.Distance(previous, current);
                previous = current;
            }
            _totalLength = _arcLengthTable[sampleCount];
        }

        private void InvalidateArcLengthCache() { _arcLengthTable = null; _totalLength = 0f; }

        private void BindPoint(BezierSplinePoint point)
        {
            point.BindChangeCallback(InvalidateArcLengthCache);
        }

        private static Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        private static Vector3 EvaluateCubicDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var u = 1f - t;
            return 3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2);
        }

        private static Vector3 OppositeTangent(Vector3 tangent, float length) => tangent.sqrMagnitude > 0.000001f ? -tangent.normalized * length : Vector3.zero;

        private static void NormalizePoint(BezierSplinePoint point)
        {
            if (point.Mode == BezierPointMode.Corner)
            {
                point.InTangent = point.OutTangent = Vector3.zero;
            }
            else if (point.Mode == BezierPointMode.Smooth)
            {
                NormalizeSmoothPoint(point);
            }
        }

        private static void NormalizeSmoothPoint(BezierSplinePoint point)
        {
            if (point.InTangent.sqrMagnitude > 0.000001f)
            {
                var length = point.OutTangent.sqrMagnitude > 0.000001f ? point.OutTangent.magnitude : point.InTangent.magnitude;
                point.OutTangent = OppositeTangent(point.InTangent, length);
            }
            else if (point.OutTangent.sqrMagnitude > 0.000001f)
            {
                var length = point.InTangent.sqrMagnitude > 0.000001f ? point.InTangent.magnitude : point.OutTangent.magnitude;
                point.InTangent = OppositeTangent(point.OutTangent, length);
            }
        }

        private static Vector3[] SolveSecondDerivatives(float[] diagonal, Vector3[] rhs, bool periodic)
        {
            var count = diagonal.Length;
            var upper = new float[count];
            var result = new Vector3[count];
            upper[0] = periodic ? 1f / diagonal[0] : 0f;
            result[0] = rhs[0] / diagonal[0];
            for (var i = 1; i < count; i++)
            {
                var lower = !periodic && i == count - 1 ? 0f : 1f;
                var denominator = diagonal[i] - lower * upper[i - 1];
                upper[i] = i < count - 1 ? 1f / denominator : 0f;
                result[i] = (rhs[i] - lower * result[i - 1]) / denominator;
            }
            for (var i = count - 2; i >= 0; i--) result[i] -= upper[i] * result[i + 1];
            return result;
        }
    }
}
