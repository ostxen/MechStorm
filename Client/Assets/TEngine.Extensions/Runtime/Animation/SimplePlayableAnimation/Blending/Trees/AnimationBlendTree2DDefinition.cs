using System;
using UnityEngine;
using UnityEngine.Playables;

namespace TEngine.Extensions.SimpleAnimation
{
    public sealed class AnimationBlendTreeSample2D
    {
        internal AnimationBlendTreeMotion Motion { get; }
        public Vector2 Position { get; }

        public AnimationBlendTreeSample2D(string clipKey, Vector2 position)
        {
            Motion = new AnimationBlendTreeMotion(clipKey);
            BlendParameterValidation.Check(position);
            Position = position;
        }

        public AnimationBlendTreeSample2D(AnimationBlendTreeDefinition tree, Vector2 position)
        {
            Motion = new AnimationBlendTreeMotion(tree);
            BlendParameterValidation.Check(position);
            Position = position;
        }
    }

    /// <summary>默认使用 Cartesian Gradient Band 权重；可注入方向或 Compute 计算器，计算器的释放由调用方负责。</summary>
    public sealed class AnimationBlendTree2DDefinition : AnimationBlendTreeDefinition
    {
        private readonly AnimationBlendTreeSample2D[] _samples;
        private readonly Vector2[] _positions;
        private readonly Vector2 _parameter;
        private readonly IBlendWeightCalculator2D _calculator;

        public AnimationBlendTree2DDefinition(AnimationBlendTreeSample2D[] samples,
            Vector2 parameter = default, IBlendWeightCalculator2D calculator = null)
        {
            if (samples == null || samples.Length == 0)
                throw new ArgumentException("至少需要一个样本。", nameof(samples));
            BlendParameterValidation.Check(parameter);
            _samples = (AnimationBlendTreeSample2D[])samples.Clone();
            _positions = new Vector2[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                if (_samples[i] == null) throw new ArgumentException("样本不能为空。", nameof(samples));
                _positions[i] = _samples[i].Position;
            }
            AnimationBlendTree2D.ValidateThresholds(_positions);
            _parameter = parameter;
            _calculator = calculator;
        }

        internal override AnimationBlendTree CreateNode(PlayableGraph graph, Func<string, AnimationClip> resolveClip)
        {
            var node = new AnimationBlendTree2D(graph, _positions, _calculator);
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
