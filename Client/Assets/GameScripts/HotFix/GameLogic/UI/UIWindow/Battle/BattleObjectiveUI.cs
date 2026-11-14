using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TEngine;

namespace GameLogic
{
	[Window(UILayer.Top, location : "BattleObjectiveUI", fullScreen : true)]
	public partial class BattleObjectiveUI
	{
		private const float FadeDuration = 0.3f;
		private const float HoldDuration = 0.5f;
		private CanvasGroup _fade;

		protected override void BindMemberProperty()
		{
			_fade = gameObject.GetComponent<CanvasGroup>();
			if (_fade == null)
			{
				_fade = gameObject.AddComponent<CanvasGroup>();
			}
		}

		protected override void OnCreate()
		{
			_fade.alpha = 0f;
			_fade.interactable = false;
		}

		public async UniTask PlayAsync(string objective, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			m_tmpObjectiveText.text = objective;
			_fade.alpha = 0f;
			float elapsed = 0f;
			while (elapsed < FadeDuration)
			{
				await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
				elapsed += Time.unscaledDeltaTime;
				_fade.alpha = Mathf.Clamp01(elapsed / FadeDuration);
			}
			_fade.alpha = 1f;
			await UniTask.Delay(TimeSpan.FromSeconds(HoldDuration), ignoreTimeScale: true, cancellationToken: cancellationToken);
		}
	}
}
