using TEngine.Extensions.VirtualizedList;
using UnityEngine;

namespace TEngine.Extensions.VirtualizedList
{
    public static class VirtualizedListResourceAdapter
    {
        public static void Install()
        {
            VirtualizedListServices.PrefabLoader = (key, parent) =>
                ModuleSystem.GetModule<IResourceModule>().LoadGameObject(key, parent: parent);
        }
    }
}
