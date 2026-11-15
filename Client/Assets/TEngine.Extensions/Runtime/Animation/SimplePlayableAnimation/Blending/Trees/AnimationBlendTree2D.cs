using UnityEngine;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 按二维参数混合样本，默认使用 Cartesian Gradient Band 权重，也可注入方向或 Compute 计算器。
    /// </summary>
    public sealed class AnimationBlendTree2D : AnimationBlendTree
    {
        private readonly Vector2[] _thresholds;
        private readonly float[] _weights;
        private readonly IBlendWeightCalculator2D _calculator;
        private Vector2 _parameter;

        /// <summary>计算器由调用方持有；若传入 Compute 后端，调用方负责 Dispose。</summary>
        public AnimationBlendTree2D(PlayableGraph graph, Vector2[] thresholds,
            IBlendWeightCalculator2D calculator = null)
            : base(graph, ValidateThresholds(thresholds))
        {
            _thresholds = (Vector2[])thresholds.Clone();
            _weights = new float[_thresholds.Length];
            _calculator = calculator ?? new CartesianBlendWeightCalculator2D();
        }

        internal static int ValidateThresholds(Vector2[] thresholds)
        {
            BlendParameterValidation.CheckSamples(thresholds);
            return thresholds.Length;
        }

        public Vector2 Parameter
        {
            get => _parameter;
            set
            {
                ThrowIfDisposed();
                BlendParameterValidation.Check(value);
                _parameter = value;
                RecalculateWeights();
            }
        }

        protected override void RecalculateWeights()
        {
            if (ChildNodes.Count != _thresholds.Length) return;
            _calculator.Calculate(_parameter, _thresholds, _weights);
            ApplyNormalizedWeights(_weights);
        }
    }
}
