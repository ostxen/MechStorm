using UnityEngine;
using UnityEngine.UI;

namespace MechStorm.Presentation.UI
{
    public enum SegmentedSliderFillDirection
    {
        LeftToRight = 0,
        RightToLeft = 1,
        BottomToTop = 2,
        TopToBottom = 3
    }

    public enum SegmentedSliderHighlightMode
    {
        None = 0,
        Contiguous = 1
    }

    public enum SegmentedSliderHighlightDirection
    {
        LeftToRight = 0,
        RightToLeft = 1,
        BottomToTop = 2,
        TopToBottom = 3
    }

    public enum SegmentedSliderHighlightAnchor
    {
        PhysicalStart = 0,
        PhysicalEnd = 1,
        FilledEdge = 2
    }

    [AddComponentMenu("UI/Segmented Slider")]
    [DisallowMultipleComponent]
    public sealed class SegmentedSlider : MaskableGraphic
    {
        private const string ShaderName = "MechStorm/UI/SegmentedSlider";
        private const string ValueProperty = "_Value";
        private const string MinValueProperty = "_MinValue";
        private const string MaxValueProperty = "_MaxValue";
        private const string WholeNumbersProperty = "_WholeNumbers";
        private const string FillWholeCellsProperty = "_FillWholeCells";
        private const string FillDirectionProperty = "_FillDirection";
        private const string ActiveColorProperty = "_ActiveColor";
        private const string InactiveColorProperty = "_InactiveColor";
        private const string SegmentColorTextureProperty = "_SegmentColorTex";
        private const string UseSegmentColorsProperty = "_UseSegmentColors";
        private const string HighlightCountProperty = "_HighlightCount";
        private const string HighlightModeProperty = "_HighlightMode";
        private const string HighlightDirectionProperty = "_HighlightDirection";
        private const string HighlightAnchorProperty = "_HighlightAnchor";
        private const string BreathingHighlightColorProperty = "_BreathingHighlightColor";
        private const string BreathingHighlightSpeedProperty = "_BreathingHighlightSpeed";
        private const string BreathingHighlightStrengthProperty = "_BreathingHighlightStrength";
        private const string BreathingHighlightEnabledProperty = "_BreathingHighlightEnabled";

        [SerializeField] private Material _materialTemplate;
        [SerializeField] private float _minValue;
        [SerializeField] private float _maxValue = 5f;
        [SerializeField] private float _value = 2.5f;
        [SerializeField] private bool _wholeNumbers;
        [SerializeField, Min(0f)] private float _gap = 4f;
        [SerializeField] private bool _fillWholeCells;
        [SerializeField] private SegmentedSliderFillDirection _fillDirection = SegmentedSliderFillDirection.LeftToRight;
        [SerializeField, Min(0f)] private float _leftMargin;
        [SerializeField, Min(0f)] private float _rightMargin;
        [SerializeField, Min(0f)] private float _bottomMargin;
        [SerializeField, Min(0f)] private float _topMargin;
        [SerializeField] private Color _activeColor = new Color(1f, 0.82f, 0.12f, 1f);
        [SerializeField] private Color _inactiveColor = new Color(0.18f, 0.2f, 0.24f, 1f);
        [SerializeField] private Color[] _segmentColors = new Color[0];
        [SerializeField] private SegmentedSliderHighlightMode _highlightMode;
        [SerializeField, Min(0)] private int _highlightCount;
        [SerializeField] private SegmentedSliderHighlightDirection _highlightDirection =
            SegmentedSliderHighlightDirection.RightToLeft;
        [SerializeField] private SegmentedSliderHighlightAnchor _highlightAnchor =
            SegmentedSliderHighlightAnchor.FilledEdge;
        [SerializeField] private Color _breathingHighlightColor = Color.white;
        [SerializeField, Min(0f)] private float _breathingHighlightSpeed = 6f;
        [SerializeField, Range(0f, 1f)] private float _breathingHighlightStrength = 0.8f;

        private Material _runtimeMaterial;
        private Texture2D _generatedColorTexture;
        private Color[] _appliedSegmentColors;
        private Color _appliedActiveColor;

