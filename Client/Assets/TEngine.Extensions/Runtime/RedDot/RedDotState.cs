namespace TEngine.Extensions.RedDot
{
    public enum RedDotDisplayType
    {
        Dot,
        Number,
        New,
        Custom
    }

    public sealed class RedDotState
    {
        public RedDotState(string key)
        {
            Key = key;
        }

        public string Key { get; }
        public bool IsActive { get; internal set; }
        public int Count { get; internal set; }
        public RedDotDisplayType DisplayType { get; internal set; } = RedDotDisplayType.Dot;
    }
}
