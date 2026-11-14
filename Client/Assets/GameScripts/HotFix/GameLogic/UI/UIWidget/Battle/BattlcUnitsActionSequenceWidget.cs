using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
	public partial class BattlcUnitsActionSequenceWidget
	{
		private Button _toggleButton;

		public RectTransform Content => m_rectContent;

		protected override void BindMemberProperty()
		{
			_toggleButton = gameObject.GetComponent<Button>();
			_toggleButton.targetGraphic = gameObject.GetComponent<Image>();
			_toggleButton.transition = Selectable.Transition.None;
			var navigation = _toggleButton.navigation;
			navigation.mode = Navigation.Mode.None;
			_toggleButton.navigation = navigation;
		}

		protected override void RegisterEvent()
		{
			_toggleButton.onClick.AddListener(ToggleExpanded);
		}

		protected override void OnCreate()
		{
			SetExpanded(false);
			SetInteractable(false);
		}

		public void SetInteractable(bool interactable)
		{
			_toggleButton.interactable = interactable;
		}

		public void AddUnit(GameObject itemObject, int order, string pilotName)
		{
			CreateWidget<BattlePreparationSequenceItem>(itemObject).SetData(order, pilotName);
		}

		private void ToggleExpanded()
		{
			SetExpanded(!m_rectContent.gameObject.activeSelf);
		}

		private void SetExpanded(bool expanded)
		{
			m_rectContent.gameObject.SetActive(expanded);
			m_rectTipDesc.gameObject.SetActive(expanded);
			m_imgArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, expanded ? 180f : 0f);
		}

		protected override void OnDestroy()
		{
			_toggleButton.onClick.RemoveListener(ToggleExpanded);
		}
	}

	public sealed class BattlePreparationSequenceItem : UIWidget
	{
		private TextMeshProUGUI _sequenceNumber;
		private TextMeshProUGUI _pilotInitial;
		private TextMeshProUGUI _pilotName;

		protected override void BindMemberProperty()
		{
			_sequenceNumber = FindChildComponent<TextMeshProUGUI>("SequenceNumber");
			_pilotInitial = FindChildComponent<TextMeshProUGUI>("PliotSlot/PilotInitial");
			_pilotName = FindChildComponent<TextMeshProUGUI>("PilotName");
		}

		public void SetData(int order, string pilotName)
		{
			_sequenceNumber.text = order.ToString();
			_pilotInitial.text = pilotName.Substring(0, 1);
			_pilotName.text = pilotName;
		}
	}
}
