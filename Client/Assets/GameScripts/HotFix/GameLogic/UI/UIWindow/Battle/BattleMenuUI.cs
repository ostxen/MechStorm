using System;
using Cysharp.Threading.Tasks;
using TEngine;

namespace GameLogic
{
	[Window(UILayer.UI, location : "BattleMenuUI")]
	public partial class BattleMenuUI
	{
		public event Action ExitRequested;
		public event Action RestartRequested;
		// 仅供镜头滚轮过滤；不改变遮罩的UI射线或关闭行为。
		public UnityEngine.GameObject CameraScrollPassthrough => m_btnClose.gameObject;

		protected override void OnDestroy()
		{
			ExitRequested = null;
			RestartRequested = null;
		}

		#region 事件

		private partial UniTaskVoid OnClickCloseBtn()
		{
			GameModule.UI.CloseUI<BattleMenuUI>();

			return default;
		}

		private partial UniTaskVoid OnClickSettingsBtn()
		{
			GameModule.UI.ShowUIAsync<SettingsUI>();

			return default;
		}

		private async partial UniTaskVoid OnClickAchievementBtn()
		{
			await UniTask.Yield();
		}

		private async partial UniTaskVoid OnClickDelegateBtn()
		{
			await UniTask.Yield();
		}

		private partial UniTaskVoid OnClickRestartBtn()
		{
			RestartRequested?.Invoke();

			return default;
		}

		private partial UniTaskVoid OnClickRetreatBtn()
		{
			ExitRequested?.Invoke();

			return default;
		}

		#endregion
	}
}
