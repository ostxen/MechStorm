using UnityEngine;
using UnityEngine.UI;

namespace MechStorm.Presentation.UI
{
    /// <summary>
    /// 只提供 RectTransform 范围内的 UGUI 射线命中，不提交网格或材质。
    /// </summary>
    [AddComponentMenu("UI/Raycast Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    [DisallowMultipleComponent]
    public sealed class RaycastGraphic : Graphic
    {
        public override void SetVerticesDirty()
        {
        }

        public override void SetMaterialDirty()
        {
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
        }
    }
}
