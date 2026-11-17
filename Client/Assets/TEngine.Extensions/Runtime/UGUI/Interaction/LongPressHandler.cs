using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace TEngine.Extensions.UGUI
{
    [AddComponentMenu("TEngine/UI/Long Press Handler")]
    public sealed class LongPressHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, IPointerClickHandler
    {
        [Min(0)] public float durationThreshold = 1f;
        [Min(0)] public float startSpeed = 3f;
        [Min(0)] public float addSpeed = 2f;
        [Min(0)] public float maxValue = 25f;
        [Min(0)] public float callIntervalTime = 0.05f;
        public UnityEvent onClick;
        public IntEvent onLongPress;

        private bool _pointerDown;
        private bool _longPressTriggered;
        private bool _reachedMaxValue;
        private float _pressTime;
        private float _callTime;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _pointerDown = true;
            _longPressTriggered = false;
            _reachedMaxValue = false;
            _pressTime = 0;
            _callTime = 0;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _pointerDown = false;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _pointerDown = false;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && !_longPressTriggered)
            {
                onClick?.Invoke();
            }
        }

        private void Update()
        {
            if (!_pointerDown)
            {
                return;
            }

            if (!_longPressTriggered)
            {
                _pressTime += Time.unscaledDeltaTime;
                if (_pressTime < durationThreshold)
                {
                    return;
                }

                _longPressTriggered = true;
                _pressTime = 0;
                _callTime = 0;
                if (callIntervalTime <= 0)
                {
                    onLongPress?.Invoke(0);
                }

                return;
            }

            if (callIntervalTime <= 0)
            {
                return;
            }

            _pressTime += Time.unscaledDeltaTime;
            _callTime += Time.unscaledDeltaTime;
            if (_callTime < callIntervalTime)
            {
                return;
            }

            _callTime = 0;
            var value = _reachedMaxValue
                ? maxValue
                : ((startSpeed + startSpeed + addSpeed * _pressTime) * 0.5f) * _pressTime;
            if (value >= maxValue)
            {
                _reachedMaxValue = true;
                value = maxValue;
            }

            onLongPress?.Invoke(Mathf.FloorToInt(value));
        }

        private void OnDisable()
        {
            _pointerDown = false;
            _longPressTriggered = false;
            _pressTime = 0;
            _callTime = 0;
        }

        [System.Serializable]
        public sealed class IntEvent : UnityEvent<int>
        {
        }
    }
}
