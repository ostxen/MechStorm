using UnityEngine;

namespace TEngine.Extensions.GameObjectPool
{
    [DisallowMultipleComponent]
    internal sealed class GameObjectPoolMonitor : MonoBehaviour
    {
        private GameObjectPoolGroup _owner;
        private int _instanceId;

        internal GameObjectPoolGroup Owner => _owner;

        internal void Initialize(GameObjectPoolGroup owner, int instanceId)
        {
            _owner = owner;
            _instanceId = instanceId;
        }

        internal void Detach()
        {
            _owner = null;
        }

        private void OnDestroy()
        {
            _owner?.NotifyDestroyed(_instanceId, gameObject);
        }
    }
}
