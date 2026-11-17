using UnityEngine;

namespace TEngine.Extensions.VirtualizedList
{
    public sealed class VirtualizedGridOptions
    {
        public bool Vertical = true;
        public int ColumnCount = 1;
        public Vector2 ItemSize = new(100f, 100f);
        public Vector2 ItemSpacing = Vector2.zero;
        public float BufferPixels = 200f;
        public VirtualizedSnapOptions Snap = new();
        public int MaxCachedItemsPerPrefab = 32;
        public IVirtualizedItemAnimator Animator;
        public IVirtualizedItemDecoration Decoration;
        internal int FirstShownRow = -1;
        internal int LastShownRow = -1;
    }
}
