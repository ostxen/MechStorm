using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TEngine.Extensions.GameObjectPool
{
    /// <summary>
    /// 通用 GameObject 对象池入口。资源加载和资源释放由 provider 负责。
    /// </summary>
    public sealed class GameObjectPool : IDisposable
    {
        private readonly IGameObjectPoolResourceProvider _provider;
        private readonly Transform _poolRoot;
        private readonly List<GameObjectPoolGroup> _groups = new List<GameObjectPoolGroup>();
        private readonly GameObjectPoolGroup _defaultGroup;
        private bool _disposed;

        public GameObjectPool(
            IGameObjectPoolResourceProvider provider,
            Transform poolRoot = null,
            IEnumerable<GameObjectPoolConfig> configs = null)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _poolRoot = poolRoot;
            _defaultGroup = CreateGroup(GameObjectPoolConfig.Default);

            if (configs != null)
            {
                foreach (var config in configs)
                {
                    Configure(config);
                }
            }

            GameObjectPoolRegistry.Register(this);
        }

        public GameObjectPool(
            IGameObjectPoolResourceProvider provider,
            GameObjectPoolConfigAsset configAsset,
            Transform poolRoot = null)
            : this(provider, poolRoot, configAsset == null ? null : configAsset.Configs)
        {
        }

        public IReadOnlyList<GameObjectPoolGroup> Groups => _groups;

        public GameObjectPoolGroup GetGroup(string key)
        {
            ThrowIfDisposed();
            return FindGroup(key);
        }

        public bool TryGetGroup(string name, out GameObjectPoolGroup group)
        {
            ThrowIfDisposed();
            group = _groups.FirstOrDefault(item =>
                string.Equals(item.Config.Name, name, StringComparison.Ordinal));
            return group != null;
        }

        public GameObject Rent(string key, Transform parent = null)
        {
            ThrowIfDisposed();
            return FindGroup(key).Rent(key, parent);
        }

        public GameObject Get(string key, Transform parent = null)
        {
            return Rent(key, parent);
        }

        public GameObject Spawn(string key, Transform parent = null)
        {
            return Rent(key, parent);
        }

        public async Task<GameObject> RentAsync(
            string key,
            Transform parent = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();
            return await FindGroup(key).RentAsync(key, parent, cancellationToken);
        }

        public Task<GameObject> GetAsync(
            string key,
            Transform parent = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return RentAsync(key, parent, cancellationToken);
        }

        public Task<GameObject> SpawnAsync(
            string key,
            Transform parent = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return RentAsync(key, parent, cancellationToken);
        }

        public bool Return(GameObject instance)
        {
            if (_disposed || instance == null)
            {
                return false;
            }

            GameObjectPoolGroup group = null;
            if (_defaultGroup.Owns(instance))
            {
                group = _defaultGroup;
            }
            else
            {
                for (var i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].Owns(instance))
                    {
                        group = _groups[i];
                        break;
                    }
                }
            }

            if (group == null)
            {
                // 未由本池创建的对象不应被静默销毁，交由调用方处理。
                return false;
            }

            return group.Return(instance);
        }

        public bool Release(GameObject instance)
        {
            return Return(instance);
        }

        public bool Recycle(GameObject instance)
        {
            return Return(instance);
        }

        public bool Remove(GameObject instance)
        {
            if (_disposed || instance == null)
            {
                return false;
            }

            if (_defaultGroup.Owns(instance))
            {
                return _defaultGroup.Remove(instance);
            }

            for (var i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Owns(instance))
                {
                    return _groups[i].Remove(instance);
                }
            }

            return false;
        }

        public void Preload(string key, int count = -1, Transform parent = null)
        {
            ThrowIfDisposed();
            var group = FindGroup(key);
            group.Preload(key, count < 0 ? group.Config.PreloadCount : count, parent);
        }

        public Task PreloadAsync(
            string key,
            int count = -1,
            Transform parent = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();
            var group = FindGroup(key);
            return group.PreloadAsync(
                key,
                count < 0 ? group.Config.PreloadCount : count,
                parent,
                cancellationToken);
        }

        public int Cleanup()
        {
            ThrowIfDisposed();
            var destroyed = 0;
            foreach (var group in _groups)
            {
                destroyed += group.Cleanup();
                if (group.CanAutoDestroy())
                {
                    destroyed += group.TotalCount;
                    group.Clear();
                }
            }

            destroyed += _defaultGroup.Cleanup();
            return destroyed;
        }

        public void Clear()
        {
            Clear(true);
        }

        public void Clear(bool includePersistent)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var group in _groups.ToArray())
            {
                if (includePersistent || !group.Config.DontDestroy)
                {
                    group.Clear();
                }
            }

            _defaultGroup.Clear();
        }

        public IReadOnlyList<GameObjectPoolGroupSnapshot> GetSnapshots()
        {
            var snapshots = new List<GameObjectPoolGroupSnapshot>(_groups.Count + 1);
            snapshots.Add(_defaultGroup.CreateSnapshot());
            snapshots.AddRange(_groups.Select(group => group.CreateSnapshot()));
            return snapshots;
        }

        public void Configure(GameObjectPoolConfig config)
        {
            ThrowIfDisposed();
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            config.Validate();
            var oldGroup = _groups.FirstOrDefault(group => group.Config.Name == config.Name);
            if (oldGroup != null)
            {
                oldGroup.Clear();
                _groups.Remove(oldGroup);
            }

            _groups.Add(CreateGroup(config));
            _groups.Sort((left, right) =>
                right.Config.Specificity.CompareTo(left.Config.Specificity));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Clear();
            foreach (var group in _groups)
            {
                group.Dispose();
            }

            _defaultGroup.Dispose();
            _groups.Clear();
            _disposed = true;
            GameObjectPoolRegistry.Unregister(this);
        }

        private GameObjectPoolGroup FindGroup(string key)
        {
            GameObjectPoolConfig.ValidateKey(key);
            for (var i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Config.Matches(key))
                {
                    return _groups[i];
                }
            }

            return _defaultGroup;
        }

        private GameObjectPoolGroup CreateGroup(GameObjectPoolConfig config)
        {
            return new GameObjectPoolGroup(config, _provider, _poolRoot);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GameObjectPool));
            }
        }
    }

    /// <summary>
    /// 活跃对象池的弱注册表，仅供 Editor 监控，不影响池的使用方式。
    /// </summary>
    public static class GameObjectPoolRegistry
    {
        private static readonly List<WeakReference<GameObjectPool>> Pools =
            new List<WeakReference<GameObjectPool>>();

        internal static void Register(GameObjectPool pool)
        {
            Pools.Add(new WeakReference<GameObjectPool>(pool));
        }

        internal static void Unregister(GameObjectPool pool)
        {
            for (var i = Pools.Count - 1; i >= 0; i--)
            {
                GameObjectPool target;
                if (!Pools[i].TryGetTarget(out target) || ReferenceEquals(target, pool))
                {
                    Pools.RemoveAt(i);
                }
            }
        }

        public static IReadOnlyList<GameObjectPoolSnapshot> GetSnapshots()
        {
            var result = new List<GameObjectPoolSnapshot>();
            for (var i = Pools.Count - 1; i >= 0; i--)
            {
                GameObjectPool pool;
                if (!Pools[i].TryGetTarget(out pool))
                {
                    Pools.RemoveAt(i);
                    continue;
                }

                result.Add(new GameObjectPoolSnapshot(pool.GetSnapshots()));
            }

            return result;
        }

        public static int CleanupAll()
        {
            var count = 0;
            for (var i = Pools.Count - 1; i >= 0; i--)
            {
                GameObjectPool pool;
                if (!Pools[i].TryGetTarget(out pool))
                {
                    Pools.RemoveAt(i);
                    continue;
                }

                count += pool.Cleanup();
            }

            return count;
        }
    }
}
