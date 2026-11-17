using System;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 按一维参数在相邻阈值之间线性混合，适合用移动速度控制动作权重。
    /// </summary>
    public sealed class AnimationBlendTree1D : AnimationBlendTree
    {
        private readonly float[] _thresholds;
        private readonly float[] _weights;
        private float _parameter;

        public AnimationBlendTree1D(PlayableGraph graph, float[] thresholds)
            : base(graph, ValidateThresholds(thresholds))
        {
            _thresholds = (float[])thresholds.Clone();
            _weights = new float[_thresholds.Length];
        }

        internal static int ValidateThresholds(float[] thresholds)
        {
            if (thresholds == null || thresholds.Length == 0)
                throw new ArgumentException("至少需要一个阈值。", nameof(thresholds));
            for (int i = 0; i < thresholds.Length; i++)
            {
                BlendParameterValidation.Check(thresholds[i]);
                if (i > 0 && thresholds[i] <= thresholds[i - 1])
                {
                    throw new ArgumentException("阈值必须严格递增。", nameof(thresholds));
                }
            }
            return thresholds.Length;
        }

        public float Parameter
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

        public float GetThreshold(int index)
        {
            ValidateIndex(index);
            return _thresholds[index];
        }

        protected override void RecalculateWeights()
        {
            if (ChildNodes.Count != _thresholds.Length)
            {
                return;
            }

            var weights = _weights;
            Array.Clear(weights, 0, weights.Length);
            if (ChildNodes.Count == 1 || _parameter <= _thresholds[0])
            {
                weights[0] = 1f;
                ApplyNormalizedWeights(weights);
                return;
            }

            var lastIndex = ChildNodes.Count - 1;
            if (_parameter >= _thresholds[lastIndex])
            {
                weights[lastIndex] = 1f;
                ApplyNormalizedWeights(weights);
                return;
            }

            for (int i = 0; i < lastIndex; i++)
            {
                if (_parameter < _thresholds[i] || _parameter > _thresholds[i + 1])
                {
                    continue;
                }

                var range = (double)_thresholds[i + 1] - _thresholds[i];
                var ratio = (float)(((double)_parameter - _thresholds[i]) / range);
                weights[i] = 1f - ratio;
                weights[i + 1] = ratio;
                break;
            }

            ApplyNormalizedWeights(weights);
        }
    }
}
