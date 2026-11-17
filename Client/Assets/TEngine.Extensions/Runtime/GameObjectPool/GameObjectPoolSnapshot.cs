using System.Collections.Generic;

namespace TEngine.Extensions.GameObjectPool
{
    public sealed class GameObjectPoolGroupSnapshot
    {
        internal GameObjectPoolGroupSnapshot(
            string name,
            string key,
            int capacity,
            float expireTime,
            float autoDestroyTime,
            bool dontDestroy,
            int totalCount,
            int activeCount,
            int availableCount,
            IReadOnlyList<string> loadedKeys)
        {
            Name = name;
            Key = key;
            Capacity = capacity;
            ExpireTime = expireTime;
            AutoDestroyTime = autoDestroyTime;
            DontDestroy = dontDestroy;
            TotalCount = totalCount;
            ActiveCount = activeCount;
            AvailableCount = availableCount;
            LoadedKeys = loadedKeys;
        }

        public string Name { get; }
        public string Key { get; }
        public int Capacity { get; }
        public float ExpireTime { get; }
        public float AutoDestroyTime { get; }
        public bool DontDestroy { get; }
        public int TotalCount { get; }
        public int ActiveCount { get; }
        public int AvailableCount { get; }
        public IReadOnlyList<string> LoadedKeys { get; }
    }

    public sealed class GameObjectPoolSnapshot
    {
        internal GameObjectPoolSnapshot(IReadOnlyList<GameObjectPoolGroupSnapshot> groups)
        {
            Groups = groups;
        }

        public IReadOnlyList<GameObjectPoolGroupSnapshot> Groups { get; }
    }
}
