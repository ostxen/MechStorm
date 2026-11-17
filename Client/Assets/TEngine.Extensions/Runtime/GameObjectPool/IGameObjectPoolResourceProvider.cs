using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TEngine.Extensions.GameObjectPool
{
    /// <summary>
    /// GameObject 的创建/销毁边界。异步操作由池共享，调用方取消不会取消 provider 操作。
    /// </summary>
    public interface IGameObjectPoolResourceProvider
    {
        GameObject Create(string key, Transform parent);

        Task<GameObject> CreateAsync(
            string key,
            Transform parent,
            CancellationToken cancellationToken);

        void Destroy(GameObject instance);
    }
}
