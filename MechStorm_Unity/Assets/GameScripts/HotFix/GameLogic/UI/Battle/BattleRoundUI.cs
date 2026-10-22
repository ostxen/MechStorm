using System;
using System.Threading;
using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TEngine;

namespace GameLogic
{
	[Window(UILayer.Top, location : "BattleRoundUI", fullScreen : true)]
	public partial class BattleRoundUI
	{
		private const float SlideDuration = 0.3f;
		private const float HoldDuration = 0.5f;
		private Vector2 _restPosition;
		private TextMeshProUGUI[] _labels;
		private Color[] _defaultColors;

		protected override void OnCreate()
		{
			_restPosition = m_rectRoundInfo.anchoredPosition;
			_labels = m_rectRoundInfo.GetComponentsInChildren<TextMeshProUGUI>(true);
			_defaultColors = new Color[_labels.Length];
			for (int i = 0; i < _labels.Length; i++)
			{
				_defaultColors[i] = _labels[i].color;
			}
			m_rectRoundInfo.anchoredPosition = GetOffscreenPosition();
		}

		private Vector2 GetOffscreenPosition()
		{
			// 终点保留 Prefab 原布局；只将起点推到父矩形右边界以外。
			m_rectRoundInfo.anchoredPosition = _restPosition;
			var parent = (RectTransform)m_rectRoundInfo.parent;
			var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, m_rectRoundInfo);
			float offset = Mathf.Max(0f, parent.rect.xMax - bounds.min.x) + 1f;
			return _restPosition + Vector2.right * offset;
		}

		public async UniTask PlayAsync(int roundNumber, bool isPlayerTurn, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			m_tmpRoundNum.text = roundNumber.ToString("D2");
			m_tmpFactionRoundText.text = isPlayerTurn ? "我方回合" : "敌方回合";
			for (int i = 0; i < _labels.Length; i++)
			{
				Color original = _defaultColors[i];
				_labels[i].color = isPlayerTurn ? original : new Color(1f, 0f, 0f, original.a);
			}
			Vector2 start = GetOffscreenPosition();
			m_rectRoundInfo.anchoredPosition = start;
			float elapsed = 0f;
			while (elapsed < SlideDuration)
			{
				await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
				elapsed += Time.unscaledDeltaTime;
				float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SlideDuration));
				m_rectRoundInfo.anchoredPosition = Vector2.LerpUnclamped(start, _restPosition, progress);
			}
			m_rectRoundInfo.anchoredPosition = _restPosition;
			await UniTask.Delay(TimeSpan.FromSeconds(HoldDuration), ignoreTimeScale: true, cancellationToken: cancellationToken);
		}
	}
}
