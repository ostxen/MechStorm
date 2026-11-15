using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TEngine.Extensions.UGUI
{
    /// <summary>
    /// UGUI Image 的扩展，支持 UV 镜像和简单 Sprite 圆角。
    /// 圆角使用网格扇形，不依赖额外 Shader。
    /// </summary>
    [AddComponentMenu("TEngine/UI/Image")]
    [DisallowMultipleComponent]
    public class UIImage : Image
    {
        public enum MirrorMode
        {
            None,
            Horizontal,
            Vertical,
            Both
        }

        [SerializeField] private MirrorMode mirror;
        [SerializeField] private bool useRoundedCorners;
        [SerializeField, Min(0f)] private float cornerRadius = 20f;
        [SerializeField, Range(1, 20)] private int cornerSegments = 5;

        private readonly List<UIVertex> _vertices = new();

        public MirrorMode Mirror
        {
            get => mirror;
            set
            {
                if (mirror == value)
                {
                    return;
                }

                mirror = value;
                SetVerticesDirty();
            }
        }

        public bool UseRoundedCorners
        {
            get => useRoundedCorners;
            set
            {
                if (useRoundedCorners == value)
                {
                    return;
                }

                useRoundedCorners = value;
                SetVerticesDirty();
            }
        }

        public float CornerRadius
        {
            get => cornerRadius;
            set
            {
                var clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(cornerRadius, clamped))
                {
                    return;
                }

                cornerRadius = clamped;
                SetVerticesDirty();
            }
        }

        public int CornerSegments
        {
            get => cornerSegments;
            set
            {
                var clamped = Mathf.Clamp(value, 1, 20);
                if (cornerSegments == clamped)
                {
                    return;
                }

                cornerSegments = clamped;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            base.OnPopulateMesh(toFill);
            if (toFill.currentVertCount == 0)
            {
                return;
            }

            if (useRoundedCorners && type == Type.Simple && cornerRadius > 0f)
            {
                ApplyRoundedCorners(toFill);
            }

            if (mirror != MirrorMode.None)
            {
                ApplyMirror(toFill);
            }
        }

        private void ApplyMirror(VertexHelper toFill)
        {
            _vertices.Clear();
            toFill.GetUIVertexStream(_vertices);
            if (_vertices.Count == 0)
            {
                return;
            }

            var minU = float.MaxValue;
            var maxU = float.MinValue;
            var minV = float.MaxValue;
            var maxV = float.MinValue;
            for (var i = 0; i < _vertices.Count; i++)
            {
                var uv = _vertices[i].uv0;
                minU = Mathf.Min(minU, uv.x);
                maxU = Mathf.Max(maxU, uv.x);
                minV = Mathf.Min(minV, uv.y);
                maxV = Mathf.Max(maxV, uv.y);
            }

            for (var i = 0; i < _vertices.Count; i++)
            {
                var vertex = _vertices[i];
                var uv = vertex.uv0;
                if (mirror == MirrorMode.Horizontal || mirror == MirrorMode.Both)
                {
                    uv.x = minU + maxU - uv.x;
                }

                if (mirror == MirrorMode.Vertical || mirror == MirrorMode.Both)
                {
                    uv.y = minV + maxV - uv.y;
                }

                vertex.uv0 = uv;
                _vertices[i] = vertex;
            }

            toFill.Clear();
            toFill.AddUIVertexTriangleStream(_vertices);
        }

        private void ApplyRoundedCorners(VertexHelper toFill)
        {
            _vertices.Clear();
            toFill.GetUIVertexStream(_vertices);
            if (_vertices.Count == 0)
            {
                return;
            }

            var bounds = CalculateBounds(_vertices);
            var uvBounds = CalculateUvBounds(_vertices);
            var radius = Mathf.Clamp(
                cornerRadius,
                0f,
                Mathf.Min(bounds.Width, bounds.Height) * 0.5f);
            if (radius <= 0f)
            {
                return;
            }

            var sample = _vertices[0];
            var center = CreateVertex(
                sample,
                new Vector2(
                    (bounds.MinX + bounds.MaxX) * 0.5f,
                    (bounds.MinY + bounds.MaxY) * 0.5f),
                new Vector2(
                    (uvBounds.MinX + uvBounds.MaxX) * 0.5f,
                    (uvBounds.MinY + uvBounds.MaxY) * 0.5f));

            toFill.Clear();
            toFill.AddVert(center);
            var boundaryCount = cornerSegments * 4;
            for (var i = 0; i < boundaryCount; i++)
            {
                var point = GetRoundedBoundaryPoint(
                    i,
                    boundaryCount,
                    bounds,
                    radius);
                var uv = new Vector2(
                    Mathf.Lerp(
                        uvBounds.MinX,
                        uvBounds.MaxX,
                        Normalize(point.x, bounds.MinX, bounds.MaxX)),
                    Mathf.Lerp(
                        uvBounds.MinY,
                        uvBounds.MaxY,
                        Normalize(point.y, bounds.MinY, bounds.MaxY)));
                toFill.AddVert(CreateVertex(sample, point, uv));
            }

            for (var i = 0; i < boundaryCount; i++)
            {
                toFill.AddTriangle(0, i + 1, (i + 1) % boundaryCount + 1);
            }
        }

        private Vector2 GetRoundedBoundaryPoint(
            int index,
            int boundaryCount,
            BoundsData bounds,
            float radius)
        {
            var corner = index / cornerSegments;
            var segment = index % cornerSegments;
            var angle = (-90f + corner * 90f + segment * 90f / cornerSegments) * Mathf.Deg2Rad;
            Vector2 center;
            switch (corner)
            {
                case 0:
                    center = new Vector2(bounds.MaxX - radius, bounds.MinY + radius);
                    break;
                case 1:
                    center = new Vector2(bounds.MaxX - radius, bounds.MaxY - radius);
                    break;
                case 2:
                    center = new Vector2(bounds.MinX + radius, bounds.MaxY - radius);
                    break;
                default:
                    center = new Vector2(bounds.MinX + radius, bounds.MinY + radius);
                    break;
            }

            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static UIVertex CreateVertex(UIVertex source, Vector2 position, Vector2 uv)
        {
            source.position = new Vector3(position.x, position.y, source.position.z);
            source.uv0 = uv;
            return source;
        }

        private static float Normalize(float value, float min, float max)
        {
            return Mathf.Approximately(min, max)
                ? 0f
                : Mathf.Clamp01((value - min) / (max - min));
        }

        private static BoundsData CalculateBounds(List<UIVertex> vertices)
        {
            var result = new BoundsData
            {
                MinX = float.MaxValue,
                MinY = float.MaxValue,
                MaxX = float.MinValue,
                MaxY = float.MinValue
            };
            for (var i = 0; i < vertices.Count; i++)
            {
                var position = vertices[i].position;
                result.MinX = Mathf.Min(result.MinX, position.x);
                result.MinY = Mathf.Min(result.MinY, position.y);
                result.MaxX = Mathf.Max(result.MaxX, position.x);
                result.MaxY = Mathf.Max(result.MaxY, position.y);
            }

            return result;
        }

        private static BoundsData CalculateUvBounds(List<UIVertex> vertices)
        {
            var result = new BoundsData
            {
                MinX = float.MaxValue,
                MinY = float.MaxValue,
                MaxX = float.MinValue,
                MaxY = float.MinValue
            };
            for (var i = 0; i < vertices.Count; i++)
            {
                var uv = vertices[i].uv0;
                result.MinX = Mathf.Min(result.MinX, uv.x);
                result.MinY = Mathf.Min(result.MinY, uv.y);
                result.MaxX = Mathf.Max(result.MaxX, uv.x);
                result.MaxY = Mathf.Max(result.MaxY, uv.y);
            }

            return result;
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
