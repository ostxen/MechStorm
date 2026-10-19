using System;

namespace GameLogic
{
	public partial class BattleActionControlWidget
	{
		public event Action Standby;
		public event Action Confirmed;
		public event Action Cancelled;

		protected override void OnCreate()
		{
			_standby.Clicked += () => Standby?.Invoke();
			_abilityConfirm.Confirmed += () => Confirmed?.Invoke();
			_abilityConfirm.Cancelled += () => Cancelled?.Invoke();
			_commandOperation.Visible = false;
		}

		public void SetState(bool selecting, bool showConfirm, bool canConfirm)
		{
			_standby.Visible = !selecting;
			_standby.SetInteractable(!selecting);
			_abilityConfirm.Visible = selecting;
			_abilityConfirm.SetState(showConfirm, canConfirm);
			// 当前配置全部技能走武器技能路径，没有角色指令。
			_commandOperation.Visible = false;
		}

		protected override void OnDestroy()
		{
			Standby = null;
			Confirmed = null;
			Cancelled = null;
		}
	}
}
