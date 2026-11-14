using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MechStorm.Presentation.Units
{
    public sealed class UnitHealthBarView
    {
        private const float CanvasScale = 0.01f;
        private const float SegmentGap = 2f;
        private static readonly Vector2 CanvasSize = new Vector2(120f, 42f);
        private static readonly Vector2 BarSize = new Vector2(100f, 10f);
        private static readonly Vector2 ActionPointBarSize = new Vector2(100f, 6f);
        private static readonly Color ActionPointOwnedColor = new Color(1f, 0.85f, 0.15f, 1f);
        private static readonly Color ActionPointEmptyColor = new Color(0.35f, 0.35f, 0.35f, 0.9f);

        private readonly Transform _canvasTransform;
        private readonly RectTransform _fillRect;
        private readonly RectTransform _actionPointRoot;
        private readonly Text _valueText;
        private readonly Camera _camera;
        private readonly List<Image> _actionPointSegments = new List<Image>();

        private int _actionPointSegmentCount = -1;

        public UnitHealthBarView(Transform followTarget, Camera camera)
        {
            if (followTarget == null)
            {
                throw new ArgumentNullException(nameof(followTarget));
            }

            _camera = camera;
            var canvas = CreateCanvas(followTarget);
            _canvasTransform = canvas.transform;

            var backgroundRect = CreateImage("Background", canvas.transform, BarSize, new Vector2(0f, 10f),
                new Color(0f, 0f, 0f, 0.7f));
            _fillRect = CreateImage("Fill", backgroundRect, BarSize, Vector2.zero, new Color(0.2f, 0.9f, 0.25f, 1f));
            _fillRect.anchorMin = new Vector2(0f, 0.5f);
            _fillRect.anchorMax = new Vector2(0f, 0.5f);
            _fillRect.pivot = new Vector2(0f, 0.5f);
            _fillRect.anchoredPosition = Vector2.zero;

            _actionPointRoot = CreateImage("ActionPointBackground", canvas.transform, ActionPointBarSize,
                new Vector2(0f, -2f), new Color(0f, 0f, 0f, 0.55f));
            _valueText = CreateText(canvas.transform);
        }

        public void RefreshValue(int currentDurability, int maxDurability)
        {
            var safeMax = Mathf.Max(0, maxDurability);
            var safeCurrent = Mathf.Clamp(currentDurability, 0, safeMax);
            var ratio = safeMax > 0 ? (float)safeCurrent / safeMax : 0f;

            _fillRect.localScale = new Vector3(ratio, 1f, 1f);
            _valueText.text = safeCurrent + "/" + safeMax;
        }

        public void RefreshActionPoint(int currentActionPoint, int maxActionPoint)
        {
            var safeMax = Mathf.Max(0, maxActionPoint);
            var safeCurrent = Mathf.Clamp(currentActionPoint, 0, safeMax);
            EnsureActionPointSegments(safeMax);

            for (var i = 0; i < _actionPointSegments.Count; i++)
            {
                _actionPointSegments[i].color = i < safeCurrent
                    ? ActionPointOwnedColor
                    : ActionPointEmptyColor;
            }
        }

        public void RefreshFacing()
        {
            var camera = _camera != null ? _camera : Camera.main;
            if (camera == null)
            {
                return;
            }

            _canvasTransform.rotation = camera.transform.rotation;
        }

        private void EnsureActionPointSegments(int maxActionPoint)
        {
            if (_actionPointSegmentCount == maxActionPoint)
            {
                return;
            }

            _actionPointSegmentCount = maxActionPoint;
            for (var i = _actionPointSegments.Count - 1; i >= 0; i--)
            {
                var segment = _actionPointSegments[i];
                if (segment != null)
                {
                    UnityEngine.Object.Destroy(segment.gameObject);
                }
            }

            _actionPointSegments.Clear();
            if (maxActionPoint <= 0)
            {
                return;
            }

            var totalGap = SegmentGap * (maxActionPoint - 1);
            var segmentWidth = Mathf.Max(1f, (ActionPointBarSize.x - totalGap) / maxActionPoint);
            var left = -ActionPointBarSize.x * 0.5f;

            for (var i = 0; i < maxActionPoint; i++)
            {
                var x = left + i * (segmentWidth + SegmentGap) + segmentWidth * 0.5f;
                var segmentRect = CreateImage("ActionPointSegment_" + i, _actionPointRoot,
                    new Vector2(segmentWidth, ActionPointBarSize.y - 2f), new Vector2(x, 0f), ActionPointEmptyColor);
                _actionPointSegments.Add(segmentRect.GetComponent<Image>());
            }
        }

        private static Canvas CreateCanvas(Transform followTarget)
        {
            var canvasObject = new GameObject("UnitHealthBarCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(followTarget, false);
            canvasObject.transform.localPosition = Vector3.zero;
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * CanvasScale;

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rectTransform = (RectTransform)canvasObject.transform;
            rectTransform.sizeDelta = CanvasSize;

            return canvas;
        }

        private static RectTransform CreateImage(string name, Transform parent, Vector2 size, Vector2 anchoredPosition,
            Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            var rectTransform = (RectTransform)imageObject.transform;
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;

            var image = imageObject.GetComponent<Image>();
            image.color = color;

            return rectTransform;
        }

        private static Text CreateText(Transform parent)
        {
            var textObject = new GameObject("ValueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);

            var rectTransform = (RectTransform)textObject.transform;
            rectTransform.sizeDelta = new Vector2(120f, 14f);
            rectTransform.anchoredPosition = new Vector2(0f, -16f);

            var text = textObject.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = 12;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return text;
        }
    }
}
