using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    [Serializable]
    public sealed class AnimationClipCatalogEntry
    {
        [SerializeField]
        [Tooltip("用于查找和播放动画的稳定标识，集合内唯一。")]
        private string _animationKey;

        [SerializeField]
        private string _location;

        [SerializeField]
        private string _packageName;

        [SerializeField]
        private WrapMode _wrapMode = WrapMode.Loop;

        [SerializeField]
        private int _layer;

        /// <summary>
        /// 用于查找和播放动画的稳定标识，集合内唯一。
        /// </summary>
        public string AnimationKey => _animationKey;

        public string Location => _location;

        public string PackageName => _packageName;

        public WrapMode WrapMode => _wrapMode;

        public int Layer => _layer;
    }

    /// <summary>
    /// 将动画标识映射到资源包和加载地址，供 Provider 异步加载 Clip。
    /// </summary>
    [CreateAssetMenu(
        fileName = "AnimationClipCatalog",
        menuName = "TEngine/Animation/Clip Catalog")]
    public sealed class AnimationClipCatalog : ScriptableObject
    {
        [SerializeField]
        private List<AnimationClipCatalogEntry> _entries =
            new List<AnimationClipCatalogEntry>();

        public IReadOnlyList<AnimationClipCatalogEntry> Entries => _entries;

        public bool TryGet(
            string animationKey,
            out AnimationClipCatalogEntry entry)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var candidate = _entries[i];
                if (candidate != null &&
                    string.Equals(
                        candidate.AnimationKey,
                        animationKey,
                        StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }
    }
}
