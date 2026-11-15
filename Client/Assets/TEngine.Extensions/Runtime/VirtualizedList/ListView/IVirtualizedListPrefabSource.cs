using UnityEngine;

namespace TEngine.Extensions.VirtualizedList
{
    /// <summary>
    /// 虚拟列表的 Prefab/实例来源边界。
    /// 业务 Adapter 不直接依赖 Instantiate、资源模块或对象池实现。
    /// </summary>
    public interface IVirtualizedListPrefabSource
    {
        VirtualizedListItem Create(string key, Transform parent);
        void Release(VirtualizedListItem item);
    }
}
