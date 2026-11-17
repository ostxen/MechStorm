using System.Collections.Generic;
using UnityEngine;

namespace TEngine
{
    /// <summary>
    /// 场景中的 Bézier 样条组件。
    /// 持有并编辑一条样条，负责序列化以及局部空间与世界空间的转换。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BezierSplineComponent : MonoBehaviour
    {
        [SerializeField]
        private BezierSpline _spline = new BezierSpline();

        /// <summary>组件持有的 Bézier 样条对象。</summary>
        public BezierSpline Spline => _spline;
        /// <summary>样条锚点列表，坐标位于组件局部空间。</summary>
        public IReadOnlyList<BezierSplinePoint> Points => _spline.Points;
        /// <summary>是否连接最后一个锚点和第一个锚点。</summary>
        public bool Closed { get => _spline.Closed; set => _spline.Closed = value; }
        /// <summary>样条段数量。</summary>
        public int SegmentCount => _spline.SegmentCount;
        /// <summary>样条总长度。</summary>
        public float Length => _spline.Length;

        private void Reset()
        {
            _spline = new BezierSpline();
            _spline.AddPoint(Vector3.zero);
            _spline.AddPoint(Vector3.right * 3f);
            _spline.SetTangent(0, false, Vector3.right);
            _spline.SetTangent(1, true, Vector3.left);
        }

        private void OnValidate()
        {
            if (_spline == null) _spline = new BezierSpline();
            _spline.Validate();
        }

        /// <summary>在组件局部空间按参数求取位置。</summary>
        public Vector3 EvaluateLocalPosition(float t) => _spline.EvaluateLocalPosition(t);
        /// <summary>在世界空间按参数求取位置。</summary>
        public Vector3 EvaluateWorldPosition(float t) => transform.TransformPoint(_spline.EvaluateLocalPosition(t));
        /// <summary>在组件局部空间求取切向量。</summary>
        public Vector3 EvaluateLocalTangent(float t) => _spline.EvaluateLocalTangent(t);
        /// <summary>在世界空间求取切向量。</summary>
        public Vector3 EvaluateWorldTangent(float t) => transform.TransformVector(_spline.EvaluateLocalTangent(t));
        /// <summary>在组件局部空间求取指定阶数导数。</summary>
        public Vector3 EvaluateLocalDerivative(float t, int order) => _spline.EvaluateLocalDerivative(t, order);
        /// <summary>在世界空间求取指定阶数导数。</summary>
        public Vector3 EvaluateWorldDerivative(float t, int order) => order == 0
            ? EvaluateWorldPosition(t)
            : transform.TransformVector(_spline.EvaluateLocalDerivative(t, order));
        /// <summary>按近似弧长均匀分布的参数求取局部位置。</summary>
        public Vector3 EvaluateLocalPositionUniform(float t) => _spline.EvaluateLocalPositionUniform(t);
        /// <summary>按近似弧长均匀分布的参数求取世界位置。</summary>
        public Vector3 EvaluateWorldPositionUniform(float t) => transform.TransformPoint(_spline.EvaluateLocalPositionUniform(t));
        /// <summary>按近似弧长均匀分布的参数求取世界切向量。</summary>
        public Vector3 EvaluateWorldTangentUniform(float t) => transform.TransformVector(_spline.EvaluateLocalTangentUniform(t));
        /// <summary>重新计算满足 C2 连续性的控制柄。</summary>
        public void RecalculateC2Tangents() => _spline.RecalculateC2Tangents();
        /// <summary>使弧长缓存失效。</summary>
        public void RebuildCache() => _spline.RebuildCache();
        /// <summary>添加局部空间锚点。</summary>
        public int AddPoint(Vector3 position) => _spline.AddPoint(position);
        /// <summary>删除指定索引的锚点。</summary>
        public void RemovePointAt(int index) => _spline.RemovePointAt(index);
        /// <summary>反转样条方向。</summary>
        public void Reverse() => _spline.Reverse();
        /// <summary>重置指定锚点的控制柄。</summary>
        public void ResetTangents(int index) => _spline.ResetTangents(index);
        /// <summary>在归一化参数位置插入锚点。</summary>
        public int InsertPointAt(float t) => _spline.InsertPointAt(t);
        /// <summary>设置指定锚点的入或出控制柄。</summary>
        public void SetTangent(int index, bool incoming, Vector3 tangent) => _spline.SetTangent(index, incoming, tangent);
        /// <summary>设置指定锚点的控制柄模式。</summary>
        public void SetMode(int index, BezierPointMode mode) => _spline.SetMode(index, mode);
    }
}
