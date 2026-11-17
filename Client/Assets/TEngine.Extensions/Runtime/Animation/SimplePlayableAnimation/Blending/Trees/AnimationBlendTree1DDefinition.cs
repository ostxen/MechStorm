using System;
using UnityEngine;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    public sealed class AnimationBlendTreeSample1D
    {
        internal AnimationBlendTreeMotion Motion { get; }
        public float Threshold { get; }

        public AnimationBlendTreeSample1D(string clipKey, float threshold)
        {
            Motion = new AnimationBlendTreeMotion(clipKey);
            BlendParameterValidation.Check(threshold);
            Threshold = threshold;
        }

        public AnimationBlendTreeSample1D(AnimationBlendTreeDefinition tree, float threshold)
        {
            Motion = new AnimationBlendTreeMotion(tree);
            BlendParameterValidation.Check(threshold);
            Threshold = threshold;
        }
    }

    /// <summary>Clip Key 或子树与阈值成对配置，阈值必须严格递增。</summary>
    public sealed class AnimationBlendTree1DDefinition : AnimationBlendTreeDefinition
    {
        private readonly AnimationBlendTreeSample1D[] _samples;
        private readonly float[] _thresholds;
        private readonly float _parameter;

        public AnimationBlendTree1DDefinition(AnimationBlendTreeSample1D[] samples, float parameter = 0f)
        {
            if (samples == null || samples.Length == 0)
                throw new ArgumentException("至少需要一个样本。", nameof(samples));
            BlendParameterValidation.Check(parameter);
            _samples = (AnimationBlendTreeSample1D[])samples.Clone();
            _thresholds = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                if (_samples[i] == null) throw new ArgumentException("样本不能为空。", nameof(samples));
                _thresholds[i] = _samples[i].Threshold;
            }
            AnimationBlendTree1D.ValidateThresholds(_thresholds);
            _parameter = parameter;
        }

        internal override AnimationBlendTree CreateNode(PlayableGraph graph, Func<string, AnimationClip> resolveClip)
        {
            var node = new AnimationBlendTree1D(graph, _thresholds);
            try
            {
                foreach (var sample in _samples)
                {
                    var child = sample.Motion.CreateNode(graph, resolveClip);
                    try { node.AddChild(child); }
                    catch { child.Dispose(); throw; }
                }
                node.Parameter = _parameter;
                return node;
            }
            catch
            {
                node.Dispose();
                throw;
            }
        }
    }
}
