using System;
using Cysharp.Threading.Tasks;

namespace GameLogic
{
	public partial class AbilityConfirmItem
	{
		public event Action Confirmed;
		public event Action Cancelled;

		protected override void RegisterEvent()
		{
			_attack.Clicked += OnConfirm;
		}

		public void SetState(bool showConfirm, bool canConfirm)
		{
			_attack.Visible = showConfirm;
			_attack.SetInteractable(canConfirm);
		}

		private void OnConfirm()
		{
			Confirmed?.Invoke();
		}

		private partial UniTaskVoid OnClickCancelBtn()
		{
			Cancelled?.Invoke();
			return default;
		}

		protected override void OnDestroy()
		{
			Confirmed = null;
			Cancelled = null;
		}
	}
}
