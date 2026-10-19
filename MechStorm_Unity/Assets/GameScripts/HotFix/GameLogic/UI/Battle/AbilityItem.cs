using System;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
	public partial class AbilityItem
	{
		private Button _button;
		public event Action Clicked;

		protected override void BindMemberProperty()
		{
			_button = gameObject.GetComponent<Button>();
			if (_button == null)
			{
				throw new GameFrameworkException("AbilityItem 根节点缺少 Button。");
			}
			_button.onClick.AddListener(OnClick);
		}

		public void SetData(string title, int actionPointCost, bool interactable, bool selected)
		{
			m_tmpAbilityName.SetText(title);
			m_tmpAPCostValue.SetText(actionPointCost.ToString());
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