        public override Texture mainTexture => Texture2D.whiteTexture;

        private int SegmentCount => Mathf.Max(1, Mathf.RoundToInt(_maxValue));

        protected override void OnEnable()
        {
            raycastTarget = false;
            onCullStateChanged.RemoveAllListeners();
            base.OnEnable();
            Apply();
        }

        protected override void OnDestroy()
        {
            material = null;
            DestroyRuntimeMaterial();
            DestroyGeneratedColorTexture();
            base.OnDestroy();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _highlightCount = Mathf.Clamp(_highlightCount, 0, SegmentCount);
            NormalizeNumericSettings();
            _gap = Mathf.Max(0f, _gap);
            _leftMargin = Mathf.Max(0f, _leftMargin);
            _rightMargin = Mathf.Max(0f, _rightMargin);
            _bottomMargin = Mathf.Max(0f, _bottomMargin);
            _topMargin = Mathf.Max(0f, _topMargin);
            SetAllDirty();
            raycastTarget = false;
            onCullStateChanged.RemoveAllListeners();
            if (!Application.isPlaying && !gameObject.scene.IsValid())
            {
                return;
            }

            Apply();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Layout layout = CalculateLayout();
            if (layout.CellSize <= 0f)
            {
                return;
            }

            for (int index = 0; index < SegmentCount; index++)
            {
                AddCell(vertexHelper, index, layout);
            }
        }

        public void SetValue(int currentValue, int maxValue)
        {
            _minValue = 0f;
            _maxValue = Mathf.Max(0f, maxValue);
            SetValue(currentValue);
            SetVerticesDirty();
            Apply();
        }

        public float minValue
        {
            get => _minValue;
            set
            {
                _minValue = value;
                if (_maxValue < _minValue)
                {
                    _maxValue = _minValue;
                }

                NormalizeNumericSettings();
                SetMaterialDirty();
            }
        }

        public float maxValue
        {
            get => _maxValue;
            set
            {
                _maxValue = Mathf.Max(_minValue, value);
                NormalizeNumericSettings();
                SetVerticesDirty();
                Apply();
                SetMaterialDirty();
            }
        }

        public bool wholeNumbers
        {
            get => _wholeNumbers;
            set
            {
                if (_wholeNumbers == value)
                {
                    return;
                }

                _wholeNumbers = value;
                NormalizeNumericSettings();
                ApplyValues();
                SetMaterialDirty();
            }
        }

        public float value
        {
            get => _value;
            set => SetValue(value);
        }

        public void SetValue(float input)
        {
            _value = ClampValue(input);
            ApplyValue();
        }

        public void SetRange(float minValue, float maxValue)
        {
            _minValue = minValue;
            _maxValue = Mathf.Max(_minValue, maxValue);
            NormalizeNumericSettings();
            SetVerticesDirty();
            Apply();
            SetMaterialDirty();
        }

