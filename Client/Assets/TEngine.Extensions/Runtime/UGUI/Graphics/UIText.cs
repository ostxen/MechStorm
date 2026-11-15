using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TEngine.Extensions.UGUI
{
    /// <summary>
    /// UGUI Text 的可组合扩展。
    /// 支持字符间距、整体/逐字四角渐变和顶点颜色。
    /// </summary>
    [AddComponentMenu("TEngine/UI/Text")]
    [DisallowMultipleComponent]
    public class UIText : Text
    {
        public enum ColorBlendMode
        {
            Multiply,
            Additive
        }

        [SerializeField] private float characterSpacing;
        [SerializeField] private bool useGradient;
        [SerializeField] private bool splitTextGradient;
        [SerializeField] private Color gradientTop = Color.white;
        [SerializeField] private Color gradientBottom = Color.white;
        [SerializeField] private Color gradientLeft = Color.white;
        [SerializeField] private Color gradientRight = Color.white;
        [SerializeField] private Vector2 gradientOffset;
        [SerializeField] private bool useVertexColor;
        [SerializeField] private Color vertexTopLeft = Color.white;
        [SerializeField] private Color vertexTopRight = Color.white;
        [SerializeField] private Color vertexBottomLeft = Color.white;
        [SerializeField] private Color vertexBottomRight = Color.white;
        [SerializeField] private Vector2 vertexColorOffset;
        [SerializeField] private ColorBlendMode vertexColorBlendMode = ColorBlendMode.Multiply;

        private readonly List<UIVertex> _vertices = new();

        public float CharacterSpacing
        {
            get => characterSpacing;
            set
            {
                if (Mathf.Approximately(characterSpacing, value))
                {
                    return;
                }

                characterSpacing = value;
                SetVerticesDirty();
                SetLayoutDirty();
            }
        }

        /// <summary>
        /// 保留的简化 API。开启后使用 TopColor/BottomColor 做垂直渐变。
        /// </summary>
        public bool UseVerticalGradient
        {
            get => useGradient;
            set
            {
                if (useGradient == value)
                {
                    return;
                }

                useGradient = value;
                SetVerticesDirty();
            }
        }

        public bool UseGradient
        {
            get => useGradient;
            set
            {
                if (useGradient == value)
                {
                    return;
                }

                useGradient = value;
                SetVerticesDirty();
            }
        }

        public bool SplitTextGradient
        {
            get => splitTextGradient;
            set
            {
                if (splitTextGradient == value)
                {
                    return;
                }

                splitTextGradient = value;
                SetVerticesDirty();
            }
        }

        public Color TopColor
        {
            get => gradientTop;
            set
            {
                gradientTop = value;
                SetVerticesDirty();
            }
        }

        public Color BottomColor
        {
            get => gradientBottom;
            set
            {
                gradientBottom = value;
                SetVerticesDirty();
            }
        }

        public Color GradientTopColor
        {
            get => gradientTop;
            set
            {
                gradientTop = value;
                SetVerticesDirty();
            }
        }

        public Color GradientBottomColor
        {
            get => gradientBottom;
            set
            {
                gradientBottom = value;
                SetVerticesDirty();
            }
        }

        public Color GradientLeftColor
        {
            get => gradientLeft;
            set
            {
                gradientLeft = value;
                SetVerticesDirty();
            }
        }

        public Color GradientRightColor
        {
            get => gradientRight;
            set
            {
                gradientRight = value;
                SetVerticesDirty();
            }
        }

        public Vector2 GradientOffset
        {
            get => gradientOffset;
            set
            {
                gradientOffset = value;
                SetVerticesDirty();
            }
        }

        public bool UseVertexColor
        {
            get => useVertexColor;
            set
            {
                if (useVertexColor == value)
                {
                    return;
                }

                useVertexColor = value;
                SetVerticesDirty();
            }
        }

        public Color VertexTopLeftColor
        {
            get => vertexTopLeft;
            set
            {
                vertexTopLeft = value;
                SetVerticesDirty();
            }
        }

        public Color VertexTopRightColor
        {
            get => vertexTopRight;
            set
            {
                vertexTopRight = value;
                SetVerticesDirty();
            }
        }

        public Color VertexBottomLeftColor
        {
            get => vertexBottomLeft;
            set
            {
                vertexBottomLeft = value;
                SetVerticesDirty();
            }
        }

        public Color VertexBottomRightColor
        {
            get => vertexBottomRight;
            set
            {
                vertexBottomRight = value;
                SetVerticesDirty();
            }
        }

        public Vector2 VertexColorOffset
        {
            get => vertexColorOffset;
            set
            {
                vertexColorOffset = value;
                SetVerticesDirty();
            }
        }

        public ColorBlendMode VertexColorBlend
        {
            get => vertexColorBlendMode;
            set
            {
                vertexColorBlendMode = value;
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// 当前 TextGenerator 计算出的可见行数。
        /// </summary>
        public int VisibleLines => cachedTextGenerator != null
            ? cachedTextGenerator.lineCount
            : 0;

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            base.OnPopulateMesh(toFill);
            if (Mathf.Approximately(characterSpacing, 0f) &&
                !useGradient &&
                !useVertexColor)
            {
                return;
            }

            _vertices.Clear();
            toFill.GetUIVertexStream(_vertices);
            if (_vertices.Count == 0)
            {
                return;
            }

            if (!Mathf.Approximately(characterSpacing, 0f))
            {
                ApplyCharacterSpacing();
            }

            if (useGradient)
            {
                ApplyGradient();
            }

            if (useVertexColor)
            {
                ApplyVertexColor();
            }

            toFill.Clear();
            toFill.AddUIVertexTriangleStream(_vertices);
        }

        private void ApplyCharacterSpacing()
        {
            var currentLineY = _vertices[0].position.y;
            var lineCharacterIndex = 0;
            const float lineTolerance = 0.01f;

            // Legacy Text 为每个字符输出两个三角形，即六个顶点。
            for (var i = 0; i + 5 < _vertices.Count; i += 6)
            {
                var characterY = _vertices[i].position.y;
                if (Mathf.Abs(characterY - currentLineY) > lineTolerance)
                {
                    currentLineY = characterY;
                    lineCharacterIndex = 0;
                }

                var offset = characterSpacing * lineCharacterIndex++;
                for (var vertex = 0; vertex < 6; vertex++)
                {
                    var index = i + vertex;
                    var item = _vertices[index];
                    var position = item.position;
                    position.x += offset;
                    item.position = position;
                    _vertices[index] = item;
                }
            }
        }

        private void ApplyGradient()
        {
            if (splitTextGradient)
            {
                for (var start = 0; start < _vertices.Count; start += 6)
                {
                    var end = Math.Min(start + 6, _vertices.Count);
                    ApplyColors(start, end, CalculateBounds(start, end));
                }

                return;
            }

            ApplyColors(0, _vertices.Count, CalculateBounds(0, _vertices.Count));
        }

        private void ApplyVertexColor()
        {
            ApplyColors(
                0,
                _vertices.Count,
                CalculateBounds(0, _vertices.Count),
                true);
        }

        private void ApplyColors(int start, int end, BoundsData bounds, bool vertexOnly = false)
        {
            var invWidth = bounds.Width > 0.0001f ? 1f / bounds.Width : 0f;
            var invHeight = bounds.Height > 0.0001f ? 1f / bounds.Height : 0f;

            for (var i = start; i < end; i++)
            {
                var vertex = _vertices[i];
                var x = Mathf.Clamp01(
                    (vertex.position.x - bounds.MinX) * invWidth + gradientOffset.x);
                var y = Mathf.Clamp01(
                    (vertex.position.y - bounds.MinY) * invHeight + gradientOffset.y);

                if (!vertexOnly)
                {
                    var vertical = Color.Lerp(gradientBottom, gradientTop, y);
                    var horizontal = Color.Lerp(gradientLeft, gradientRight, x);
                    var gradient = Multiply(vertical, horizontal);
                    vertex.color = Multiply(vertex.color, gradient);
                }

                if (useVertexColor)
                {
                    var vertexColor = Bilinear(
                        vertexBottomLeft,
                        vertexBottomRight,
                        vertexTopLeft,
                        vertexTopRight,
                        Mathf.Clamp01(
                            (vertex.position.x - bounds.MinX) * invWidth + vertexColorOffset.x),
                        Mathf.Clamp01(
                            (vertex.position.y - bounds.MinY) * invHeight + vertexColorOffset.y));
                    vertex.color = vertexColorBlendMode == ColorBlendMode.Additive
                        ? Add(vertex.color, vertexColor)
                        : Multiply(vertex.color, vertexColor);
                }

                _vertices[i] = vertex;
            }
        }

        private BoundsData CalculateBounds(int start, int end)
        {
            var bounds = new BoundsData
            {
                MinX = float.MaxValue,
                MinY = float.MaxValue,
                MaxX = float.MinValue,
                MaxY = float.MinValue
            };

            for (var i = start; i < end; i++)
            {
                var position = _vertices[i].position;
                bounds.MinX = Mathf.Min(bounds.MinX, position.x);
                bounds.MinY = Mathf.Min(bounds.MinY, position.y);
                bounds.MaxX = Mathf.Max(bounds.MaxX, position.x);
                bounds.MaxY = Mathf.Max(bounds.MaxY, position.y);
            }

            return bounds;
        }

        private static Color Bilinear(
            Color bottomLeft,
            Color bottomRight,
            Color topLeft,
            Color topRight,
            float x,
            float y)
        {
            return Color.Lerp(
                Color.Lerp(bottomLeft, bottomRight, x),
                Color.Lerp(topLeft, topRight, x),
                y);
        }

        private static Color Multiply(Color left, Color right)
        {
            return new Color(
                left.r * right.r,
                left.g * right.g,
                left.b * right.b,
                left.a * right.a);
        }

        private static Color Add(Color left, Color right)
        {
            return new Color(
                left.r + right.r,
                left.g + right.g,
                left.b + right.b,
                left.a + right.a);
        }

        private struct BoundsData
        {
            public float MinX;
            public float MinY;
            public float MaxX;
            public float MaxY;

            public float Width => MaxX - MinX;
            public float Height => MaxY - MinY;
        }
    }
}
