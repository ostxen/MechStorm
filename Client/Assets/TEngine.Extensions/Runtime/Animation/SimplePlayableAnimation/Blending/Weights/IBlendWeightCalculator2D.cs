using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 根据二维参数和样本位置计算权重，将结果写入调用方缓冲的前 thresholds.Count 个元素。
    /// CPU 实现要求样本非空、有限且不重复，可缓存样本数据；计算器实例不支持并发调用。
    /// </summary>
    public interface IBlendWeightCalculator2D
    {
        void Calculate(Vector2 parameter, IReadOnlyList<Vector2> thresholds, IList<float> weights);
    }
}