        public void SetFillDirection(SegmentedSliderFillDirection direction)
        {
            _fillDirection = direction;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        public void SetMargins(float left, float right, float bottom, float top)
        {
            _leftMargin = Mathf.Max(0f, left);
            _rightMargin = Mathf.Max(0f, right);
            _bottomMargin = Mathf.Max(0f, bottom);
            _topMargin = Mathf.Max(0f, top);
            SetVerticesDirty();
        }

        public int highlightCount
        {
            get => _highlightCount;
            set
            {
                _highlightCount = Mathf.Clamp(value, 0, SegmentCount);
                _highlightMode = _highlightCount > 0
                    ? SegmentedSliderHighlightMode.Contiguous
                    : SegmentedSliderHighlightMode.None;
                ApplyHighlightValues();
                SetMaterialDirty();
            }
        }

        public SegmentedSliderHighlightDirection highlightDirection
        {
            get => _highlightDirection;
            set
            {
                _highlightDirection = value;
                ApplyHighlightValues();
                SetMaterialDirty();
            }
        }

        public SegmentedSliderHighlightAnchor highlightAnchor
        {
            get => _highlightAnchor;
            set
            {
                _highlightAnchor = value;
                ApplyHighlightValues();
                SetMaterialDirty();
            }
        }

        public SegmentedSliderHighlightMode highlightMode
        {
            get => _highlightMode;
            set
            {
                _highlightMode = value;
                ApplyHighlightValues();
                SetMaterialDirty();
            }
        }

        public void SetHighlight(int count,
            SegmentedSliderHighlightDirection direction,
            SegmentedSliderHighlightAnchor anchor =
                SegmentedSliderHighlightAnchor.FilledEdge)
        {
            _highlightCount = Mathf.Clamp(count, 0, SegmentCount);
            _highlightMode = _highlightCount > 0
                ? SegmentedSliderHighlightMode.Contiguous
                : SegmentedSliderHighlightMode.None;
            _highlightDirection = direction;
            _highlightAnchor = anchor;
            ApplyHighlightValues();
            SetMaterialDirty();
        }

        public void SetHighlight(int count)
        {
            SetHighlight(count,
                SegmentedSliderHighlightDirection.RightToLeft,
                SegmentedSliderHighlightAnchor.FilledEdge);
        }

        public void ClearHighlight()
        {
            _highlightCount = 0;
            _highlightMode = SegmentedSliderHighlightMode.None;
            ApplyHighlightValues();
            SetMaterialDirty();
        }

        public void SetSegmentColors(Color[] colors)
        {
            _segmentColors = colors ?? new Color[0];
            ApplyColorTexture();
            SetMaterialDirty();
        }

        private void NormalizeNumericSettings()
        {
            if (_maxValue < _minValue)
            {
                _maxValue = _minValue;
            }

            if (_wholeNumbers)
            {
                _minValue = Mathf.Round(_minValue);
                _maxValue = Mathf.Max(_minValue, Mathf.Round(_maxValue));
            }

            _value = ClampValue(_value);
            ApplyValue();
        }

        private float ClampValue(float input)
        {
            float clampedValue = Mathf.Clamp(input, _minValue, _maxValue);
            return _wholeNumbers ? Mathf.Round(clampedValue) : clampedValue;
        }

        private void ApplyValue()
        {
            if (_runtimeMaterial != null)
            {
                _runtimeMaterial.SetFloat(ValueProperty, _value);
            }

            SetMaterialDirty();
        }

        private void Apply()
        {
            if (!EnsureMaterial())
            {
                return;
            }

            ApplyValues();
            ApplyColorTexture();
        }

        private void ApplyValues()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            _runtimeMaterial.SetFloat(ValueProperty, _value);
            _runtimeMaterial.SetFloat(MinValueProperty, _minValue);
            _runtimeMaterial.SetFloat(MaxValueProperty, _maxValue);
            _runtimeMaterial.SetFloat(WholeNumbersProperty, _wholeNumbers ? 1f : 0f);
            _runtimeMaterial.SetFloat(FillWholeCellsProperty, _fillWholeCells ? 1f : 0f);
            _runtimeMaterial.SetFloat(FillDirectionProperty, (float)_fillDirection);
            ApplyHighlightValues();
            _runtimeMaterial.SetColor(ActiveColorProperty, _activeColor);
            _runtimeMaterial.SetColor(InactiveColorProperty, _inactiveColor);
            _runtimeMaterial.SetColor(BreathingHighlightColorProperty, _breathingHighlightColor);
            _runtimeMaterial.SetFloat(BreathingHighlightSpeedProperty, _breathingHighlightSpeed);
            _runtimeMaterial.SetFloat(BreathingHighlightStrengthProperty, _breathingHighlightStrength);
            _runtimeMaterial.SetFloat(BreathingHighlightEnabledProperty, Application.isPlaying ? 1f : 0f);
        }

        private void ApplyHighlightValues()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            _runtimeMaterial.SetFloat(HighlightCountProperty,
                _highlightMode == SegmentedSliderHighlightMode.Contiguous ? _highlightCount : 0f);
            _runtimeMaterial.SetFloat(HighlightModeProperty, (float)_highlightMode);
            _runtimeMaterial.SetFloat(HighlightDirectionProperty, (float)_highlightDirection);
            _runtimeMaterial.SetFloat(HighlightAnchorProperty, (float)_highlightAnchor);
        }

