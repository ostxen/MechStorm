using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 使用 Cartesian Gradient Band 插值计算二维权重，样本变化时重建内部预计算缓存。
    /// </summary>
    public sealed class CartesianBlendWeightCalculator2D : IBlendWeightCalculator2D
    {
        private readonly GradientBandWeightCalculator2D _calculator = new GradientBandWeightCalculator2D(false);

        public void Calculate(
            Vector2 parameter,
            IReadOnlyList<Vector2> thresholds,
            IList<float> weights)
        {
            _calculator.Calculate(parameter, thresholds, weights);
        }
    }
}
