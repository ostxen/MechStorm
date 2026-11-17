using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 每次 SelectNext 以等概率选择一个子节点，适合动作变体随机播放。
    /// </summary>
    public sealed class RandomAnimationSelectorNode : AnimationSelectorNode
    {
        private readonly RandomSelectionStrategy _strategy;

        public RandomAnimationSelectorNode(PlayableGraph graph, int inputCapacity = 0)
            : base(graph, inputCapacity)
        {
            _strategy = new RandomSelectionStrategy();
        }

        protected override int SelectIndex()
        {
            return _strategy.Select(Children.Count, SelectedIndex);
        }
    }
}
