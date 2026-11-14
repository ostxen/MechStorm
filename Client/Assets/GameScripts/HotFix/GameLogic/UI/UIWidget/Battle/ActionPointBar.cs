using MechStorm.Presentation.UI;
using UnityEngine;

namespace GameLogic
{
	public partial class ActionPointBar
	{
		private SegmentedSlider _actionPointSlider;

		protected override void BindMemberProperty()
		{
			// 根组件不在生成器的子节点绑定范围内。
			_actionPointSlider = gameObject.GetComponent<SegmentedSlider>();
		}

		protected override void OnCreate()
		{
			ClearHighlight();
		}

		public void SetValue(int current, int max)
		{
			int displayMax = Mathf.Max(0, max);
			int displayCurrent = Mathf.Clamp(current, 0, displayMax);
			_actionPointSlider.SetValue(displayCurrent, displayMax);
			m_tmpActionPointValue.SetText($"{displayCurrent}/{displayMax}");
			// 数值刷新后，原消耗预览已失效，由调用方按当前选择重新设置。
			ClearHighlight();
		}

		public void SetHighlight(int count)
		{
			int displayCount = Mathf.Clamp(count, 0, Mathf.RoundToInt(_actionPointSlider.value));
			_actionPointSlider.SetHighlight(displayCount);
		}

		public void ClearHighlight()
		{
			_actionPointSlider.ClearHighlight();
		}
	}
}
