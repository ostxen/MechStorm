namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 按候选项加入顺序选择下一项。
    /// </summary>
    public sealed class SequentialSelectionStrategy
        : ISelectionStrategy
    {
        public bool Loop { get; set; } = true;

        public int Select(int candidateCount, int currentIndex)
        {
            if (candidateCount <= 0)
            {
                return -1;
            }

            if (currentIndex < 0)
            {
                return 0;
            }

            var nextIndex = currentIndex + 1;
            if (nextIndex < candidateCount)
            {
                return nextIndex;
            }

            return Loop ? 0 : -1;
        }
    }
}
