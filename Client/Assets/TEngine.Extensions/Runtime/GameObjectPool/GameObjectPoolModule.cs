using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TEngine;
using TEngine.Extensions.GameObjectPool;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TEngine.Extensions.GameObjectPool.TEngine
{
    /// <summary>
    /// TEngine 模块外观。底层仍使用可替换 Provider 的 GameObjectPool。
    /// </summary>
    public sealed class TEngineGameObjectPoolModule :
        Module,
        IGameObjectPoolModule,
        IUpdateModule
    {
        private TEngineGameObjectPoolResourceProvider _provider;
        private GameObjectPool _pool;
        private GameObject _poolRoot;

        public GameObject PoolRoot => _poolRoot;

        public override void OnInit()
        {
            _provider = new TEngineGameObjectPoolResourceProvider();
            _poolRoot = new GameObject("[TEngine.GameObjectPool]");
            UnityEngine.Object.DontDestroyOnLoad(_poolRoot);
            _pool = new GameObjectPool(_provider, _poolRoot.transform);
        }

        public override void Shutdown()
        {
            _pool?.Dispose();
            _pool = null;
            if (_poolRoot != null)
            {
                UnityEngine.Object.Destroy(_poolRoot);
                _poolRoot = null;
            }

            _provider = null;
        }

        public async UniTask<GameObjectPoolModuleGroup> CreateGameObjectPoolAsync(
            string location,
            int initCapacity = 0,
            int maxCapacity = int.MaxValue,
            float autoDestroyTime = -1f,
            bool dontDestroy = false,
            bool allowMultiSpawn = false,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            if (!_pool.TryGetGroup(location, out var group))
            {
                var config = new GameObjectPoolConfig(
                    name: location,
                    key: location,
                    capacity: maxCapacity,
                    preloadCount: initCapacity,
                    expireTime: 0f,
                    autoDestroyTime: autoDestroyTime,
                    dontDestroy: dontDestroy,
                    matchMode: GameObjectPoolKeyMatchMode.Exact,
                    overflowMode: allowMultiSpawn
                        ? GameObjectPoolOverflowMode.Temporary
                        : GameObjectPoolOverflowMode.Reject);
                _pool.Configure(config);
                group = _pool.GetGroup(location);
            }

            await _pool.PreloadAsync(location, initCapacity, _poolRoot.transform, cancellationToken);
            return new GameObjectPoolModuleGroup(group);
        }

        public UniTask<GameObject> SpawnAsync(
            string location,
            CancellationToken cancellationToken = default)
        {
            return SpawnAsync(
                location,
                null,
                Vector3.zero,
                Quaternion.identity,
                cancellationToken);
        }

        public UniTask<GameObject> SpawnAsync(
            string location,
            Transform parent,
            CancellationToken cancellationToken = default)
        {
            return SpawnAsync(
                location,
                parent,
                Vector3.zero,
                Quaternion.identity,
                cancellationToken);
        }

        public async UniTask<GameObject> SpawnAsync(
            string location,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            var instance = await _pool.RentAsync(location, parent, cancellationToken);
            if (instance == null)
            {
                return null;
            }

            if (parent == null)
            {
                SceneManager.MoveGameObjectToScene(
                    instance,
                    SceneManager.GetActiveScene());
            }

            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            return instance;
        }

        public void Recycle(GameObject instance)
        {
            EnsureInitialized();
            if (!_pool.Recycle(instance))
            {
                Log.Warning($"GameObject 不属于当前扩展对象池，忽略回收: {instance?.name}");
            }
        }

        public void Remove(GameObject instance)
        {
            EnsureInitialized();
            if (!_pool.Remove(instance))
            {
                Log.Warning($"GameObject 不属于当前扩展对象池，忽略销毁: {instance?.name}");
            }
        }

        public void DestroyPool(string location)
        {
            EnsureInitialized();
            if (_pool.TryGetGroup(location, out var group))
            {
                group.Clear();
            }
        }

        public void DestroyAllPool(bool includePersistent)
        {
            EnsureInitialized();
            _pool.Clear(includePersistent);
        }

        public void GetDebugInfos(List<GameObjectPoolGroupSnapshot> results)
        {
            EnsureInitialized();
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            results.AddRange(_pool.GetSnapshots());
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            _pool?.Cleanup();
        }

        private void EnsureInitialized()
        {
            if (_pool == null)
            {
                throw new InvalidOperationException(
                    "TEngineGameObjectPoolModule is not registered or initialized.");
            }
        }
    }
}
