using System;
using MechStorm.Presentation.Cameras;
using NUnit.Framework;
using UnityEngine;

namespace MechStorm.Presentation.Tests.Cameras
{
    public class BattleCameraSettingsTests
    {
        [Test]
        public void Defaults_PreserveExistingCameraTuningAndSerialize()
        {
            var settings = JsonUtility.FromJson<BattleCameraSettings>(JsonUtility.ToJson(new BattleCameraSettings()));
            Assert.DoesNotThrow(settings.Validate);
            Assert.AreEqual(0f, settings.InitialAzimuth);
            Assert.AreEqual(45f, settings.Elevation);
            Assert.AreEqual(10f, settings.InitialDistance);
            Assert.AreEqual(0.25f, settings.TransitionDuration);
            Assert.AreEqual(6f, settings.MinDistance);
            Assert.AreEqual(18f, settings.MaxDistance);
            Assert.AreEqual(1f, settings.ZoomStep);
            Assert.AreEqual(8f, settings.DragThreshold);
        }

        [TestCase(0f, 45f, 0f)]
        [TestCase(0f, 0f, 10f)]
        [TestCase(0f, 90f, 10f)]
        [TestCase(float.NaN, 45f, 10f)]
        [TestCase(0f, float.NaN, 10f)]
        [TestCase(0f, 45f, float.PositiveInfinity)]
        public void Validate_InvalidPoseThrows(float azimuth, float elevation, float distance)
        {
            var settings = new BattleCameraSettings(initialAzimuth: azimuth, elevation: elevation, initialDistance: distance);
            Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Validate_InvalidTransitionDurationThrows(float duration)
        {
            var settings = new BattleCameraSettings(transitionDuration: duration);
            Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
        }

        [TestCase(0f, 18f, 1f)]
        [TestCase(6f, 5f, 1f)]
        [TestCase(11f, 18f, 1f)]
        [TestCase(6f, 9f, 1f)]
        [TestCase(6f, 18f, 0f)]
        [TestCase(float.NaN, 18f, 1f)]
        [TestCase(6f, float.PositiveInfinity, 1f)]
        [TestCase(6f, 18f, float.NaN)]
        public void Validate_InvalidZoomThrows(float min, float max, float step)
        {
            var settings = new BattleCameraSettings(minDistance: min, maxDistance: max, zoomStep: step);
            Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Validate_InvalidDragThresholdThrows(float threshold)
        {
            var settings = new BattleCameraSettings(dragThreshold: threshold);
            Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
        }
    }
}
