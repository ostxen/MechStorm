using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TEngine.Extensions.GameObjectPool.TEngine
{
    public interface IGameObjectPoolModule
    {
        GameObject PoolRoot { get; }

        UniTask<GameObjectPoolModuleGroup> CreateGameObjectPoolAsync(
            string location,
            int initCapacity = 0,
            int maxCapacity = int.MaxValue,
            float autoDestroyTime = -1f,
            bool dontDestroy = false,
            bool allowMultiSpawn = false,
            CancellationToken cancellationToken = default);

        UniTask<GameObject> SpawnAsync(
            string location,
            CancellationToken cancellationToken = default);

        UniTask<GameObject> SpawnAsync(
            string location,
            Transform parent,
            CancellationToken cancellationToken = default);

        UniTask<GameObject> SpawnAsync(
            string location,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            CancellationToken cancellationToken = default);

        void Recycle(GameObject instance);
        void Remove(GameObject instance);
        void DestroyPool(string location);
        void DestroyAllPool(bool includePersistent);
        void GetDebugInfos(List<GameObjectPoolGroupSnapshot> results);
    }
}
