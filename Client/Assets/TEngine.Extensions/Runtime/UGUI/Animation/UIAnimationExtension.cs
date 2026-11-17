using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TEngine.Extensions.UGUI
{
    public static class UIAnimationExtension
    {
        public static async UniTask FadeToAlphaAsync(this CanvasGroup target, float alpha, float duration,
            bool unscaledTime = true, Action completed = null, CancellationToken cancellationToken = default)
        {
            if (target == null)
            {
                return;
            }

            var start = target.alpha;
            await LerpAsync(duration, unscaledTime,
                value => target.alpha = Mathf.Lerp(start, alpha, value), cancellationToken);
            if (target == null)
            {
                return;
            }

            target.alpha = alpha;
            completed?.Invoke();
        }

        public static async UniTask ColorToAsync(this Graphic target, Color color, float duration,
            bool unscaledTime = true, Action completed = null, CancellationToken cancellationToken = default)
        {
            if (target == null)
            {
                return;
            }

            var start = target.color;
            await LerpAsync(duration, unscaledTime,
                value => target.color = Color.LerpUnclamped(start, color, value), cancellationToken);
            if (target == null)
            {
                return;
            }

            target.color = color;
            completed?.Invoke();
        }

        public static async UniTask FillToAsync(this Image target, float value, float duration,
            bool unscaledTime = true, Action completed = null, CancellationToken cancellationToken = default)
        {
            if (target == null)
            {
                return;
            }

            var start = target.fillAmount;
            await LerpAsync(duration, unscaledTime,
                progress => target.fillAmount = Mathf.Lerp(start, value, progress), cancellationToken);
            if (target == null)
            {
                return;
            }

            target.fillAmount = value;
            completed?.Invoke();
        }

        public static async UniTask ValueToAsync(this Slider target, float value, float duration,
            bool unscaledTime = true, Action completed = null, CancellationToken cancellationToken = default)
        {
            if (target == null)
            {
                return;
            }

            var start = target.value;
            await LerpAsync(duration, unscaledTime,
                progress => target.value = Mathf.Lerp(start, value, progress), cancellationToken);
            if (target == null)
            {
                return;
            }

            target.value = value;
            completed?.Invoke();
        }

        private static async UniTask LerpAsync(float duration, bool unscaledTime, Action<float> update,
            CancellationToken cancellationToken)
        {
            if (duration <= 0f)
            {
                update(1f);
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                update(Mathf.Clamp01(elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }
}
