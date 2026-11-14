using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TEngine;
using TMPro;
using UnityEngine.UI;

namespace GameLogic
{
	[Window(UILayer.UI, location : "BattleHUDUI")]
	public partial class BattleHUDUI
	{
		private bool _displayEnabled;
		private WeaponItem _weaponItem;
		private readonly List<AbilityItem> _abilityItems = new();
		private readonly List<CommandItem> _commandItems = new();
		public event Action<int> ActionRequested;
		public event Action<int> CommandRequested;
		public event Action ConfirmRequested;
		public event Action CancelRequested;
		public event Action StandbyRequested;
		public event Action MenuRequested;
		public event Action EndFactionTurnRequested;
		private Button _menu;
		private Button _endFactionTurn;
		private TextMeshProUGUI _endFactionTurnLabel;
		private TextMeshProUGUI _roundFaction;
		private TextMeshProUGUI _objective;

		protected override void BindMemberProperty()
		{
			_roundFaction = m_rectBattleStateRegion.Find("m_tmpRoundFaction").GetComponent<TextMeshProUGUI>();
			_objective = m_rectBattleStateRegion.Find("m_tmpObjective").GetComponent<TextMeshProUGUI>();
			_menu = m_rectBattleAuxiliaryRegion.Find("m_btnMenu").GetComponent<Button>();
			_endFactionTurn = transform.Find("FactionTurnControlRegion/m_btnEndFactionTurn").GetComponent<Button>();
			_endFactionTurnLabel = _endFactionTurn.transform.Find("Label").GetComponent<TextMeshProUGUI>();
		}

		public void SetDisplayEnabled(bool enabled)
		{
			_displayEnabled = enabled;
			Visible = enabled;
		}

		protected override void OnSetVisible(bool visible)
		{
			// TE 开关其他窗口时会重算显隐，预备中的 HUD 不能被自动显示。
			if (visible && !_displayEnabled)
			{
				Visible = false;
			}
		}

		protected override void RegisterEvent()
		{
			_menu.onClick.AddListener(OnMenu);
			_endFactionTurn.onClick.AddListener(OnEndFactionTurn);
			_actionControl.Confirmed += () => ConfirmRequested?.Invoke();
			_actionControl.Cancelled += () => CancelRequested?.Invoke();
			_actionControl.Standby += () => StandbyRequested?.Invoke();
		}

		private void OnMenu() => MenuRequested?.Invoke();
		private void OnEndFactionTurn() => EndFactionTurnRequested?.Invoke();

		public void SetEndFactionTurn(bool interactable, string factionName)
		{
			_endFactionTurn.interactable = interactable;
			_endFactionTurnLabel.text = $"结束{factionName}回合";
		}

		public void SetBattleState(int roundNumber, string factionName)
		{
			_roundFaction.text = $"回合 {roundNumber:00}    /    {factionName}";
		}

		public void SetObjective(string description)
		{
			_objective.text = $"战斗目标：{description}";
		}

		protected override void OnCreate()
		{
			SetDisplayEnabled(false);
			transform.Find("CameraControls").gameObject.SetActive(false);
			_menu.interactable = true;
			_menu.transform.Find("Availability").gameObject.SetActive(false);
			// 动态条目准备完毕并绑定 Presenter 前不开放输入。
			_combatInfo.Visible = false;
			m_rectBattleStateRegion.gameObject.SetActive(true);
			m_rectBattleAuxiliaryRegion.gameObject.SetActive(true);
			_unitStatus.Visible = true;
			m_rectWeapons.gameObject.SetActive(false);
			m_rectAbilities.gameObject.SetActive(false);
			m_rectCommands.gameObject.SetActive(false);
			_actionControl.Visible = false;
		}

		public async UniTask InitializeActionsAsync(int maxAbilityCount)
		{
			_weaponItem = await CreateWidgetByTypeAsync<WeaponItem>(m_rectWeapons, false);
			for (int i = _abilityItems.Count; i < maxAbilityCount + 1; i++)
			{
				var item = await CreateWidgetByTypeAsync<AbilityItem>(m_rectAbilities, false);
				int index = i;
				item.Clicked += () => ActionRequested?.Invoke(index);
				_abilityItems.Add(item);
			}
			for (int i = _commandItems.Count; i < maxAbilityCount; i++)
			{
				var item = await CreateWidgetByTypeAsync<CommandItem>(m_rectCommands, false);
				int index = i;
				item.Clicked += () => CommandRequested?.Invoke(index);
				_commandItems.Add(item);
			}
		}

		public void SetCommandCount(int count)
		{
			for (int i = 0; i < _commandItems.Count; i++)
			{
				_commandItems[i].Visible = i < count;
			}
		}

		public void SetCommand(int index, string title, int cost, bool interactable, bool selected)
		{
			_commandItems[index].SetData(title, cost, interactable, selected);
		}

		public void SetWeapon(int damage, int minRange, int maxRange, bool interactable, bool selected)
		{
			_weaponItem.Visible = true;
			_weaponItem.SetData(damage, minRange, maxRange, interactable, selected);
		}

		public void SetAbilityCount(int count)
		{
			if (count < 0 || count > _abilityItems.Count)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}
			for (int i = 0; i < _abilityItems.Count; i++)
			{
				_abilityItems[i].Visible = i < count;
			}
		}

		public void SetAbility(int index, string title, int cost, bool interactable, bool selected)
		{
			_abilityItems[index].SetData(title, cost, interactable, selected);
		}

		public void SetActionLayout(bool enabled, bool selecting, bool hasTarget, bool showConfirm, bool canConfirm, bool commandSelected = false)
		{
			m_rectWeapons.gameObject.SetActive(enabled && !hasTarget && !commandSelected);
			m_rectAbilities.gameObject.SetActive(enabled && hasTarget && !commandSelected);
			m_rectCommands.gameObject.SetActive(enabled && (!hasTarget || commandSelected));
			_actionControl.Visible = enabled;
			_actionControl.SetState(selecting, showConfirm, canConfirm);
		}

		public void SetActionPointPreview(int cost)
		{
			_unitStatus.SetActionPointPreview(cost);
		}

		public void SetCombatInfo(bool visible, string actorName, int actorAP, int actorMaxAP,
			string targetName, int targetAP, int targetMaxAP, int cost)
		{
			_combatInfo.Visible = visible;
			_unitStatus.Visible = !visible;
			m_rectBattleStateRegion.gameObject.SetActive(!visible);
			m_rectBattleAuxiliaryRegion.gameObject.SetActive(!visible);
			if (visible)
			{
				_combatInfo.SetUnits(actorName, actorAP, actorMaxAP, targetName, targetAP, targetMaxAP, cost);
			}
		}

		public void SetPilotName(string pilotName)
		{
			_unitStatus.SetPilotName(pilotName);
		}

		public void SetActionPoint(int current, int max)
		{
	
			_unitStatus.SetActionPoint(current, max);
			
		}

		protected override void OnDestroy()
		{
			_menu.onClick.RemoveListener(OnMenu);
			_endFactionTurn.onClick.RemoveListener(OnEndFactionTurn);
			MenuRequested = null;
			EndFactionTurnRequested = null;
			// 子 Widget 已由 TE 回收，此处只清除窗口持有的引用。
			_weaponItem = null;
			_abilityItems.Clear();
			_commandItems.Clear();
			ActionRequested = null;
			CommandRequested = null;
			ConfirmRequested = null;
			CancelRequested = null;
			StandbyRequested = null;
		}
	}
}
