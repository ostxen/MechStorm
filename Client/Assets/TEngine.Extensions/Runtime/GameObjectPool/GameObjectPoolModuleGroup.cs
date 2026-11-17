using TEngine.Extensions.GameObjectPool;

namespace TEngine.Extensions.GameObjectPool.TEngine
{
    public sealed class GameObjectPoolModuleGroup
    {
        internal GameObjectPoolModuleGroup(GameObjectPoolGroup group)
        {
            Group = group;
        }

        public GameObjectPoolGroup Group { get; }
    }
}
