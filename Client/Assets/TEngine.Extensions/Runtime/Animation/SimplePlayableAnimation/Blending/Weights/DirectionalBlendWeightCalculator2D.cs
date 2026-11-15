using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 使用 Polar Gradient Band 插值混合方向和速度；允许一个原点 Idle 样本及同方向不同长度的样本。
    /// </summary>
    public sealed class DirectionalBlendWeightCalculator2D : IBlendWeightCalculator2D
    {
        private readonly GradientBandWeightCalculator2D _calculator = new GradientBandWeightCalculator2D(true);

        public void Calculate(Vector2 parameter, IReadOnlyList<Vector2> thresholds, IList<float> weights)
        {
            _calculator.Calculate(parameter, thresholds, weights);
        }
    }
}
