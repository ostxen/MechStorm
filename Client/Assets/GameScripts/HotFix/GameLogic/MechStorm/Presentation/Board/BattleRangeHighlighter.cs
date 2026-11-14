using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Board
{
    /// <summary>
    /// 创建并缓存棋盘格表现，根据高亮映射刷新每格颜色。
    /// </summary>
    public sealed class BattleRangeHighlighter : IDisposable
    {
        private const float MarkerHeightOffset = 0.01f;

        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        private readonly Dictionary<Vector2Int, MarkerView> _markers;
        private readonly MaterialPropertyBlock _propertyBlock;
        private readonly Color _defaultColor;
        private readonly GameObject _root;
        private bool _isDisposed;

        /// <summary>
        /// 根据场景模板创建全部棋盘格表现并设置默认颜色。
        /// </summary>
        public BattleRangeHighlighter(int width, int height, float cellSize,
            GridCoordinateConverter coordinateConverter, GameObject markerTemplate, Color defaultColor)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
            }

            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");
            }

            if (coordinateConverter == null)
            {
                throw new ArgumentNullException(nameof(coordinateConverter));
            }

            if (markerTemplate == null)
            {
                throw new ArgumentNullException(nameof(markerTemplate));
            }

            var templateRenderer = markerTemplate.GetComponent<Renderer>();
            if (templateRenderer == null)
            {
                throw new ArgumentException(
                    "Highlight marker template must have a Renderer on its root object.",
                    nameof(markerTemplate));
            }

            if (templateRenderer.sharedMaterial == null ||
                (!templateRenderer.sharedMaterial.HasProperty(ColorPropertyId) &&
                 !templateRenderer.sharedMaterial.HasProperty(BaseColorPropertyId)))
            {
                throw new ArgumentException(
                    "Highlight marker material must expose _Color or _BaseColor.",
                    nameof(markerTemplate));
            }

            _root = new GameObject("BattleRangeHighlights");
            _propertyBlock = new MaterialPropertyBlock();
            _defaultColor = defaultColor;
            _markers = CreateMarkers(width, height, cellSize, coordinateConverter, markerTemplate);
            markerTemplate.SetActive(false);
            Clear();
        }

        /// <summary>
        /// 先恢复默认颜色，再应用指定格子的高亮类型。
        /// </summary>
        public void Show(IReadOnlyDictionary<Vector2Int, BattleCellHighlightType> highlights)
        {
            if (highlights == null)
            {
                throw new ArgumentNullException(nameof(highlights));
            }

            ThrowIfDisposed();
            Clear();

            foreach (var highlight in highlights)
            {
                if (!_markers.TryGetValue(highlight.Key, out var markerView))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(highlights), highlight.Key, "Highlight position must be inside the board.");
                }

                ApplyColor(markerView.Renderer, highlight.Value);
                markerView.GameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 将全部棋盘格恢复为默认颜色。
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();
            foreach (var markerView in _markers.Values)
            {
                ApplyColor(markerView.Renderer, _defaultColor);
                markerView.GameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 销毁运行时创建的棋盘格表现。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            UnityEngine.Object.Destroy(_root);
        }

        /// <summary>
        /// 为每个棋盘坐标创建表现对象并缓存其 Renderer。
        /// </summary>
        private Dictionary<Vector2Int, MarkerView> CreateMarkers(int width, int height, float cellSize,
            GridCoordinateConverter coordinateConverter, GameObject markerTemplate)
        {
            var markers = new Dictionary<Vector2Int, MarkerView>(width * height);
            var templateScale = markerTemplate.transform.localScale;
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var position = new Vector2Int(x, y);
                    var marker = GameObject.Instantiate(markerTemplate, _root.transform);
                    marker.name = $"RangeHighlight_{x}_{y}";
                    marker.transform.position = coordinateConverter.GridToWorld(position) + Vector3.up * MarkerHeightOffset;
                    marker.transform.localScale = templateScale * cellSize;
                    SetLayerRecursively(marker.transform, Physics.IgnoreRaycastLayer);

                    foreach (var markerCollider in marker.GetComponentsInChildren<Collider>(true))
                    {
                        markerCollider.enabled = false;
                    }

                    var markerRenderer = marker.GetComponent<Renderer>();
                    markerRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    markerRenderer.receiveShadows = false;

                    var coordinateText = marker.GetComponentInChildren<TextMesh>(true);
                    if (coordinateText != null)
                    {
                        coordinateText.text = $"({x},{y})";
                    }
                    marker.SetActive(false);
                    markers.Add(position, new MarkerView(marker, markerRenderer));
                }
            }

            return markers;
        }

        /// <summary>
        /// 将高亮类型转换为颜色并应用到指定格子。
        /// </summary>
        private void ApplyColor(Renderer markerRenderer, BattleCellHighlightType highlightType)
        {
            var color = highlightType switch
            {
                BattleCellHighlightType.Move => new Color(0.1f, 0.45f, 1f),
                BattleCellHighlightType.Attack => new Color(1f, 0.2f, 0.15f),
                BattleCellHighlightType.MoveAndAttack => new Color(0.65f, 0.2f, 0.9f),
                BattleCellHighlightType.ValidTarget => new Color(1f, 0.8f, 0.1f),
                BattleCellHighlightType.SelectedAttackTarget => new Color(1f, 0.45f, 0.08f),
                BattleCellHighlightType.Ability => new Color(0.15f, 0.75f, 1f),
                BattleCellHighlightType.ValidAbilityTarget => new Color(0.2f, 1f, 0.45f),
                BattleCellHighlightType.SelectedAbilityTarget => new Color(1f, 0.75f, 0.1f),
                BattleCellHighlightType.SelectedActionTarget => new Color(1f, 0.65f, 0.15f),
                _ => throw new ArgumentOutOfRangeException(nameof(highlightType), highlightType, null),
            };

            ApplyColor(markerRenderer, color);
        }

        /// <summary>
        /// 通过属性块覆盖当前 Renderer 的颜色。
        /// </summary>
        private void ApplyColor(Renderer markerRenderer, Color color)
        {
            _propertyBlock.Clear();
            _propertyBlock.SetColor(ColorPropertyId, color);
            _propertyBlock.SetColor(BaseColorPropertyId, color);
            markerRenderer.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>
        /// 递归设置对象层级，避免高亮对象参与射线检测。
        /// </summary>
        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetLayerRecursively(child, layer);
            }
        }

        /// <summary>
        /// 阻止已释放的高亮器继续工作。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(BattleRangeHighlighter));
            }
        }

        /// <summary>
        /// 保存单个棋盘格的对象与渲染器引用。
        /// </summary>
        private sealed class MarkerView
        {
            public GameObject GameObject { get; }

            public Renderer Renderer { get; }

            /// <summary>
            /// 绑定棋盘格对象及其颜色渲染器。
            /// </summary>
            public MarkerView(GameObject gameObject, Renderer renderer)
            {
                GameObject = gameObject;
                Renderer = renderer;
            }
        }
    }
}
