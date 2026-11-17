namespace TEngine.Extensions.VirtualizedList
{
    public readonly struct VirtualizedListScrollAnchor
    {
        public VirtualizedListScrollAnchor(int itemIndex, float offset, bool stickToEnd)
        {
            ItemIndex = itemIndex;
            Offset = offset;
            StickToEnd = stickToEnd;
        }

        public int ItemIndex { get; }
        public float Offset { get; }
        public bool StickToEnd { get; }
    }
}
