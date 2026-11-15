using UnityEngine;

namespace TEngine.Extensions.UGUI
{
    [AddComponentMenu("TEngine/UI/Aspect Ratio Background")]
    public sealed class AspectRatioBackground : MonoBehaviour
    {
        [Min(0.01f)] public float referenceAspect = 9f / 16f;
        [SerializeField] private bool fitWidth = true;

        private Vector2Int _lastScreenSize;
        private Vector3 _baseScale;

        private void Awake()
        {
            _baseScale = transform.localScale;
            Apply(true);
        }

        private void OnEnable()
        {
            Apply(true);
        }

        private void Update()
        {
            Apply(false);
        }

        private void Apply(bool force)
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (!force && screenSize == _lastScreenSize)
            {
                return;
            }

            _lastScreenSize = screenSize;
            var aspect = Screen.height == 0 ? referenceAspect : Screen.width / (float)Screen.height;
            var ratio = fitWidth
                ? Mathf.Max(1f, aspect / referenceAspect)
                : Mathf.Max(1f, referenceAspect / aspect);
            transform.localScale = _baseScale * ratio;
        }
    }
}
