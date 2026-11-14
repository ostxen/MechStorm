using Cysharp.Threading.Tasks;
using TEngine;

namespace GameLogic
{
	[Window(UILayer.UI, location : "StartUI")]
	public partial class StartUI
	{
		private bool _ready;

		protected override void OnCreate()
		{
			if (m_bindComponent == null || m_btn_Start == null)
			{
				throw new GameFrameworkException("StartUI 的开始按钮绑定无效，请检查 UIBindComponent。");
			}

			_ready = true;
			m_btn_Start.interactable = true;
		}

		protected override void OnDestroy()
		{
			_ready = false;
		}

		#region 事件

		private partial UniTaskVoid OnClick_StartBtn()
		{
			if (!_ready || !m_btn_Start.interactable)
			{
				return default;
			}

			m_btn_Start.interactable = false;
			GameEvent.Get<IGameProcedure>().RequestStartGame();
			return default;
		}

		#endregion
	}
}
