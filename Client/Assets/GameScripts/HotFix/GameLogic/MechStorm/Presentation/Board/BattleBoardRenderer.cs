using UnityEngine;

namespace MechStorm.Presentation.Board
{
    public class BattleBoardRenderer
    {
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        private Transform _plane;

        public void Initialize(int width, int height, float cellSize, Vector3 origin, Transform plane, Color color)
        {
            var worldWidth = width * cellSize;
            var worldHeight = height * cellSize;
            _plane = plane;

            _plane.localScale = new Vector3(worldWidth / 10f, 1f, worldHeight / 10f);
            _plane.position = new Vector3(
                origin.x + worldWidth / 2f, origin.y, origin.z + worldHeight / 2f);

            if (_plane.TryGetComponent<Renderer>(out var planeRenderer))
            {
                // 仅覆盖当前 Renderer 的颜色，避免修改共享材质或生成材质实例。
                var propertyBlock = new MaterialPropertyBlock();
                propertyBlock.SetColor(ColorPropertyId, color);
                propertyBlock.SetColor(BaseColorPropertyId, color);
                planeRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
