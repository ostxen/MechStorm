using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic
{
	[Window(UILayer.UI, location : "BattleHUDUI")]
	public partial class BattleHUDUI
	{
		private bool _displayEnabled;
		public event Action<int> ActionRequested;
		public event Action ConfirmRequested;
		public event Action CancelRequested;
		public event Action StandbyRequested;

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
			_abilities.Selected += index => ActionRequested?.Invoke(index);
			_actionControl.Confirmed += () => ConfirmRequested?.Invoke();
			_actionControl.Cancelled += () => CancelRequested?.Invoke();
			_actionControl.Standby += () => StandbyRequested?.Invoke();
		}

		protected override void OnCreate()
		{
			SetDisplayEnabled(false);
			// 动态条目准备完毕并绑定 Presenter 前不开放输入。
			_combatInfo.Visible = false;
			_unitStatus.Visible = true;
			_weapons.Visible = false;
			_abilities.Visible = false;
			_commands.Visible = false;
			_actionControl.Visible = false;
		}

		public async UniTask InitializeActionsAsync(int maxAbilityCount, CancellationToken cancellationToken)
		{
			await _weapons.InitializeAsync(cancellationToken);
			await _abilities.InitializeAsync(maxAbilityCount + 1, cancellationToken);
		}

		public void SetWeapon(int damage, int minRange, int maxRange, bool interactable, bool selected)
		{
			_weapons.SetData(damage, minRange, maxRange, interactable, selected);
		}

		public void SetAbilityCount(int count)
		{
			_abilities.SetCount(count);
			LayoutActionLists(count);
		}

		private void LayoutActionLists(int abilityCount)
		{
			// 武器和技能属于互斥阶段，共用右侧区域，不再按两列并排。
			const float spacing = 8f;
			var control = _actionControl.rectTransform;
			float controlTop = rectTransform.InverseTransformPoint(
				control.TransformPoint(new Vector3(0f, control.rect.yMax, 0f))).y;
			float height = abilityCount > 0 ? _abilities.GetContentHeight(abilityCount) : _weapons.ContentHeight;
			float top = Mathf.Min(rectTransform.rect.yMax - spacing, controlTop + spacing + height);
			float right = rectTransform.rect.xMax - spacing;
			PlaceList(_abilities.rectTransform, right, top);
			PlaceList(_weapons.rectTransform, right, top);
		}

		private void PlaceList(RectTransform list, float right, float top)
		{
			list.position = rectTransform.TransformPoint(
				new Vector3(right - list.rect.xMax, top - list.rect.yMax, 0f));
		}

		public void SetAbility(int index, string title, int cost, bool interactable, bool selected)
		{
			_abilities.SetItem(index, title, cost, interactable, selected);
		}

		public void SetActionLayout(bool enabled, bool selecting, bool hasTarget, bool showConfirm, bool canConfirm)
		{
			_weapons.Visible = enabled && !hasTarget;
			_abilities.Visible = enabled && hasTarget;
			_commands.Visible = false;
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
			ActionRequested = null;
			ConfirmRequested = null;
			CancelRequested = null;
			StandbyRequested = null;
		}
	}
}
