using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.VirtualizedList
{
    public sealed class VirtualizedGridPrefabSource : IVirtualizedGridPrefabSource
    {
        private readonly Dictionary<string, GameObject> _prefabs =
            new(StringComparer.Ordinal);

        public void Register(string key, GameObject prefab)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Prefab key cannot be empty.", nameof(key));
            }

            _prefabs[key] = prefab ?? throw new ArgumentNullException(nameof(prefab));
        }

        public VirtualizedGridItem Create(string key, Transform parent)
        {
            if (!_prefabs.TryGetValue(key, out var prefab))
            {
                throw new InvalidOperationException($"Prefab is not registered: {key}");
            }

            var instance = UnityEngine.Object.Instantiate(prefab, parent);
            var item = instance.GetComponent<VirtualizedGridItem>();
            if (item == null)
            {
                UnityEngine.Object.Destroy(instance);
                throw new InvalidOperationException(
                    $"Prefab '{key}' must contain a VirtualizedGridItem component.");
            }

            item.PrefabKey = key;
            return item;
        }

        public void Release(VirtualizedGridItem item)
        {
            if (item != null)
            {
                UnityEngine.Object.Destroy(item.gameObject);
            }
        }
    }
}
