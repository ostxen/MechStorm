using System;
using TEngine.Extensions.RedDot;
using UnityEngine;
using UnityEngine.UI;

namespace TEngine.Extensions.RedDot.View
{
    [AddComponentMenu("TEngine/Red Dot/View")]
    public sealed class RedDotView : MonoBehaviour
    {
        [SerializeField] private RedDotRegistryComponent registry;
        [SerializeField] private string key;
        [SerializeField] private Graphic indicator;
        [SerializeField] private Graphic newIndicator;
        [SerializeField] private Text countText;
        [SerializeField] private bool showCount;

        private IDisposable _subscription;

        private void OnEnable()
        {
            if (registry == null)
            {
                registry = GetComponentInParent<RedDotRegistryComponent>();
            }

            Bind();
        }

        private void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        public void Bind(RedDotRegistryComponent target, string redNoteKey)
        {
            _subscription?.Dispose();
            registry = target;
            key = redNoteKey;
            Bind();
        }

        private void Bind()
        {
            if (registry == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            _subscription = registry.Registry.Subscribe(key, ApplyState);
        }

        private void ApplyState(RedDotState state)
        {
            var visible = state.IsActive;
            if (indicator != null)
            {
                indicator.enabled = visible &&
                                   (state.DisplayType == RedDotDisplayType.Dot ||
                                    state.DisplayType == RedDotDisplayType.Custom);
            }

            if (newIndicator != null)
            {
                newIndicator.enabled = visible && state.DisplayType == RedDotDisplayType.New;
            }

            if (countText != null)
            {
                var showNumber = showCount &&
                                 state.DisplayType == RedDotDisplayType.Number &&
                                 visible;
                countText.text = showNumber ? state.Count.ToString() : string.Empty;
                countText.enabled = showNumber;
            }
        }
    }
}
