using System.Threading;
using System.Threading.Tasks;
using TEngine;
using TEngine.Extensions.GameObjectPool;
using UnityEngine;

namespace TEngine.Extensions.GameObjectPool.TEngine
{
    /// <summary>
    /// TEngine 资源模块适配。LoadGameObject 创建的实例由 AssetsReference 在销毁时释放。
    /// </summary>
    public sealed class TEngineGameObjectPoolResourceProvider :
        IGameObjectPoolResourceProvider
    {
        private IResourceModule Resource => ModuleSystem.GetModule<IResourceModule>();

        public GameObject Create(string key, Transform parent)
        {
            return Resource.LoadGameObject(key, parent);
        }

        public async Task<GameObject> CreateAsync(
            string key,
            Transform parent,
            CancellationToken cancellationToken)
        {
            return await Resource.LoadGameObjectAsync(key, parent, cancellationToken);
        }

        public void Destroy(GameObject instance)
        {
            if (instance != null)
            {
                Object.Destroy(instance);
            }
        }
    }
}
