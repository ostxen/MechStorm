using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
	[Window(UILayer.System, location : "LoadingUI", fullScreen: true)]
	public partial class LoadingUI
	{
		private Slider _progressBar;
		public bool IsReady { get; private set; }

		protected override void OnCreate()
		{
			// 当前生成绑定只有文字，沿用 Prefab 现有进度条路径，不修改生成文件。
			_progressBar = FindChildComponent<Slider>("Slider_LoadingBar");
			if (m_bindComponent == null || m_tmpLoadingValue == null || _progressBar == null)
			{
				throw new GameFrameworkException("LoadingUI 的文字或进度条绑定无效。");
			}

			_progressBar.interactable = false;
			IsReady = true;
			SetProgress(0f);
		}

		/// <summary>
		/// 显示真实加载进度，参数范围为 0～1。
		/// </summary>
		public void SetProgress(float progress)
		{
			if (!IsReady)
			{
				return;
			}

			progress = Mathf.Clamp01(progress);
			_progressBar.SetValueWithoutNotify(Mathf.Lerp(_progressBar.minValue, _progressBar.maxValue, progress));
			m_tmpLoadingValue.SetText("Loading... {0}%", Mathf.RoundToInt(progress * 100f));
		}

		protected override void OnDestroy()
		{
			IsReady = false;
			_progressBar = null;
		}
	}
}
