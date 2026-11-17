using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TEngine.Extensions.VirtualizedList
{
    public sealed class VirtualizedItemInteraction :
        MonoBehaviour,
        IPointerClickHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        public float LongPressSeconds = 0.5f;
        public event Action<int> Clicked;
        public event Action<int> LongPressed;

        private bool _pressed;
        private bool _longPressed;
        private float _pressedAt;

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            _longPressed = false;
            _pressedAt = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_longPressed)
            {
                Clicked?.Invoke(GetIndex());
            }
        }

        private void Update()
        {
            if (_pressed &&
                !_longPressed &&
                Time.unscaledTime - _pressedAt >= LongPressSeconds)
            {
                _longPressed = true;
                LongPressed?.Invoke(GetIndex());
            }
        }

        private int GetIndex()
        {
            var listItem = GetComponentInParent<VirtualizedListItem>();
            if (listItem != null)
            {
                return listItem.CurrentIndex;
            }

            var gridItem = GetComponentInParent<VirtualizedGridItem>();
            return gridItem == null ? -1 : gridItem.CurrentIndex;
        }
    }

    public sealed class VirtualizedItemReorderHandle : MonoBehaviour, IEndDragHandler
    {
        public IVirtualizedReorderHandler Handler { get; set; }
        public Func<PointerEventData, int> TargetIndexResolver { get; set; }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Handler == null || TargetIndexResolver == null)
            {
                return;
            }

            var sourceIndex = GetIndex();
            var targetIndex = TargetIndexResolver(eventData);
            if (sourceIndex >= 0 &&
                targetIndex >= 0 &&
                sourceIndex != targetIndex &&
                Handler.CanMove(sourceIndex, targetIndex))
            {
                Handler.Move(sourceIndex, targetIndex);
            }
        }

        private int GetIndex()
        {
            var listItem = GetComponentInParent<VirtualizedListItem>();
            if (listItem != null)
            {
                return listItem.CurrentIndex;
            }

            var gridItem = GetComponentInParent<VirtualizedGridItem>();
            return gridItem == null ? -1 : gridItem.CurrentIndex;
        }
    }
}