        private void ApplyColorTexture()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            bool hasColors = _segmentColors != null && _segmentColors.Length > 0;
            bool hasAppliedColors = _appliedSegmentColors != null && _appliedSegmentColors.Length > 0;
            bool colorsChanged = !AreColorsEqual(_segmentColors, _appliedSegmentColors);
            bool activeColorChanged = _appliedSegmentColors == null || _appliedActiveColor != _activeColor;
            bool textureSizeChanged = _generatedColorTexture != null &&
                                      _generatedColorTexture.width != SegmentCount;
            if (!colorsChanged && !activeColorChanged && !textureSizeChanged &&
                hasColors == hasAppliedColors)
            {
                return;
            }

            _appliedSegmentColors = _segmentColors == null
                ? new Color[0]
                : (Color[])_segmentColors.Clone();
            _appliedActiveColor = _activeColor;
            if (!hasColors)
            {
                _runtimeMaterial.SetFloat(UseSegmentColorsProperty, 0f);
                _runtimeMaterial.SetTexture(SegmentColorTextureProperty, Texture2D.whiteTexture);
                DestroyGeneratedColorTexture();
                return;
            }

            _runtimeMaterial.SetFloat(UseSegmentColorsProperty, 1f);
            EnsureColorTexture();

            for (int index = 0; index < SegmentCount; index++)
            {
                Color color = index < _segmentColors.Length ? _segmentColors[index] : _activeColor;
                _generatedColorTexture.SetPixel(index, 0, color);
            }

