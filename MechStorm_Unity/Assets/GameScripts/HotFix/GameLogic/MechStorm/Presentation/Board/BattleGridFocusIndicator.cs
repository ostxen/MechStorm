using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Board
{
    /// <summary>
    /// 使用程序化四角网格显示当前棋盘焦点，不修改范围底色。
    /// </summary>
    public sealed class BattleGridFocusIndicator : IDisposable
    {
        private const float HeightOffset = 0.03f;
        private const float HalfExtentRatio = 0.43f;
        private const float ArmLengthRatio = 0.22f;
        private const float ThicknessRatio = 0.045f;

        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        private readonly GridCoordinateConverter _coordinateConverter;
        private readonly GameObject _root;
        private readonly Mesh _mesh;
        private bool _isDisposed;

        public bool IsVisible => !_isDisposed && _root.activeSelf;

        public Vector2Int? FocusedPosition { get; private set; }

        public BattleGridFocusIndicator(float cellSize, GridCoordinateConverter coordinateConverter,
            Material material, Color color)
        {
            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cellSize), "Cell size must be greater than zero.");
            }

            _coordinateConverter = coordinateConverter ??
                throw new ArgumentNullException(nameof(coordinateConverter));
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }

            if (!material.HasProperty(ColorPropertyId) &&
                !material.HasProperty(BaseColorPropertyId))
            {
                throw new ArgumentException(
                    "Grid focus material must expose _Color or _BaseColor.",
                    nameof(material));
            }

            _root = new GameObject("BattleGridFocus");
            _root.layer = Physics.IgnoreRaycastLayer;

            var meshFilter = _root.AddComponent<MeshFilter>();
            var meshRenderer = _root.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            _mesh = CreateCornerMesh(cellSize);
            meshFilter.sharedMesh = _mesh;
            ApplyColor(meshRenderer, color);
            _root.SetActive(false);
        }

        public void Show(Vector2Int gridPosition)
        {
            ThrowIfDisposed();
            FocusedPosition = gridPosition;
            _root.transform.position =
                _coordinateConverter.GridToWorld(gridPosition) +
                Vector3.up * HeightOffset;
            _root.SetActive(true);
        }

        public void Hide()
        {
            ThrowIfDisposed();
            FocusedPosition = null;
            _root.SetActive(false);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            FocusedPosition = null;
            UnityEngine.Object.Destroy(_mesh);
            UnityEngine.Object.Destroy(_root);
        }

        private static Mesh CreateCornerMesh(float cellSize)
        {
            var halfExtent = cellSize * HalfExtentRatio;
            var armLength = cellSize * ArmLengthRatio;
            var halfThickness = cellSize * ThicknessRatio * 0.5f;
            var vertices = new List<Vector3>(32);
            var triangles = new List<int>(48);

            AddCorner(vertices, triangles, -1, -1, halfExtent, armLength, halfThickness);
            AddCorner(vertices, triangles, -1, 1, halfExtent, armLength, halfThickness);
            AddCorner(vertices, triangles, 1, -1, halfExtent, armLength, halfThickness);
            AddCorner(vertices, triangles, 1, 1, halfExtent, armLength, halfThickness);

            var mesh = new Mesh { name = "BattleGridFocusMesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCorner(List<Vector3> vertices, List<int> triangles,
            int xDirection, int zDirection, float halfExtent, float armLength,
            float halfThickness)
        {
            var outerX = xDirection * halfExtent;
            var innerX = xDirection * (halfExtent - armLength);
            var outerZ = zDirection * halfExtent;
            var innerZ = zDirection * (halfExtent - armLength);

            AddQuad(vertices, triangles,
                Mathf.Min(outerX, innerX), Mathf.Max(outerX, innerX),
                outerZ - halfThickness, outerZ + halfThickness);
            AddQuad(vertices, triangles,
                outerX - halfThickness, outerX + halfThickness,
                Mathf.Min(outerZ, innerZ), Mathf.Max(outerZ, innerZ));
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles,
            float minX, float maxX, float minZ, float maxZ)
        {
            var startIndex = vertices.Count;
            vertices.Add(new Vector3(minX, 0f, minZ));
            vertices.Add(new Vector3(minX, 0f, maxZ));
            vertices.Add(new Vector3(maxX, 0f, maxZ));
            vertices.Add(new Vector3(maxX, 0f, minZ));

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }

        private static void ApplyColor(Renderer renderer, Color color)
        {
            var propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetColor(ColorPropertyId, color);
            propertyBlock.SetColor(BaseColorPropertyId, color);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(BattleGridFocusIndicator));
            }
        }
    }
}
