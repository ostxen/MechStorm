using System;
using NUnit.Framework;
using UnityEngine;

namespace TEngine.Extensions.SimpleAnimation.Tests
{
    public sealed class BlendWeightCalculatorTests
    {
        [Test]
        public void Cartesian_CollinearSamplesUseAdjacentWeights()
        {
            var calculator = new CartesianBlendWeightCalculator2D();
            var samples = new[] { Vector2.zero, Vector2.right, Vector2.right * 3f };
            var weights = new float[3];
            calculator.Calculate(Vector2.right * 0.25f, samples, weights);
            AssertWeights(weights, 0.75f, 0.25f, 0f);
            calculator.Calculate(Vector2.right * 2f, samples, weights);
            AssertWeights(weights, 0f, 0.5f, 0.5f);
            calculator.Calculate(Vector2.right * 5f, samples, weights);
            AssertWeights(weights, 0f, 0f, 1f);
        }

        [Test]
        public void Cartesian_SymmetricSquareHasEqualCenterWeights()
        {
            var weights = new float[4];
            new CartesianBlendWeightCalculator2D().Calculate(Vector2.zero,
                new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), Vector2.one },
                weights);
            AssertWeights(weights, 0.25f, 0.25f, 0.25f, 0.25f);
        }

        [Test]
        public void Directional_BlendsIdleWalkAndRunByMagnitude()
        {
            var calculator = new DirectionalBlendWeightCalculator2D();
            var samples = new[] { Vector2.zero, Vector2.up, Vector2.up * 3f };
            var weights = new float[3];
            calculator.Calculate(Vector2.up * 0.25f, samples, weights);
            AssertWeights(weights, 0.75f, 0.25f, 0f);
            calculator.Calculate(Vector2.up * 2f, samples, weights);
            AssertWeights(weights, 0f, 0.5f, 0.5f);
            calculator.Calculate(Vector2.up * 4f, samples, weights);
            AssertWeights(weights, 0f, 0f, 1f);
            calculator.Calculate(Vector2.zero, samples, weights);
            AssertWeights(weights, 1f, 0f, 0f);
        }

        [Test]
        public void Directional_DiagonalBlendsAdjacentDirectionsOnly()
        {
            var weights = new float[4];
            new DirectionalBlendWeightCalculator2D().Calculate(Vector2.one.normalized,
                new[] { Vector2.right, Vector2.up, Vector2.left, Vector2.down }, weights);
            AssertWeights(weights, 0.5f, 0.5f, 0f, 0f);
        }

        [Test]
        public void Directional_WrapsAnglesAcrossNegativeXAxis()
        {
            var weights = new float[2];
            new DirectionalBlendWeightCalculator2D().Calculate(Vector2.left,
                new[] { new Vector2(-1f, 0.1f).normalized, new Vector2(-1f, -0.1f).normalized },
                weights);
            AssertWeights(weights, 0.5f, 0.5f);
        }

        [Test]
        public void Directional_OppositeSamplesAreIndependentOfOrderAndArc()
        {
            var calculator = new DirectionalBlendWeightCalculator2D();
            var weights = new float[2];
            foreach (var parameter in new[] { Vector2.up, Vector2.down })
            {
                calculator.Calculate(parameter, new[] { Vector2.left, Vector2.right }, weights);
                AssertWeights(weights, 0.5f, 0.5f);
                calculator.Calculate(parameter, new[] { Vector2.right, Vector2.left }, weights);
                AssertWeights(weights, 0.5f, 0.5f);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExactMatchOverwritesStaleWeights(bool directional)
        {
            var weights = new[] { 0.9f, 0.9f, 0.9f };
            var calculator = CreateCalculator(directional);
            var samples = new[] { Vector2.zero, Vector2.right, Vector2.up };
            calculator.Calculate(Vector2.up, samples, weights);
            AssertWeights(weights, 0f, 0f, 1f);
            calculator.Calculate(Vector2.zero, samples, weights);
            AssertWeights(weights, 1f, 0f, 0f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SingleSampleAlwaysGetsFullWeight(bool directional)
        {
            var weights = new[] { -1f, 7f };
            CreateCalculator(directional).Calculate(new Vector2(float.MaxValue, -float.MaxValue),
                new[] { Vector2.zero }, weights);
            Assert.That(weights[0], Is.EqualTo(1f));
            Assert.That(weights[1], Is.EqualTo(7f), "缓冲区超出样本数量的部分不应被修改。");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CacheTracksSampleContentCountAndOrder(bool directional)
        {
            var calculator = CreateCalculator(directional);
            var samples = new[] { Vector2.zero, Vector2.up * 2f };
            var weights = new float[2];
            calculator.Calculate(Vector2.up, samples, weights);
            AssertWeights(weights, 0.5f, 0.5f);
            samples[1] = Vector2.up * 4f;
            calculator.Calculate(Vector2.up, samples, weights);
            AssertWeights(weights, 0.75f, 0.25f);
            calculator.Calculate(Vector2.up, new[] { Vector2.up * 4f, Vector2.zero }, weights);
            AssertWeights(weights, 0.25f, 0.75f);
            calculator.Calculate(Vector2.up, samples, weights);
            AssertWeights(weights, 0.75f, 0.25f);
            calculator.Calculate(Vector2.up, new[] { Vector2.zero }, weights);
            Assert.That(weights[0], Is.EqualTo(1f));
            calculator.Calculate(Vector2.up, samples, weights);
            AssertWeights(weights, 0.75f, 0.25f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidInputIsRejectedBeforeChangingWeights(bool directional)
        {
            var calculator = CreateCalculator(directional);
            var weights = new[] { 0.2f, 0.8f };
            var samples = new[] { Vector2.zero, Vector2.up };
            Assert.Throws<ArgumentException>(() => calculator.Calculate(Vector2.zero, null, weights));
            Assert.Throws<ArgumentException>(() => calculator.Calculate(Vector2.zero, samples, null));
            Assert.Throws<ArgumentException>(() => calculator.Calculate(Vector2.zero, samples, new float[1]));
            Assert.Throws<ArgumentException>(() => calculator.Calculate(Vector2.zero, Array.Empty<Vector2>(), weights));
            Assert.Throws<ArgumentException>(() => calculator.Calculate(Vector2.zero, new[] { Vector2.zero, Vector2.zero }, weights));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                calculator.Calculate(new Vector2(float.NaN, 0f), samples, weights));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                calculator.Calculate(Vector2.zero, new[] { Vector2.zero, new Vector2(float.PositiveInfinity, 0f) }, weights));
            AssertWeights(weights, 0.2f, 0.8f);
            calculator.Calculate(Vector2.up * 0.5f, samples, weights);
            AssertWeights(weights, 0.5f, 0.5f);
            samples[1] = Vector2.zero;
            Assert.Throws<ArgumentException>(() => calculator.Calculate(Vector2.zero, samples, weights));
            AssertWeights(weights, 0.5f, 0.5f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExtremeFiniteSamplesRemainNormalized(bool directional)
        {
            var calculator = CreateCalculator(directional);
            var weights = new float[3];
            var samples = new[] { Vector2.zero, new Vector2(float.MaxValue, 0f), new Vector2(0f, float.MaxValue) };
            calculator.Calculate(new Vector2(float.MaxValue, float.MaxValue), samples, weights);
            AssertNormalized(weights);
            calculator.Calculate(new Vector2(-float.MaxValue, -float.MaxValue), samples, weights);
            AssertNormalized(weights);
            calculator.Calculate(Vector2.zero, new[] { new Vector2(float.Epsilon, 0f), new Vector2(2f * float.Epsilon, 0f) }, weights);
            Assert.That(weights[0], Is.EqualTo(1f));
            Assert.That(weights[1], Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TinyContributionsAreNotCutOff(bool directional)
        {
            var weights = new float[2];
            CreateCalculator(directional).Calculate(Vector2.up * 0.001f,
                new[] { Vector2.zero, Vector2.up }, weights);
            AssertWeights(weights, 0.999f, 0.001f);
        }

        private static IBlendWeightCalculator2D CreateCalculator(bool directional) =>
            directional ? (IBlendWeightCalculator2D)new DirectionalBlendWeightCalculator2D()
                : new CartesianBlendWeightCalculator2D();

        private static void AssertWeights(float[] actual, params float[] expected)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < actual.Length; i++)
                Assert.That(actual[i], Is.EqualTo(expected[i]).Within(0.00001f), $"样本 {i}");
            AssertNormalized(actual);
        }

        private static void AssertNormalized(float[] weights)
        {
            var total = 0f;
            foreach (var weight in weights)
            {
                Assert.That(float.IsNaN(weight) || float.IsInfinity(weight), Is.False);
                Assert.That(weight, Is.InRange(0f, 1f));
                total += weight;
            }
            Assert.That(total, Is.EqualTo(1f).Within(0.00001f));
        }
    }
}
