using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 参考 Animancer 8.4.0 CartesianMixerState / DirectionalMixerState 的 Gradient Band 核心数学。
    /// 仅保留样本对投影、归一化和预计算，不依赖其 State、同步或更新调度体系。
    /// </summary>
    internal sealed class GradientBandWeightCalculator2D
    {
        private const double AngleScale = 2d;
        private readonly bool _directional;
        private Vector2[] _samples = Array.Empty<Vector2>();
        private double[] _magnitudes;
        private BlendFactor[,] _factors;

        public GradientBandWeightCalculator2D(bool directional)
        {
            _directional = directional;
        }

        public void Calculate(Vector2 parameter, IReadOnlyList<Vector2> samples, IList<float> weights)
        {
            if (samples == null || samples.Count == 0 || weights == null || weights.Count < samples.Count)
                throw new ArgumentException("样本和权重缓冲不能为空，且缓冲必须容纳全部样本。");
            BlendParameterValidation.Check(parameter);
            PrepareSamples(samples);

            var count = _samples.Length;
            var nearest = 0;
            var nearestDistance = double.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var x = (double)parameter.x - _samples[i].x;
                var y = (double)parameter.y - _samples[i].y;
                var distance = x * x + y * y;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }
            if (count == 1 || nearestDistance == 0d)
            {
                SelectSample(weights, nearest, count);
                return;
            }

            var parameterMagnitude = _directional ? Magnitude(parameter) : 0d;
            var total = 0d;
            for (int i = 0; i < count; i++)
            {
                var x = _directional ? parameterMagnitude - _magnitudes[i] : (double)parameter.x - _samples[i].x;
                var y = _directional ? SignedAngle(_samples[i], parameter) * AngleScale : (double)parameter.y - _samples[i].y;
                var weight = 1d;
                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;
                    var factor = _factors[i, j];
                    var angle = factor.OppositeDirections ? Math.Abs(y) : y;
                    weight = Math.Min(weight, 1d - x * factor.X - angle * factor.Y);
                }
                // 不采用 1% 权重截断，保留小权重的连续插值。
                weights[i] = (float)Math.Max(0d, weight);
                total += weights[i];
            }

            if (total <= 0d)
            {
                // 样本覆盖不足或数值退化时保持有效输出，等距时稳定选择首项。
                SelectSample(weights, nearest, count);
                return;
            }
            for (int i = 0; i < count; i++) weights[i] = (float)(weights[i] / total);
        }

        private void PrepareSamples(IReadOnlyList<Vector2> samples)
        {
            var changed = samples.Count != _samples.Length;
            for (int i = 0; !changed && i < samples.Count; i++)
                changed = !samples[i].Equals(_samples[i]);
            if (!changed) return;

            // 比较内容而不是列表引用，支持同一计算器交替处理不同树或原地修改的样本。
            BlendParameterValidation.CheckSamples(samples);
            var count = samples.Count;
            if (_samples.Length != count)
            {
                _samples = new Vector2[count];
                _magnitudes = new double[count];
                _factors = new BlendFactor[count, count];
            }
            for (int i = 0; i < count; i++)
            {
                _samples[i] = samples[i];
                _magnitudes[i] = _directional ? Magnitude(samples[i]) : 0d;
            }

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;
                    var scale = _directional ? (_magnitudes[i] + _magnitudes[j]) * 0.5d : 1d;
                    var x = _directional ? (_magnitudes[j] - _magnitudes[i]) / scale : (double)_samples[j].x - _samples[i].x;
                    var angle = _directional ? SignedAngle(_samples[i], _samples[j]) : 0d;
                    // 相反方向有两条等长圆弧，使用绝对角差避免 +/-PI 和样本顺序造成偏置。
                    var opposite = _directional && Math.Abs(angle) >= Math.PI - 1e-12d;
                    var y = _directional ? (opposite ? Math.Abs(angle) : angle) * AngleScale : (double)_samples[j].y - _samples[i].y;
                    var squareLength = x * x + y * y;
                    _factors[i, j] = new BlendFactor
                    {
                        X = x / squareLength / scale,
                        Y = y / squareLength,
                        OppositeDirections = opposite
                    };
                }
            }
        }

        private static double Magnitude(Vector2 value) =>
            Math.Sqrt((double)value.x * value.x + (double)value.y * value.y);

        private static double SignedAngle(Vector2 from, Vector2 to)
        {
            if ((from.x == 0f && from.y == 0f) || (to.x == 0f && to.y == 0f)) return 0d;
            return Math.Atan2((double)from.x * to.y - (double)from.y * to.x,
                (double)from.x * to.x + (double)from.y * to.y);
        }

        private static void SelectSample(IList<float> weights, int selected, int count)
        {
            for (int i = 0; i < count; i++) weights[i] = i == selected ? 1f : 0f;
        }

        private struct BlendFactor
        {
            public double X;
            public double Y;
            public bool OppositeDirections;
        }
    }
}
