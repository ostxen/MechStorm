namespace TEngine.Extensions.VirtualizedList
{
    /// <summary>
    /// 独立 Core 使用的线性列表参数。
    /// </summary>
    public sealed class VirtualizedListOptions
    {
        public bool Vertical = true;
        public float BufferPixels = 200f;
        public float ItemSpacing = 0f;
        public float ItemDefaultSize = 100f;
        public IVirtualizedListLayout Layout;
        public VirtualizedSnapOptions Snap = new();
        public int MaxCachedItemsPerPrefab = 32;
        public IVirtualizedItemAnimator Animator;
        public IVirtualizedItemDecoration Decoration;
    }
}
