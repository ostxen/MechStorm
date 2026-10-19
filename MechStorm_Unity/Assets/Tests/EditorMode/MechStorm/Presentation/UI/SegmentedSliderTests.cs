using NUnit.Framework;
using UnityEngine;
using MechStorm.Presentation.UI;

namespace MechStorm.Presentation.Tests.UI
{
    public sealed class SegmentedSliderTests
    {
        private GameObject _gameObject;
        private SegmentedSlider _slider;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("SegmentedSliderTest");
            _slider = _gameObject.AddComponent<SegmentedSlider>();
            _slider.minValue = 0f;
            _slider.maxValue = 5f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void WholeNumbers_RoundsValue()
        {
            _slider.wholeNumbers = true;
            _slider.value = 2.6f;

            Assert.AreEqual(3f, _slider.value);
        }

        [Test]
        public void SetValue_UpdatesNumericRangeAndSegmentCount()
        {
            _slider.wholeNumbers = true;
            _slider.SetValue(3, 7);

            Assert.AreEqual(0f, _slider.minValue);
            Assert.AreEqual(7f, _slider.maxValue);
            Assert.AreEqual(3f, _slider.value);
        }

        [Test]
        public void SetValue_ClampsToRange()
        {
            _slider.wholeNumbers = true;
            _slider.SetValue(99f);

            Assert.AreEqual(5f, _slider.value);
        }

        [Test]
        public void SetRange_ClampsCurrentValueToNewRange()
        {
            _slider.value = 5f;

            _slider.SetRange(1f, 3f);

            Assert.AreEqual(1f, _slider.minValue);
            Assert.AreEqual(3f, _slider.maxValue);
            Assert.AreEqual(3f, _slider.value);
        }

        [Test]
        public void SetHighlight_StoresCountDirectionAndAnchor()
        {
            _slider.SetHighlight(2,
                SegmentedSliderHighlightDirection.RightToLeft,
                SegmentedSliderHighlightAnchor.FilledEdge);

            Assert.AreEqual(2, _slider.highlightCount);
            Assert.AreEqual(
                SegmentedSliderHighlightDirection.RightToLeft,
                _slider.highlightDirection);
            Assert.AreEqual(
                SegmentedSliderHighlightAnchor.FilledEdge,
                _slider.highlightAnchor);
        }

        [Test]
        public void HighlightCount_IsClampedToMaxValue()
        {
            _slider.highlightCount = 99;

            Assert.AreEqual(5, _slider.highlightCount);
        }

        [Test]
        public void SetHighlight_UsesFilledEdgeAndRequestedCount()
        {
            _slider.SetHighlight(2);

            Assert.AreEqual(2, _slider.highlightCount);
            Assert.AreEqual(
                SegmentedSliderHighlightDirection.RightToLeft,
                _slider.highlightDirection);
            Assert.AreEqual(
                SegmentedSliderHighlightAnchor.FilledEdge,
                _slider.highlightAnchor);
        }

        [Test]
        public void HighlightMode_None_PreservesHighlightConfiguration()
        {
            _slider.SetHighlight(2);

            _slider.highlightMode = SegmentedSliderHighlightMode.None;

            Assert.AreEqual(2, _slider.highlightCount);

            _slider.highlightMode = SegmentedSliderHighlightMode.Contiguous;

            Assert.AreEqual(2, _slider.highlightCount);
        }

        [Test]
        public void ClearHighlight_DisablesHighlight()
        {
            _slider.SetHighlight(2);

            _slider.ClearHighlight();

            Assert.AreEqual(0, _slider.highlightCount);
        }

        [Test]
        public void SetHighlight_WithValueAndMaxValue_SetsValueAndCount()
        {
            _slider.SetValue(6, 10);
            _slider.SetHighlight(2);

            Assert.AreEqual(6f, _slider.value);
            Assert.AreEqual(10f, _slider.maxValue);
            Assert.AreEqual(2, _slider.highlightCount);
        }
    }
}
