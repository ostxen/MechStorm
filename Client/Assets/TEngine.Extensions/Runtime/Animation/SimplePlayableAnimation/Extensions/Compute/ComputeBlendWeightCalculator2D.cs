using System;
using System.Collections.Generic;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation
{
    /// <summary>
    /// 使用构造时的二维样本派发 ComputeShader，同步读取结果并归一化；Dispose 释放 GPU 缓冲。
    /// </summary>
    public sealed class ComputeBlendWeightCalculator2D
        : IBlendWeightCalculator2D, IDisposable
    {
        private readonly ComputeShader _shader;
        private readonly int _kernel;
        private readonly ComputeBuffer _thresholdBuffer;
        private readonly ComputeBuffer _weightBuffer;
        private readonly Vector2[] _thresholds;
        private readonly Vector2[] _weightReadback;
        private bool _disposed;

        public ComputeBlendWeightCalculator2D(
            ComputeShader shader,
            IReadOnlyList<Vector2> thresholds)
        {
            _shader = shader != null
                ? shader
                : throw new ArgumentNullException(nameof(shader));

            if (thresholds == null || thresholds.Count == 0)
            {
                throw new ArgumentException("至少需要一个阈值。", nameof(thresholds));
            }

            _kernel = shader.FindKernel("CSMain");
            _thresholds = new Vector2[thresholds.Count];
            _weightReadback = new Vector2[thresholds.Count];
            for (int i = 0; i < thresholds.Count; i++)
            {
                BlendParameterValidation.Check(thresholds[i]);
                _thresholds[i] = thresholds[i];
            }

            try
            {
                _thresholdBuffer = new ComputeBuffer(thresholds.Count, sizeof(float) * 2);
                _weightBuffer = new ComputeBuffer(thresholds.Count, sizeof(float) * 2);
                _thresholdBuffer.SetData(_thresholds);
            }
            catch
            {
                _thresholdBuffer?.Release();
                _weightBuffer?.Release();
                throw;
            }
        }

        public void Calculate(
            Vector2 parameter,
            IReadOnlyList<Vector2> thresholds,
            IList<float> weights)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ComputeBlendWeightCalculator2D));
            }

            if (thresholds == null ||
                thresholds.Count != _thresholds.Length ||
                weights == null ||
                weights.Count < thresholds.Count)
            {
                throw new ArgumentException("计算输入必须与构造时的阈值数量一致。");
            }

            BlendParameterValidation.Check(parameter);
            for (int i = 0; i < thresholds.Count; i++)
            {
                BlendParameterValidation.Check(thresholds[i]);
                if (!thresholds[i].Equals(_thresholds[i]))
                    throw new ArgumentException("Compute 样本位置和顺序必须与构造时一致。");
            }
            _shader.SetVector("_Parameter", parameter);
            _shader.SetInt("_Count", _thresholds.Length);
            _shader.SetBuffer(_kernel, "_Thresholds", _thresholdBuffer);
            _shader.SetBuffer(_kernel, "_Weights", _weightBuffer);
            _shader.Dispatch(_kernel, (_thresholds.Length + 15) / 16, 1, 1);
            _weightBuffer.GetData(_weightReadback);

            var total = 0d;
            for (int i = 0; i < thresholds.Count; i++)
            {
                BlendParameterValidation.Check(_weightReadback[i].x);
                weights[i] = Mathf.Max(0f, _weightReadback[i].x);
                total += weights[i];
            }

            if (total <= Mathf.Epsilon)
            {
                return;
            }

            var inverseTotal = 1d / total;
            for (int i = 0; i < thresholds.Count; i++)
            {
                weights[i] = (float)(weights[i] * inverseTotal);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _thresholdBuffer?.Release();
            _weightBuffer?.Release();
        }
    }
}
