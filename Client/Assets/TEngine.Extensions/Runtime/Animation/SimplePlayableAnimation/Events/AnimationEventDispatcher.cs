using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 检查播放时间跨越的事件标记，并将命中的字符串 Id 交给调用方处理。
    /// </summary>
    public sealed class AnimationEventDispatcher
    {
        private readonly List<AnimationEventMarker> _markers =
            new List<AnimationEventMarker>();

        public IReadOnlyList<AnimationEventMarker> Markers => _markers;

        public void AddMarker(AnimationEventMarker marker)
        {
            if (marker == null)
            {
                throw new ArgumentNullException(nameof(marker));
            }

            _markers.Add(marker);
            _markers.Sort((left, right) => left.Time.CompareTo(right.Time));
        }

        public void Clear()
        {
            _markers.Clear();
        }

        public void Dispatch(
            float previousTime,
            float currentTime,
            float clipLength,
            bool looping,
            Action<AnimationEventMarker> callback)
        {
            if (callback == null || clipLength <= 0f || _markers.Count == 0)
            {
                return;
            }

            if (!looping)
            {
                DispatchRange(previousTime, currentTime, callback);
                return;
            }

            if (currentTime < previousTime)
            {
                return;
            }

            var previousCycle = Mathf.FloorToInt(previousTime / clipLength);
            var currentCycle = Mathf.FloorToInt(currentTime / clipLength);
            var previousOffset = previousTime - previousCycle * clipLength;
            var currentOffset = currentTime - currentCycle * clipLength;

            if (previousCycle == currentCycle)
            {
                DispatchRange(previousOffset, currentOffset, callback);
                return;
            }

            DispatchRange(previousOffset, clipLength, callback);
            for (int cycle = previousCycle + 1;
                 cycle < currentCycle;
                 cycle++)
            {
                DispatchRange(0f, clipLength, callback);
            }

            DispatchRange(0f, currentOffset, callback);
        }

        private void DispatchRange(
            float startTime,
            float endTime,
            Action<AnimationEventMarker> callback)
        {
            for (int i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];
                if (marker.Time > startTime && marker.Time <= endTime)
                {
                    callback(marker);
                }
            }
        }
    }
}
