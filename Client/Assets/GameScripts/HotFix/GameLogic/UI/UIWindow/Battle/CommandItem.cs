using System;
using TMPro;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
	public partial class CommandItem
	{
		private Button _button;
		private TextMeshProUGUI _name;
		private UnityEngine.GameObject _selected;
		public event Action Clicked;

		protected override void BindMemberProperty()
		{
			_button = gameObject.GetComponent<Button>();
			_name = transform.Find("CommandName").GetComponent<TextMeshProUGUI>();
			_selected = transform.Find("SelectedMark").gameObject;
		}

		protected override void RegisterEvent() => _button.onClick.AddListener(OnClick);
		private void OnClick() => Clicked?.Invoke();

		public void SetData(string title, int cost, bool interactable, bool selected)
		{
			_name.SetText(title);
			m_tmpActionPointCostValue.SetText(cost.ToString());
			m_imgAbilityIcon.gameObject.SetActive(false);
			_selected.SetActive(selected);
			_button.interactable = interactable;
		}

		protected override void OnDestroy()
		{
			_button.onClick.RemoveListener(OnClick);
			Clicked = null;
		}
	}
}
