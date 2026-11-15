using System;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 选择候选项时排除当前索引，避免连续两次选择同一项。
    /// </summary>
    public sealed class NoRepeatRandomSelectionStrategy
        : ISelectionStrategy
    {
        private readonly Random _random;

        public NoRepeatRandomSelectionStrategy(Random random = null)
        {
            _random = random ?? new Random();
        }

        public int Select(int candidateCount, int currentIndex)
        {
            if (candidateCount <= 0)
            {
                return -1;
            }

            if (candidateCount == 1 ||
                currentIndex < 0 ||
                currentIndex >= candidateCount)
            {
                return _random.Next(candidateCount);
            }

            var index = _random.Next(candidateCount - 1);
            return index >= currentIndex ? index + 1 : index;
        }
    }
}
