using System;
using System.Collections.Generic;
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
		private WeaponItem _weaponItem;
		private readonly List<AbilityItem> _abilityItems = new();
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
			_actionControl.Confirmed += () => ConfirmRequested?.Invoke();
			_actionControl.Cancelled += () => CancelRequested?.Invoke();
			_actionControl.Standby += () => StandbyRequested?.Invoke();
		}

		protected override void OnCreate()
		{
			SetDisplayEnabled(false);
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

		public async UniTask InitializeActionsAsync(int maxAbilityCount, CancellationToken cancellationToken)
		{
			if (_weaponItem == null)
			{
				_weaponItem = await LoadActionItemAsync<WeaponItem>("WeaponItem", m_rectWeapons, cancellationToken);
			}
			for (int i = _abilityItems.Count; i < maxAbilityCount + 1; i++)
			{
				var item = await LoadActionItemAsync<AbilityItem>("AbilityItem", m_rectAbilities, cancellationToken);
				int index = i;
				item.Clicked += () => ActionRequested?.Invoke(index);
				_abilityItems.Add(item);
			}
		}

		private async UniTask<T> LoadActionItemAsync<T>(string location, RectTransform parent,
			CancellationToken cancellationToken) where T : UIWidget, new()
		{
			if (!GameModule.Resource.CheckLocationValid(location))
			{
				throw new GameFrameworkException($"HUD 条目资源地址无效：{location}");
			}
			cancellationToken.ThrowIfCancellationRequested();
			var go = await UIModule.Resource.LoadGameObjectAsync(location, parent, cancellationToken);
			// 加载可能晚于窗口关闭完成，此时不再向旧窗口注册条目。
			if (cancellationToken.IsCancellationRequested || IsDestroyed || gameObject == null)
			{
				if (go != null)
				{
					UnityEngine.Object.Destroy(go);
				}
				throw new OperationCanceledException(cancellationToken);
			}
			if (go == null)
			{
				throw new GameFrameworkException($"HUD 条目加载失败：{location}");
			}
			var item = CreateWidget<T>(go, false);
			if (item == null)
			{
				UnityEngine.Object.Destroy(go);
				throw new GameFrameworkException($"HUD 条目创建失败：{location}");
			}
			return item;
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

		public void SetActionLayout(bool enabled, bool selecting, bool hasTarget, bool showConfirm, bool canConfirm)
		{
			m_rectWeapons.gameObject.SetActive(enabled && !hasTarget);
			m_rectAbilities.gameObject.SetActive(enabled && hasTarget);
			m_rectCommands.gameObject.SetActive(false);
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
			// 子 Widget 已由 TE 回收，此处只清除窗口持有的引用。
			_weaponItem = null;
			_abilityItems.Clear();
			ActionRequested = null;
			ConfirmRequested = null;
			CancelRequested = null;
			StandbyRequested = null;
		}
	}
}
