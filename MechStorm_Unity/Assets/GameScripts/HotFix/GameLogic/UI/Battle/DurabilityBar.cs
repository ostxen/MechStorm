using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
	/// <summary>
	/// 单条部位耐久条原子组件：只负责数值展示，不持有部位语义。
	/// 部位标签由所属容器持有（双臂共享一个标签）。
	/// 根节点即 Slider，生成器只收集子节点，故 Slider 在此显式获取。
	/// </summary>
	public partial class DurabilityBar
	{
		private Slider _durabilitySlider;

		protected override void OnCreate()
		{
			_durabilitySlider = gameObject.GetComponent<Slider>();
		}

		public void SetValue(int current, int max)
		{
			if (_durabilitySlider != null)
			{
				_durabilitySlider.maxValue = max;
				_durabilitySlider.SetValueWithoutNotify(current);
			}

			if (m_tmp_DurabilityValue != null)
			{
				m_tmp_DurabilityValue.SetText($"{current}/{max}");
			}
		}
	}
}
