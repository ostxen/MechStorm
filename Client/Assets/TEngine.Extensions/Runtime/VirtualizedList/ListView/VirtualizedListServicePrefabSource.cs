using UnityEngine;

namespace TEngine.Extensions.VirtualizedList
{
    internal sealed class VirtualizedListServicePrefabSource : IVirtualizedListPrefabSource
    {
        public VirtualizedListItem Create(string key, Transform parent)
        {
            var instance = VirtualizedListServices.LoadPrefab(key, parent);
            if (instance == null)
            {
                throw new System.InvalidOperationException(
                    $"VirtualizedListServices could not load prefab: {key}");
            }

            var item = instance.GetComponent<VirtualizedListItem>();
            if (item == null)
            {
                UnityEngine.Object.Destroy(instance);
                throw new System.InvalidOperationException(
                    $"Loaded prefab '{key}' must contain a VirtualizedListItem component.");
            }

            item.PrefabKey = key;
            return item;
        }

        public void Release(VirtualizedListItem item)
        {
            if (item != null)
            {
                UnityEngine.Object.Destroy(item.gameObject);
            }
        }
    }
}
