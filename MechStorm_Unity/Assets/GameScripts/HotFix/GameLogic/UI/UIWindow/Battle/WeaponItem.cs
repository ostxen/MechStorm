using System;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
	public partial class WeaponItem
	{
		private Button _button;
		public event Action Clicked;

		protected override void BindMemberProperty()
		{
			_button = gameObject.GetComponent<Button>();
			if (_button == null)
			{
				throw new GameFrameworkException("WeaponItem 根节点缺少 Button。");
			}
		}

		protected override void RegisterEvent()
		{
			_button.onClick.AddListener(OnClick);
		}

		public void SetData(int damage, int minRange, int maxRange, bool interactable, bool selected)
		{
			m_tmpWeaponName.SetText("基础攻击");
			m_tmpAttackValue.SetText(damage.ToString());
			m_tmpAttackRangeValue.SetText($"{minRange}-{maxRange}");
			m_imgSelectedMark.gameObject.SetActive(selected);
			_button.interactable = interactable;
		}

		private void OnClick()
		{
			Clicked?.Invoke();
		}

		protected override void OnDestroy()
		{
			if (_button != null)
			{
				_button.onClick.RemoveListener(OnClick);
			}
			Clicked = null;
		}
	}
}
