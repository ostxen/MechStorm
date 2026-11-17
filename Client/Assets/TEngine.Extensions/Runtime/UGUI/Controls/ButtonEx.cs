using System;
using TEngine.Extensions.UGUI.Feedback;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TEngine.Extensions.UGUI
{
    [AddComponentMenu("TEngine/UI/ButtonEx")]
    [DisallowMultipleComponent]
    public class ButtonEx : Button, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Click")]
        public bool canClick = true;
        public bool canDoubleClick;
        [Min(0)] public float doubleClickInterval = 0.1f;
        public ButtonClickedEvent onDoubleClick;

        [Header("Long Press")]
        public bool canLongPress;
        public bool responseOnceByPress;
        [Min(0)] public float longPressDuration = 1f;
        [Min(0)] public float longPressInterval = 0.05f;
        public ButtonClickedEvent onPress;

        [Header("Drag")]
        public PointerEvent onBeginDrag;
        public PointerEvent onDrag;
        public PointerEvent onEndDrag;

        private bool _isDown;
        private bool _isPress;
        private bool _isDownExit;
        private bool _isDragging;
        private int _pointerId = int.MinValue;
        private float _pressTime;
        private float _longPressCallTime;
        private float _clickTime;
        private int _clickCount;

        public bool IsDragging => _isDragging;
        public int PointerId => _pointerId;
        public bool IsDown => _isDown;
        public bool IsPress => _isPress;
        public bool IsDownExit => _isDownExit;

        protected override void OnDisable()
        {
            base.OnDisable();
            ResetInteractionState();
        }

        private void Update()
        {
            UpdateLongPress();
            UpdateClick();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            PlayFeedback(UIAudioCue.PointerEnter);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsLeftButton(eventData) || _isDragging)
            {
                return;
            }

            base.OnPointerDown(eventData);
            _pointerId = eventData.pointerId;
            _isDown = true;
            _isPress = false;
            _isDownExit = false;
            _pressTime = 0;
            _longPressCallTime = 0;
            PlayFeedback(UIAudioCue.PointerDown);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (!IsPointerOwner(eventData))
            {
                return;
            }

            base.OnPointerUp(eventData);
            _isDown = false;
            _isDownExit = true;
            PlayFeedback(UIAudioCue.PointerUp);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            if (!IsPointerOwner(eventData))
            {
                return;
            }

            base.OnPointerExit(eventData);
            _isPress = false;
            _isDownExit = true;
            PlayFeedback(UIAudioCue.PointerExit);
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (!IsLeftButton(eventData) || !IsPointerOwner(eventData))
            {
                return;
            }

            if (_isPress)
            {
                _isPress = false;
                _clickCount = 0;
                return;
            }

            _clickCount++;
            _clickTime = 0;
            PlayFeedback(UIAudioCue.Click);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsPointerOwner(eventData))
            {
                return;
            }

            _isDragging = true;
            onBeginDrag?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsPointerOwner(eventData))
            {
                return;
            }

            PlayFeedback(UIAudioCue.Drag);
            onDrag?.Invoke(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsPointerOwner(eventData))
            {
                return;
            }

            _isDragging = false;
            onEndDrag?.Invoke(eventData);
        }

        private void PlayFeedback(UIAudioCue cue)
        {
            UIFeedback.Raise(this, cue);
        }

        private void UpdateLongPress()
        {
            if (!_isDown || !canLongPress)
            {
                return;
            }

            if (responseOnceByPress && _isPress)
            {
                return;
            }

            _pressTime += Time.unscaledDeltaTime;
            if (!_isPress)
            {
                if (_pressTime < longPressDuration)
                {
                    return;
                }

                _isPress = true;
                _longPressCallTime = 0;
                onPress?.Invoke();
                return;
            }

            if (longPressInterval <= 0)
            {
                onPress?.Invoke();
                return;
            }

            _longPressCallTime += Time.unscaledDeltaTime;
            if (_longPressCallTime >= longPressInterval)
            {
                _longPressCallTime = 0;
                onPress?.Invoke();
            }
        }

        private void UpdateClick()
        {
            if (_clickCount <= 0)
            {
                return;
            }

            if (!canDoubleClick && !canLongPress)
            {
                InvokeSingleClick();
                return;
            }

            _clickTime += Time.unscaledDeltaTime;
            if (_clickCount >= 2)
            {
                if (canDoubleClick)
                {
                    onDoubleClick?.Invoke();
                }

                ResetClickState();
                return;
            }

            if (_clickTime >= doubleClickInterval)
            {
                InvokeSingleClick();
            }
        }

        private void InvokeSingleClick()
        {
            if (canClick)
            {
                onClick?.Invoke();
            }

            ResetClickState();
        }

        private void ResetClickState()
        {
            _clickCount = 0;
            _clickTime = 0;
        }

        private void ResetInteractionState()
        {
            _isDown = false;
            _isPress = false;
            _isDownExit = false;
            _isDragging = false;
            _pointerId = int.MinValue;
            _pressTime = 0;
            _longPressCallTime = 0;
            ResetClickState();
        }

        private bool IsPointerOwner(PointerEventData eventData)
        {
            return IsLeftButton(eventData) &&
                   (_pointerId == int.MinValue || _pointerId == eventData.pointerId);
        }

        private static bool IsLeftButton(PointerEventData eventData)
        {
            return eventData != null && eventData.button == PointerEventData.InputButton.Left;
        }

        [Serializable]
        public sealed class PointerEvent : UnityEvent<PointerEventData>
        {
        }
    }
}
