using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    internal static class BlendParameterValidation
    {
        public static void Check(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "混合参数和样本必须为有限数值。");
        }

        public static void Check(Vector2 value)
        {
            Check(value.x);
            Check(value.y);
        }

        public static void CheckSamples(IReadOnlyList<Vector2> samples)
        {
            if (samples == null || samples.Count == 0)
                throw new ArgumentException("至少需要一个二维样本。", nameof(samples));
            for (int i = 0; i < samples.Count; i++)
            {
                Check(samples[i]);
                for (int j = 0; j < i; j++)
                    if (samples[i].Equals(samples[j]))
                        throw new ArgumentException("二维样本位置不能重复。", nameof(samples));
            }
        }
    }
}
