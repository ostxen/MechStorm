using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 有多个候选时排除当前索引再等概率随机选择；只有一个候选时继续选择该项。
    /// </summary>
    public sealed class NoRepeatRandomAnimationSelectorNode
        : AnimationSelectorNode
    {
        private readonly NoRepeatRandomSelectionStrategy _strategy =
            new NoRepeatRandomSelectionStrategy();

        public NoRepeatRandomAnimationSelectorNode(
            PlayableGraph graph,
            int inputCapacity = 0)
            : base(graph, inputCapacity)
        {
        }

        protected override int SelectIndex()
        {
            return _strategy.Select(Children.Count, SelectedIndex);
        }
    }
}
