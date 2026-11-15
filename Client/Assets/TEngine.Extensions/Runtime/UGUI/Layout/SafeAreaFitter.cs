using UnityEngine;

namespace TEngine.Extensions.UGUI
{
    [AddComponentMenu("TEngine/UI/Safe Area Fitter")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private bool applyLeft = true;
        [SerializeField] private bool applyRight = true;
        [SerializeField] private bool applyTop = true;
        [SerializeField] private bool applyBottom = true;

        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            ApplySafeArea(true);
        }

        private void OnEnable()
        {
            ApplySafeArea(true);
        }

        private void Update()
        {
            ApplySafeArea(false);
        }

        private void ApplySafeArea(bool force)
        {
            var safeArea = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (!force && safeArea == _lastSafeArea && screenSize == _lastScreenSize)
            {
                return;
            }

            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;
            var min = safeArea.position;
            var max = safeArea.position + safeArea.size;
            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            var anchorMin = new Vector2(min.x / width, min.y / height);
            var anchorMax = new Vector2(max.x / width, max.y / height);

            var currentMin = _rectTransform.anchorMin;
            var currentMax = _rectTransform.anchorMax;
            _rectTransform.anchorMin = new Vector2(
                applyLeft ? anchorMin.x : currentMin.x,
                applyBottom ? anchorMin.y : currentMin.y);
            _rectTransform.anchorMax = new Vector2(
                applyRight ? anchorMax.x : currentMax.x,
                applyTop ? anchorMax.y : currentMax.y);
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