            _generatedColorTexture.Apply(false, false);
            _runtimeMaterial.SetTexture(SegmentColorTextureProperty, _generatedColorTexture);
        }

        private void EnsureColorTexture()
        {
            if (_generatedColorTexture != null && _generatedColorTexture.width == SegmentCount)
            {
                return;
            }

            DestroyGeneratedColorTexture();
            _generatedColorTexture = new Texture2D(SegmentCount, 1, TextureFormat.RGBA32, false, false)
            {
                name = "SegmentedSlider_ColorLUT",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private bool EnsureMaterial()
        {
            if (_runtimeMaterial != null)
            {
                return true;
            }

            Material source = _materialTemplate;
            Shader shader = source != null
                ? source.shader
                : material != null
                    ? material.shader
                    : Shader.Find(ShaderName);
            if (source != null && source.shader == null)
            {
                Debug.LogWarning($"Material template must use shader: {ShaderName}", this);
                source = null;
            }

            if (source != null && source.shader.name != ShaderName)
            {
                Debug.LogWarning($"Material template must use shader: {ShaderName}", this);
                source = null;
            }

            if (source == null && material != null && material.shader != null &&
                material.shader.name == ShaderName)
            {
                source = material;
            }

            if (shader == null || shader.name != ShaderName)
            {
                shader = Shader.Find(ShaderName);
            }

            if (source == null)
            {
                if (shader == null)
                {
                    Debug.LogError($"Cannot find shader: {ShaderName}", this);
                    return false;
                }

                _runtimeMaterial = new Material(shader);
            }
            else
            {
                _runtimeMaterial = new Material(source);
            }

            _runtimeMaterial.name = $"{ShaderName} (Instance)";
            _runtimeMaterial.hideFlags = HideFlags.DontSave;
            material = _runtimeMaterial;
            return true;
        }

        private Layout CalculateLayout()
        {
            Rect rect = GetPixelAdjustedRect();
            bool horizontal = _fillDirection == SegmentedSliderFillDirection.LeftToRight ||
                              _fillDirection == SegmentedSliderFillDirection.RightToLeft;
            float length = horizontal ? rect.width : rect.height;
            float startMargin = horizontal ? _leftMargin : _bottomMargin;
            float endMargin = horizontal ? _rightMargin : _topMargin;
            float available = Mathf.Max(length - startMargin - endMargin, 0f);
            float gap = SegmentCount > 1 ? Mathf.Min(_gap, available / (SegmentCount - 1f)) : 0f;
            float cellSize = Mathf.Max((available - gap * (SegmentCount - 1)) / SegmentCount, 0f);
            return new Layout(rect, cellSize, gap, horizontal);
        }

        private void AddCell(VertexHelper vertexHelper, int index, Layout layout)
        {
            bool reversed = _fillDirection == SegmentedSliderFillDirection.RightToLeft ||
                            _fillDirection == SegmentedSliderFillDirection.TopToBottom;
            float offset = index * (layout.CellSize + layout.Gap);
            float min = reversed
                ? (layout.Horizontal ? layout.Rect.xMax - _rightMargin - layout.CellSize - offset : layout.Rect.yMax - _topMargin - layout.CellSize - offset)
                : (layout.Horizontal ? layout.Rect.xMin + _leftMargin + offset : layout.Rect.yMin + _bottomMargin + offset);
            Vector2 minPosition = layout.Horizontal
                ? new Vector2(min, layout.Rect.yMin)
                : new Vector2(layout.Rect.xMin, min);
            Vector2 maxPosition = layout.Horizontal
                ? new Vector2(min + layout.CellSize, layout.Rect.yMax)
                : new Vector2(layout.Rect.xMax, min + layout.CellSize);

            Color32 vertexColor = color;
            UIVertex bottomLeft = CreateVertex(new Vector3(minPosition.x, minPosition.y), vertexColor, layout.Horizontal ? new Vector2(0f, index) : new Vector2(index, 0f));
            UIVertex topLeft = CreateVertex(new Vector3(minPosition.x, maxPosition.y), vertexColor, layout.Horizontal ? new Vector2(0f, index) : new Vector2(index, 1f));
            UIVertex topRight = CreateVertex(new Vector3(maxPosition.x, maxPosition.y), vertexColor, layout.Horizontal ? new Vector2(1f, index) : new Vector2(index, 1f));
            UIVertex bottomRight = CreateVertex(new Vector3(maxPosition.x, minPosition.y), vertexColor, layout.Horizontal ? new Vector2(1f, index) : new Vector2(index, 0f));

            int vertexStart = vertexHelper.currentVertCount;
            vertexHelper.AddVert(bottomLeft);
            vertexHelper.AddVert(topLeft);
            vertexHelper.AddVert(topRight);
            vertexHelper.AddVert(bottomRight);
            vertexHelper.AddTriangle(vertexStart, vertexStart + 1, vertexStart + 2);
            vertexHelper.AddTriangle(vertexStart + 2, vertexStart + 3, vertexStart);
        }

        private static UIVertex CreateVertex(Vector3 position, Color32 vertexColor, Vector2 uv)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = vertexColor;
            vertex.uv0 = uv;
            return vertex;
        }

        private struct Layout
        {
            public readonly Rect Rect;
            public readonly float CellSize;
            public readonly float Gap;
            public readonly bool Horizontal;

            public Layout(Rect rect, float cellSize, float gap, bool horizontal)
            {
                Rect = rect;
                CellSize = cellSize;
                Gap = gap;
                Horizontal = horizontal;
            }
        }

        private void DestroyRuntimeMaterial()
        {
            if (_runtimeMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeMaterial);
            }
            else
            {
                DestroyImmediate(_runtimeMaterial);
            }

            _runtimeMaterial = null;
        }

        private void DestroyGeneratedColorTexture()
        {
            if (_generatedColorTexture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_generatedColorTexture);
            }
            else
            {
                DestroyImmediate(_generatedColorTexture);
            }

            _generatedColorTexture = null;
        }

        private static bool AreColorsEqual(Color[] first, Color[] second)
        {
            if (first == null || second == null || first.Length != second.Length)
            {
                return first == null && second == null;
            }

            for (int index = 0; index < first.Length; index++)
            {
                if (first[index] != second[index])
                {
                    return false;
                }
            }

            return true;
        }

    }
}
