using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.VirtualizedList
{
    /// <summary>
    /// 基于 Prefab 配置的默认实例来源。
    /// 资源系统适配层可以实现 IVirtualizedListPrefabSource 替换它。
    /// </summary>
    public sealed class VirtualizedListPrefabSource : IVirtualizedListPrefabSource
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

        public VirtualizedListItem Create(string key, Transform parent)
        {
            if (!_prefabs.TryGetValue(key, out var prefab))
            {
                throw new InvalidOperationException($"Prefab is not registered: {key}");
            }

            var instance = UnityEngine.Object.Instantiate(prefab, parent);
            var item = instance.GetComponent<VirtualizedListItem>();
            if (item == null)
            {
                UnityEngine.Object.Destroy(instance);
                throw new InvalidOperationException(
                    $"Prefab '{key}' must contain a VirtualizedListItem component.");
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
