namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 根据候选数量和当前索引选择下一项，不持有 Playable 或资源状态。
    /// </summary>
    public interface ISelectionStrategy
    {
        int Select(int candidateCount, int currentIndex);
    }
}
