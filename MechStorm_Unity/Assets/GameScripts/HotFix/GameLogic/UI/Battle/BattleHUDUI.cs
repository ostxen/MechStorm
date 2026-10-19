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
		public bool IsReady { get; private set; }
		public bool ActionItemsReady { get; private set; }
		public event Action Closed;
		public event Action<int> ActionRequested;
		public event Action ConfirmRequested;
		public event Action CancelRequested;
		public event Action StandbyRequested;

		protected override void OnCreate()
		{
			if (m_bindComponent == null || _unitStatus == null || _combatInfo == null || _weapons == null ||
			    _abilities == null || _commands == null || _actionControl == null)
			{
				throw new GameFrameworkException("BattleHUDUI 的 Widget 绑定无效，请重新检查 Prefab 绑定。");
			}

			_abilities.Selected += index => ActionRequested?.Invoke(index);
			_actionControl.Confirmed += () => ConfirmRequested?.Invoke();
			_actionControl.Cancelled += () => CancelRequested?.Invoke();
			_actionControl.Standby += () => StandbyRequested?.Invoke();
			// 动态条目准备完毕并绑定 Presenter 前不开放输入。
			_combatInfo.Visible = false;
			_unitStatus.Visible = true;
			_weapons.Visible = false;
			_abilities.Visible = false;
			_commands.Visible = false;
			_actionControl.Visible = false;
			IsReady = true;
		}

		public async UniTask InitializeActionsAsync(int maxAbilityCount, CancellationToken cancellationToken)
		{
			using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken, gameObject.GetCancellationTokenOnDestroy());
			await _weapons.InitializeAsync(lifetime.Token);
			await _abilities.InitializeAsync(maxAbilityCount + 1, lifetime.Token);
			lifetime.Token.ThrowIfCancellationRequested();
			ActionItemsReady = true;
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
			if (IsReady)
			{
				_unitStatus.SetPilotName(pilotName);
			}
		}

		public void SetActionPoint(int current, int max)
		{
			if (IsReady)
			{
				_unitStatus.SetActionPoint(current, max);
			}
		}

		protected override void OnDestroy()
		{
			IsReady = false;
			ActionItemsReady = false;
			// TE 在根窗口 OnDestroy 前已销毁子 Widget，此处只解绑，不访问子节点。
			Closed?.Invoke();
			Closed = null;
			ActionRequested = null;
			ConfirmRequested = null;
			CancelRequested = null;
			StandbyRequested = null;
		}
	}
}
