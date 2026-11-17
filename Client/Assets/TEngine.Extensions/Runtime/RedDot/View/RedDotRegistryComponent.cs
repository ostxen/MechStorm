using TEngine.Extensions.RedDot;
using UnityEngine;

namespace TEngine.Extensions.RedDot.View
{
    [AddComponentMenu("TEngine/Red Dot/Registry")]
    public sealed class RedDotRegistryComponent : MonoBehaviour
    {
        public RedDotRegistry Registry { get; } = new();

        public RedDotState Set(string key, bool active) => Registry.Set(key, active);
        public RedDotState Set(string key, int count) => Registry.Set(key, count);
        public void DefineRelation(string parentKey, RedDotRelationMode mode, params string[] childKeys) =>
            Registry.DefineRelation(parentKey, mode, childKeys);
        public void DefineRelation(
            string parentKey,
            RedDotRelationMode mode,
            RedDotDisplayType displayType,
            params string[] childKeys) =>
            Registry.DefineRelation(parentKey, mode, displayType, childKeys);
    }
}
