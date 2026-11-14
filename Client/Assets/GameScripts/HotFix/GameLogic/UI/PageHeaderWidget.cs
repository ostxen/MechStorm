using System;
using Cysharp.Threading.Tasks;

namespace GameLogic
{
	public partial class PageHeaderWidget
	{
		/// <summary>
		/// 页头返回按钮点击通知，由所属窗口决定返回行为。
		/// </summary>
		public event Action BackRequested;
		public event Action HomeRequested;

		public void SetNavigationInteractable(bool interactable)
		{
			m_btnBack.interactable = interactable;
			m_btnHome.interactable = interactable;
		}

		protected override void OnDestroy()
		{
			BackRequested = null;
			HomeRequested = null;
		}

		#region 事件

		private partial UniTaskVoid OnClickBackBtn()
		{
			BackRequested?.Invoke();

			return default;
		}

		private partial UniTaskVoid OnClickHomeBtn()
		{
			HomeRequested?.Invoke();
			return default;
		}

		#endregion
	}
}
