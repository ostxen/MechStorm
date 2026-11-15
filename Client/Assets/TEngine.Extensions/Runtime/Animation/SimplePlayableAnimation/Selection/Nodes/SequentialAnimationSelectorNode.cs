using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 每次 SelectNext 按加入顺序选择下一子节点，可通过 Loop 控制到末尾后是否回到首项。
    /// </summary>
    public sealed class SequentialAnimationSelectorNode
        : AnimationSelectorNode
    {
        private readonly SequentialSelectionStrategy _strategy =
            new SequentialSelectionStrategy();

        public SequentialAnimationSelectorNode(
            PlayableGraph graph,
            bool loop = true,
            int inputCapacity = 0)
            : base(graph, inputCapacity)
        {
            Loop = loop;
            _strategy.Loop = loop;
        }

        public bool Loop { get; set; }

        protected override int SelectIndex()
        {
            _strategy.Loop = Loop;
            return _strategy.Select(Children.Count, SelectedIndex);
        }
    }
}
