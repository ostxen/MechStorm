using System;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 使用秒数和字符串 Id 标记动画中的触发点。
    /// </summary>
    [Serializable]
    public sealed class AnimationEventMarker
    {
        [SerializeField]
        private string _id;

        [SerializeField]
        private float _time;

        public AnimationEventMarker(string id, float time)
        {
            _id = id;
            _time = Mathf.Max(0f, time);
        }

        public string Id => _id;

        public float Time => _time;
    }
}
