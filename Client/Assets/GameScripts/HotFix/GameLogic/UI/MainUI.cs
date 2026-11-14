using Cysharp.Threading.Tasks;
using TEngine;

namespace GameLogic
{
	[Window(UILayer.UI, location : "MainUI")]
	public partial class MainUI
	{
		private bool _ready;

		protected override void OnCreate()
		{
			if (m_bindComponent == null || m_btnPlay == null)
			{
				throw new GameFrameworkException("MainUI 的战斗按钮绑定无效，请检查 UIBindComponent。");
			}

			_ready = true;
			m_btnPlay.interactable = true;
			Log.Info("已进入主界面。");
		}

		protected override void OnDestroy()
		{
			_ready = false;
			if (m_btnPlay != null)
			{
				m_btnPlay.interactable = false;
			}
		}

		#region 事件

		private partial UniTaskVoid OnClickPlayBtn()
		{
			if (_ready && m_btnPlay.interactable)
			{
				m_btnPlay.interactable = false;
				GameEvent.Get<IGameProcedure>().RequestBattle();
			}

			return default;
		}

		#endregion
	}
}
