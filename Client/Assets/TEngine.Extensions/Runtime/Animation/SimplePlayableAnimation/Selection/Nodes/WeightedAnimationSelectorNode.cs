using System;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 按配置权重随机选择子节点，权重控制动作出现频率；总权重不足时保持当前选择。
    /// </summary>
    public sealed class WeightedAnimationSelectorNode
        : AnimationSelectorNode
    {
        private readonly WeightedSelectionStrategy _strategy =
            new WeightedSelectionStrategy();

        public WeightedAnimationSelectorNode(
            PlayableGraph graph,
            int inputCapacity = 0)
            : base(graph, inputCapacity)
        {
        }

        public void SetSelectionWeight(int index, float weight)
        {
            ValidateIndex(index);
            _strategy.SetWeight(index, weight);
        }

        public float GetSelectionWeight(int index)
        {
            ValidateIndex(index);
            return _strategy.GetWeight(index);
        }

        protected override int SelectIndex()
        {
            if (Children.Count == 0)
            {
                return -1;
            }

            return _strategy.Select(Children.Count, SelectedIndex);
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= Children.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
