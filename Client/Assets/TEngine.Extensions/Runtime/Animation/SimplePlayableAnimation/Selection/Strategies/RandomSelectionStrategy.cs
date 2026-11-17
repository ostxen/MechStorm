using System;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 以等概率选择一个候选项。
    /// </summary>
    public sealed class RandomSelectionStrategy
        : ISelectionStrategy
    {
        private readonly Random _random;

        public RandomSelectionStrategy(Random random = null)
        {
            _random = random ?? new Random();
        }

        public int Select(int candidateCount, int currentIndex)
        {
            return candidateCount <= 0
                ? -1
                : _random.Next(candidateCount);
        }
    }
}
