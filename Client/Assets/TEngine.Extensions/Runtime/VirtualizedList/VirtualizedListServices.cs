using UnityEngine;

namespace TEngine.Extensions.VirtualizedList
{
    public static class VirtualizedListServices
    {
        public static System.Func<string, Transform, GameObject> PrefabLoader { get; set; }

        public static GameObject LoadPrefab(string key, Transform parent)
        {
            return PrefabLoader?.Invoke(key, parent);
        }
    }
}
