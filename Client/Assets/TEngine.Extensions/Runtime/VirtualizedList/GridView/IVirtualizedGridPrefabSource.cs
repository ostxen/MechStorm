using UnityEngine;

namespace TEngine.Extensions.VirtualizedList
{
    public interface IVirtualizedGridPrefabSource
    {
        VirtualizedGridItem Create(string key, Transform parent);
        void Release(VirtualizedGridItem item);
    }
}
