using System;
using Cysharp.Threading.Tasks;

namespace GameLogic
{
	public partial class BattleActionControlWidget
	{
		public event Action Standby;
		public event Action Confirmed;
		public event Action Cancelled;

		protected override void RegisterEvent()
		{
			_standby.Clicked += () => Standby?.Invoke();
			_attack.Clicked += () => Confirmed?.Invoke();
		}

		protected override void OnCreate()
		{
			_commandOperation.Visible = false;
		}

		public void SetState(bool selecting, bool showConfirm, bool canConfirm)
		{
			_standby.Visible = !selecting;
			_standby.SetInteractable(!selecting);
			m_goAbilityConfirm.SetActive(selecting);
			_attack.Visible = showConfirm;
			_attack.SetInteractable(canConfirm);
			// 当前配置全部技能走武器技能路径，没有角色指令。
			_commandOperation.Visible = false;
		}

		private partial UniTaskVoid OnClickCancelBtn()
		{
			Cancelled?.Invoke();
			return default;
		}

		protected override void OnDestroy()
		{
			Standby = null;
			Confirmed = null;
			Cancelled = null;
		}
	}
}
