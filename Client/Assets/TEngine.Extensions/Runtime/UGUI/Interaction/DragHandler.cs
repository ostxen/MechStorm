using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace TEngine.Extensions.UGUI
{
    [AddComponentMenu("TEngine/UI/Drag Handler")]
    public sealed class DragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform coordinateSpace;
        public Vector2Event onBeginDrag;
        public Vector2Event onDrag;
        public Vector2Event onEndDrag;

        public RectTransform CoordinateSpace
        {
            get => coordinateSpace;
            set => coordinateSpace = value;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Invoke(onBeginDrag, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Invoke(onDrag, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Invoke(onEndDrag, eventData);
        }

        private void Invoke(Vector2Event callback, PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            var point = eventData.position;
            if (coordinateSpace != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    coordinateSpace, eventData.position, eventData.pressEventCamera, out point);
            }

            callback?.Invoke(point);
        }

        [System.Serializable]
        public sealed class Vector2Event : UnityEvent<Vector2>
        {
        }
    }
}
