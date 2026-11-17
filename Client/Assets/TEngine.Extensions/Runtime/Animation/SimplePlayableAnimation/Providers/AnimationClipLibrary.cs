using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    [Serializable]
    public sealed class AnimationClipLibraryEntry
    {
        [SerializeField]
        [Tooltip("用于查找和播放动画的稳定标识，集合内唯一。")]
        private string _animationKey;

        [SerializeField]
        private AnimationClip _clip;

        [SerializeField]
        private WrapMode _wrapMode = WrapMode.Loop;

        [SerializeField]
        private int _layer;

        /// <summary>
        /// 用于查找和播放动画的稳定标识，集合内唯一。
        /// </summary>
        public string AnimationKey => _animationKey;

        public AnimationClip Clip => _clip;

        public WrapMode WrapMode => _wrapMode;

        public int Layer => _layer;
    }

    /// <summary>
    /// 保存动画标识与 Clip 引用，供 Controller 同步绑定动画集合。
    /// </summary>
    [CreateAssetMenu(
        fileName = "AnimationClipLibrary",
        menuName = "TEngine/Animation/Clip Library")]
    public sealed class AnimationClipLibrary : ScriptableObject
    {
        [SerializeField]
        private List<AnimationClipLibraryEntry> _entries =
            new List<AnimationClipLibraryEntry>();

        public IReadOnlyList<AnimationClipLibraryEntry> Entries => _entries;

        public bool TryGet(
            string animationKey,
            out AnimationClipLibraryEntry entry)
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
