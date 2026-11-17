using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TEngine.Extensions.UGUI
{
    [AddComponentMenu("TEngine/UI/Nested Scroll Rect")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class NestedScrollRect : ScrollRect
    {
        [SerializeField] private List<ScrollRect> parentScrollRects = new();

        private bool _forwarding;

        public event Action<PointerEventData> BeginDrag;
        public event Action<PointerEventData> Drag;
        public event Action<PointerEventData> EndDrag;

        public IReadOnlyList<ScrollRect> ParentScrollRects => parentScrollRects;

        public void AddParentScrollRect(ScrollRect parent)
        {
            if (parent == null || parent == this || parentScrollRects.Contains(parent))
            {
                return;
            }

            parentScrollRects.Add(parent);
        }

        public void RemoveParentScrollRect(ScrollRect parent)
        {
            parentScrollRects.Remove(parent);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            base.OnBeginDrag(eventData);
            BeginDrag?.Invoke(eventData);
            Forward(eventData, static (scrollRect, data) => scrollRect.OnBeginDrag(data));
        }

        public override void OnDrag(PointerEventData eventData)
        {
            base.OnDrag(eventData);
            Drag?.Invoke(eventData);
            Forward(eventData, static (scrollRect, data) => scrollRect.OnDrag(data));
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            base.OnEndDrag(eventData);
            EndDrag?.Invoke(eventData);
            Forward(eventData, static (scrollRect, data) => scrollRect.OnEndDrag(data));
        }

        private void Forward(PointerEventData eventData, Action<ScrollRect, PointerEventData> action)
        {
            if (_forwarding)
            {
                return;
            }

            _forwarding = true;
            try
            {
                for (var i = 0; i < parentScrollRects.Count; i++)
                {
                    var parent = parentScrollRects[i];
                    if (parent != null && parent.isActiveAndEnabled)
                    {
                        action(parent, eventData);
                    }
                }
            }
            finally
            {
                _forwarding = false;
            }
        }
    }
}
