using System;
using Cysharp.Threading.Tasks;

namespace GameLogic
{
	public partial class ActionButtonItem
	{
		public event Action Clicked;

		public void SetInteractable(bool interactable)
		{
			m_btnConfirm.interactable = interactable;
		}

		private partial UniTaskVoid OnClickConfirmBtn()
		{
			Clicked?.Invoke();
			return default;
		}

		protected override void OnDestroy()
		{
			Clicked = null;
		}
	}
}
