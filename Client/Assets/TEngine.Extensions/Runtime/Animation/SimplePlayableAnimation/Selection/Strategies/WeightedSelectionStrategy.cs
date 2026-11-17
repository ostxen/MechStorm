using System;
using System.Collections.Generic;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 按候选权重随机选择一项，权重为零的候选永远不会被选中。
    /// </summary>
    public sealed class WeightedSelectionStrategy
        : ISelectionStrategy
    {
        private readonly List<float> _weights = new List<float>();
        private readonly Random _random;

        public WeightedSelectionStrategy(Random random = null)
        {
            _random = random ?? new Random();
        }

        public void SetWeight(int index, float weight)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (float.IsNaN(weight) || float.IsInfinity(weight))
            {
                throw new ArgumentException(
                    "选择权重必须是有限数值。",
                    nameof(weight));
            }

            EnsureCount(index + 1);
            _weights[index] = Math.Max(0f, weight);
        }

        public float GetWeight(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            EnsureCount(index + 1);
            return _weights[index];
        }

        public int Select(int candidateCount, int currentIndex)
        {
            if (candidateCount <= 0)
            {
                return -1;
            }

            EnsureCount(candidateCount);
            var total = 0d;
            var lastPositiveIndex = -1;
            for (int i = 0; i < candidateCount; i++)
            {
                if (_weights[i] > 0f)
                {
                    total += _weights[i];
                    lastPositiveIndex = i;
                }
            }

            if (total <= 0d)
            {
                return -1;
            }

            var value = _random.NextDouble() * total;
            for (int i = 0; i < candidateCount; i++)
            {
                value -= _weights[i];
                if (_weights[i] > 0f && value < 0d)
                {
                    return i;
                }
            }

            return lastPositiveIndex;
        }

        private void EnsureCount(int count)
        {
            while (_weights.Count < count)
            {
                _weights.Add(1f);
            }
        }
    }
}
