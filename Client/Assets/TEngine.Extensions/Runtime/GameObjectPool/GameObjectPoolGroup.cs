using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TEngine.Extensions.GameObjectPool
{
    /// <summary>
    /// 管理一个配置组。容量和最早空闲淘汰在组内生效，资源 key 仍按 key 分桶复用。
    /// </summary>
    public sealed class GameObjectPoolGroup : IDisposable
    {
        private sealed class Entry
        {
            public string Key;
            public GameObject Instance;
            public int InstanceId;
            public bool InUse;
            public bool Temporary;
            public float ReleasedAt;
            public LinkedListNode<Entry> InactiveNode;
        }

        private readonly IGameObjectPoolResourceProvider _provider;
        private readonly Transform _poolRoot;
        private readonly Dictionary<string, List<Entry>> _available =
            new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly LinkedList<Entry> _inactive = new LinkedList<Entry>();
        private readonly Dictionary<string, Task<GameObject>> _pending =
            new Dictionary<string, Task<GameObject>>(StringComparer.Ordinal);
        private readonly HashSet<string> _loadedKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private bool _disposed;
        private int _generation;
        private float _lastIdleTime = -1f;

        public GameObjectPoolGroup(
            GameObjectPoolConfig config,
            IGameObjectPoolResourceProvider provider,
            Transform poolRoot)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Config.Validate();
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _poolRoot = poolRoot;
        }

        public GameObjectPoolConfig Config { get; }
        public int TotalCount => _entries.Count;
        public int ActiveCount => CountActive();
        public int AvailableCount => _inactive.Count;

        public GameObject Rent(string key, Transform parent = null)
        {
            ThrowIfDisposed();
            GameObjectPoolConfig.ValidateKey(key);

            var entry = TakeAvailable(key);
            if (entry == null)
            {
                entry = CreateEntry(key);
            }

            return Activate(entry, parent);
        }

        public async Task<GameObject> RentAsync(
            string key,
            Transform parent,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            GameObjectPoolConfig.ValidateKey(key);

            var entry = TakeAvailable(key);
            if (entry != null)
            {
                return Activate(entry, parent);
            }

            Task<GameObject> pending;
            if (!_pending.TryGetValue(key, out pending))
            {
                pending = CreateEntryAsync(key, _generation);
                _pending[key] = pending;
                try
                {
                    var created = await AwaitWithCancellation(pending, cancellationToken);
                    if (created == null)
                    {
                        return null;
                    }
                }
                finally
                {
                    Task<GameObject> current;
                    if (pending.IsCompleted &&
                        _pending.TryGetValue(key, out current) &&
                        ReferenceEquals(current, pending))
                    {
                        _pending.Remove(key);
                    }
                }
            }
            else
            {
                try
                {
                    var created = await AwaitWithCancellation(pending, cancellationToken);
                    if (created == null)
                    {
                        return null;
                    }
                }
                finally
                {
                    if (pending.IsCompleted)
                    {
                        Task<GameObject> current;
                        if (_pending.TryGetValue(key, out current) &&
                            ReferenceEquals(current, pending))
                        {
                            _pending.Remove(key);
                        }
                    }
                }
            }

            // 共享创建只产生一个实例；后续等待者优先领取它，仍需实例时走同步创建，
            // 避免再次启动同 key 的异步资源加载。
            entry = TakeAvailable(key);
            if (entry == null)
            {
                entry = CreateEntry(key);
            }

            return Activate(entry, parent);
        }

        public void Preload(string key, int count, Transform parent = null)
        {
            ThrowIfDisposed();
            GameObjectPoolConfig.ValidateKey(key);
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            while (GetAvailableCount(key) < count)
            {
                if (CreateEntry(key) == null)
                {
                    break;
                }
            }
        }

        public async Task PreloadAsync(
            string key,
            int count,
            Transform parent,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            GameObjectPoolConfig.ValidateKey(key);
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            while (GetAvailableCount(key) < count)
            {
                var instance = await CreateEntryAsyncShared(key, cancellationToken);
                if (instance == null)
                {
                    break;
                }
            }
        }

        public bool Return(GameObject instance)
        {
            if (_disposed || instance == null ||
                !_entries.TryGetValue(instance.GetInstanceID(), out var entry) ||
                !ReferenceEquals(entry.Instance, instance) ||
                !entry.InUse)
            {
                return false;
            }

            if (entry.Temporary)
            {
                DestroyEntry(entry);
                return true;
            }

            entry.InUse = false;
            entry.ReleasedAt = Time.unscaledTime;
            instance.SetActive(false);
            ResetTransform(instance);
            AddAvailable(entry);
            MarkIdleTimeIfNeeded();
            return true;
        }

        internal bool Remove(GameObject instance)
        {
            if (_disposed || instance == null ||
                !_entries.TryGetValue(instance.GetInstanceID(), out var entry) ||
                !ReferenceEquals(entry.Instance, instance))
            {
                return false;
            }

            DestroyEntry(entry);
            return true;
        }

        public int Cleanup()
        {
            if (_disposed || Config.ExpireTime <= 0f)
            {
                return 0;
            }

            var count = 0;
            var now = Time.unscaledTime;
            var node = _inactive.First;
            while (node != null)
            {
                var next = node.Next;
                if (now - node.Value.ReleasedAt >= Config.ExpireTime)
                {
                    DestroyEntry(node.Value);
                    count++;
                }

                node = next;
            }

            MarkIdleTimeIfNeeded();
            return count;
        }

        public void Clear()
        {
            _generation++;
            _pending.Clear();
            foreach (var entry in new List<Entry>(_entries.Values))
            {
                DestroyEntry(entry);
            }

            _available.Clear();
            _inactive.Clear();
            _entries.Clear();
            _loadedKeys.Clear();
            _lastIdleTime = -1f;
        }

        public GameObjectPoolGroupSnapshot CreateSnapshot()
        {
            return new GameObjectPoolGroupSnapshot(
                Config.Name,
                Config.Key,
                Config.Capacity,
                Config.ExpireTime,
                Config.AutoDestroyTime,
                Config.DontDestroy,
                TotalCount,
                ActiveCount,
                AvailableCount,
                new List<string>(_loadedKeys));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Clear();
            _disposed = true;
        }

        internal bool Owns(GameObject instance)
        {
            return instance != null &&
                   _entries.TryGetValue(instance.GetInstanceID(), out var entry) &&
                   ReferenceEquals(entry.Instance, instance);
        }

        internal void NotifyDestroyed(int instanceId, GameObject instance)
        {
            Entry entry;
            if (_entries.TryGetValue(instanceId, out entry))
            {
                RemoveEntry(entry);
            }
        }

        internal bool CanAutoDestroy()
        {
            return !Config.DontDestroy &&
                   Config.AutoDestroyTime > 0f &&
                   ActiveCount == 0 &&
                   _lastIdleTime >= 0f &&
                   Time.unscaledTime - _lastIdleTime >= Config.AutoDestroyTime;
        }

        private Entry CreateEntry(string key)
        {
            var entry = CreateEntryCore(key, _provider.Create(key, _poolRoot));
            return entry;
        }

        private async Task<GameObject> CreateEntryAsyncShared(
            string key,
            CancellationToken cancellationToken)
        {
            Task<GameObject> pending;
            if (!_pending.TryGetValue(key, out pending))
            {
                pending = CreateEntryAsync(key, _generation);
                _pending[key] = pending;
            }

            try
            {
                return await AwaitWithCancellation(pending, cancellationToken);
            }
            finally
            {
                if (pending.IsCompleted &&
                    _pending.TryGetValue(key, out var current) &&
                    ReferenceEquals(current, pending))
                {
                    _pending.Remove(key);
                }
            }
        }

        private async Task<GameObject> CreateEntryAsync(string key, int generation)
        {
            GameObject instance = null;
            try
            {
                // 不把任一租用者的取消 token 传给共享创建操作。
                instance = await _provider.CreateAsync(key, _poolRoot, CancellationToken.None);
                if (_disposed || generation != _generation)
                {
                    if (instance != null)
                    {
                        _provider.Destroy(instance);
                    }

                    return null;
                }

                var entry = CreateEntryCore(key, instance);
                return entry == null ? null : entry.Instance;
            }
            catch
            {
                if (instance != null && !_entries.ContainsKey(instance.GetInstanceID()))
                {
                    _provider.Destroy(instance);
                }

                throw;
            }
        }

        private Entry CreateEntryCore(string key, GameObject instance)
        {
            if (instance == null)
            {
                return null;
            }

            if (!CanCreate())
            {
                if (!EvictOldestAvailable())
                {
                    if (Config.OverflowMode != GameObjectPoolOverflowMode.Temporary)
                    {
                        _provider.Destroy(instance);
                        return null;
                    }
                }
            }

            var entry = new Entry
            {
                Key = key,
                Instance = instance,
                InstanceId = instance.GetInstanceID(),
                ReleasedAt = Time.unscaledTime,
                Temporary = TotalCount >= Config.Capacity &&
                            Config.OverflowMode == GameObjectPoolOverflowMode.Temporary
            };
            _entries.Add(entry.InstanceId, entry);
            _loadedKeys.Add(key);

            var monitor = instance.GetComponent<GameObjectPoolMonitor>() ??
                          instance.AddComponent<GameObjectPoolMonitor>();
            monitor.Initialize(this, entry.InstanceId);
            instance.SetActive(false);
            ResetTransform(instance);
            AddAvailable(entry);
            return entry;
        }

        private Entry TakeAvailable(string key)
        {
            List<Entry> entries;
            if (!_available.TryGetValue(key, out entries))
            {
                return null;
            }

            while (entries.Count > 0)
            {
                var index = entries.Count - 1;
                var entry = entries[index];
                entries.RemoveAt(index);
                RemoveInactiveNode(entry);
                if (entry.Instance != null)
                {
                    return entry;
                }

                RemoveEntry(entry);
            }

            return null;
        }

        private GameObject Activate(Entry entry, Transform parent)
        {
            if (entry == null || entry.Instance == null)
            {
                return null;
            }

            // 新创建的对象先登记为可用，正式借出时必须从两个 inactive 链表移除。
            RemoveInactiveNode(entry);
            RemoveAvailable(entry);
            entry.InUse = true;
            entry.Instance.transform.SetParent(parent, false);
            entry.Instance.SetActive(true);
            return entry.Instance;
        }

        private void AddAvailable(Entry entry)
        {
            List<Entry> entries;
            if (!_available.TryGetValue(entry.Key, out entries))
            {
                entries = new List<Entry>();
                _available.Add(entry.Key, entries);
            }

            if (entry.InactiveNode == null)
            {
                entry.InactiveNode = _inactive.AddLast(entry);
            }

            entries.Add(entry);
            MarkIdleTimeIfNeeded();
        }

        private bool CanCreate()
        {
            return Config.Capacity > TotalCount;
        }

        private bool EvictOldestAvailable()
        {
            var oldest = _inactive.First;
            if (oldest == null)
            {
                return false;
            }

            DestroyEntry(oldest.Value);
            return true;
        }

        private void DestroyEntry(Entry entry)
        {
            if (entry == null)
            {
                return;
            }

            var instance = entry.Instance;
            RemoveEntry(entry);
            if (instance != null)
            {
                var monitor = instance.GetComponent<GameObjectPoolMonitor>();
                if (monitor != null)
                {
                    monitor.Detach();
                }

                _provider.Destroy(instance);
            }
        }

        private void RemoveEntry(Entry entry)
        {
            if (entry == null)
            {
                return;
            }

            RemoveInactiveNode(entry);
            RemoveAvailable(entry);

            _entries.Remove(entry.InstanceId);
            if (!HasEntryForKey(entry.Key))
            {
                _loadedKeys.Remove(entry.Key);
            }

            MarkIdleTimeIfNeeded();
        }

        private void RemoveAvailable(Entry entry)
        {
            List<Entry> available;
            if (!_available.TryGetValue(entry.Key, out available))
            {
                return;
            }

            available.Remove(entry);
            if (available.Count == 0)
            {
                _available.Remove(entry.Key);
            }
        }

        private void RemoveInactiveNode(Entry entry)
        {
            if (entry.InactiveNode == null)
            {
                return;
            }

            _inactive.Remove(entry.InactiveNode);
            entry.InactiveNode = null;
        }

        private int GetAvailableCount(string key)
        {
            return _available.TryGetValue(key, out var entries) ? entries.Count : 0;
        }

        private void MarkIdleTimeIfNeeded()
        {
            _lastIdleTime = ActiveCount == 0 ? Time.unscaledTime : -1f;
        }

        private void ResetTransform(GameObject instance)
        {
            if (_poolRoot != null)
            {
                instance.transform.SetParent(_poolRoot, false);
            }

            if (Config.ResetTransform)
            {
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
            }
        }

        private bool HasEntryForKey(string key)
        {
            foreach (var entry in _entries.Values)
            {
                if (string.Equals(entry.Key, key, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private int CountActive()
        {
            var count = 0;
            foreach (var entry in _entries.Values)
            {
                if (entry.InUse)
                {
                    count++;
                }
            }

            return count;
        }

        private static async Task<T> AwaitWithCancellation<T>(
            Task<T> task,
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                return await task;
            }

            var cancellation = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(
                       () => cancellation.TrySetResult(true)))
            {
                var completed = await Task.WhenAny(task, cancellation.Task);
                if (completed != task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await task;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GameObjectPoolGroup));
            }
        }
    }
}
