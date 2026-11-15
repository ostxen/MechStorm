namespace TEngine.Extensions.VirtualizedList
{
    public enum VirtualizedSnapAlignment
    {
        Start,
        Center,
        End
    }

    public sealed class VirtualizedSnapOptions
    {
        public bool Enabled = true;
        public bool SnapOnEndDrag = true;
        public VirtualizedSnapAlignment Alignment = VirtualizedSnapAlignment.Start;
        public float SmoothTime = 0.16f;
        public float BufferPixels = 0f;
    }
}
