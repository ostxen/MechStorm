using UnityEngine;
using UnityEngine.EventSystems;

namespace TEngine.Extensions.UGUI
{
    [AddComponentMenu("TEngine/UI/Press Scale Feedback")]
    public sealed class PressScaleFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject target;
        public Vector3 pressedScale = new(0.95f, 0.95f, 0.95f);
        [Min(0)] public float duration = 0.1f;

        private Vector3 _defaultScale;
        private bool _pressed;
        private float _progress;

        private void Awake()
        {
            target ??= gameObject;
            _defaultScale = target.transform.localScale;
        }

        private void Update()
        {
            var destination = _pressed
                ? Vector3.Scale(_defaultScale, pressedScale)
                : _defaultScale;
            var step = duration <= 0 ? 1f : Time.unscaledDeltaTime / duration;
            _progress = Mathf.MoveTowards(_progress, _pressed ? 1f : 0f, step);
            target.transform.localScale = Vector3.LerpUnclamped(_defaultScale, destination, _progress);
        }

        public void OnPointerDown(PointerEventData eventData) => _pressed = true;
        public void OnPointerUp(PointerEventData eventData) => _pressed = false;
        public void OnPointerEnter(PointerEventData eventData) { }
        public void OnPointerExit(PointerEventData eventData) => _pressed = false;

        private void OnDisable()
        {
            _pressed = false;
            _progress = 0;
            if (target != null)
            {
                target.transform.localScale = _defaultScale;
            }
        }
    }
}
