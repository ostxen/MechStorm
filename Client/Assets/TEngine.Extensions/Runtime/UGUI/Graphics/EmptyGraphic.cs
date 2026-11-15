using UnityEngine.UI;

namespace TEngine.Extensions.UGUI
{
    public sealed class EmptyGraphic : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
        }
    }
}
