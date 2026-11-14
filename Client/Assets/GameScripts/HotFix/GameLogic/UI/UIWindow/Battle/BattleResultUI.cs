using System;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine.UI;

namespace GameLogic
{
	[Window(UILayer.UI, location : "BattleResultUI")]
	public partial class BattleResultUI
	{
		public event Action StatisticsRequested;
		public event Action ExitRequested;
		public event Action RestartRequested;
		private Button _backgroundButton;

		protected override void BindMemberProperty()
		{
			_backgroundButton = m_rimgBackground.GetComponent<Button>();
		}

		protected override void RegisterEvent()
		{
			_backgroundButton.onClick.AddListener(OnClickBackground);
		}

		/// <summary>
		/// 按玩家阵营映射显示胜败；失败时额外显示失败专属按钮区，胜利时全部隐藏。
		/// </summary>
		public void ShowOutcome(bool isVictory)
		{
			m_tmpVictoryText.gameObject.SetActive(isVictory);
			m_tmpFailureText.gameObject.SetActive(!isVictory);
			m_rectFailureContainer.gameObject.SetActive(!isVictory);
			_backgroundButton.interactable = isVictory;
		}

		private void OnClickBackground()
		{
			ExitRequested?.Invoke();
		}

		protected override void OnDestroy()
		{
			_backgroundButton.onClick.RemoveListener(OnClickBackground);
			StatisticsRequested = null;
			ExitRequested = null;
			RestartRequested = null;
		}

		#region 事件

		private partial UniTaskVoid OnClickStatisticBtn()
		{
			StatisticsRequested?.Invoke();

			return default;
		}

		private async partial UniTaskVoid OnClickDelegateBtn()
		{
			await UniTask.Yield();
		}

		private async partial UniTaskVoid OnClickTutorialBtn()
		{
			await UniTask.Yield();
		}

		private partial UniTaskVoid OnClickRetreatBtn()
		{
			ExitRequested?.Invoke();

			return default;
		}

		private partial UniTaskVoid OnClickAgainBtn()
		{
			RestartRequested?.Invoke();

			return default;
		}

		#endregion
	}
}
