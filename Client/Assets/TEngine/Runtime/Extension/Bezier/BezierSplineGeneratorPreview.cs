using UnityEngine;

namespace TEngine
{
    /// <summary>
    /// Bézier 样条生成器的场景预览组件。
    /// 根据起点、终点和形状参数绘制动态生成的路径，便于调试轨迹效果。
    /// </summary>
    [ExecuteAlways]
    public sealed class BezierSplineGeneratorPreview : MonoBehaviour
    {
        [SerializeField]
        private Transform _start;

        [SerializeField]
        private Transform _target;

        [SerializeField]
        private float _arcHeight = 2f;

        [SerializeField]
        private float _sideOffset;

        [SerializeField, Min(8)]
        private int _samples = 32;

        [SerializeField]
        private bool _showDirection = true;

        public Transform Start => _start;
        public Transform Target => _target;
        public float ArcHeight => _arcHeight;
        public float SideOffset => _sideOffset;

        private void OnValidate()
        {
            _samples = Mathf.Max(8, _samples);
        }

        private void OnDrawGizmos()
        {
            if (_start == null || _target == null)
            {
                return;
            }

            var previous = _start.position;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            for (var i = 1; i <= _samples; i++)
            {
                var current = Evaluate(i / (float)_samples);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }

            if (_showDirection)
            {
                for (var i = 1; i < _samples; i += 4)
                {
                    var t = i / (float)_samples;
                    var position = Evaluate(t);
                    var tangent = EvaluateTangent(t);
                    if (tangent.sqrMagnitude > 0.000001f)
                    {
                        Gizmos.DrawRay(position, tangent.normalized * 0.25f);
                    }
                }
            }
        }

        public Vector3 Evaluate(float t)
        {
            return _start == null || _target == null
                ? transform.position
                : BezierSplineGenerator.Evaluate(_start.position, _target.position, t, _arcHeight, _sideOffset);
        }

        public Vector3 EvaluateTangent(float t)
        {
            return _start == null || _target == null
                ? Vector3.zero
                : BezierSplineGenerator.EvaluateTangent(_start.position, _target.position, t, _arcHeight, _sideOffset);
        }
    }
}
