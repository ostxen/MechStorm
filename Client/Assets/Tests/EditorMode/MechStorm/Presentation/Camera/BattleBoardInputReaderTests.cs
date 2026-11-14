using System.Collections.Generic;
using MechStorm.Battle.Units;
using MechStorm.Presentation.Board;
using MechStorm.Presentation.Input;
using NUnit.Framework;
using UnityEngine;

namespace MechStorm.Presentation.Tests.Input
{
    public class BattleBoardInputReaderTests
    {
        private Camera _camera;
        private GameObject _board;
        private BattleBoardInputReader _reader;
        private int _panCount;
        private readonly Vector2 _center = new Vector2(400f, 300f);

        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("InputTestCamera").AddComponent<Camera>();
            _camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
            _camera.transform.SetPositionAndRotation(new Vector3(0f, 10f, 0f), Quaternion.Euler(90f, 0f, 0f));
            _board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _board.transform.position = new Vector3(0f, -0.05f, 0f);
            _board.transform.localScale = new Vector3(20f, 0.1f, 20f);
            _panCount = 0;
            _reader = new BattleBoardInputReader(_camera, _board.GetComponent<Collider>(),
                new GridCoordinateConverter(1f, new Vector3(-10f, 0f, -10f)),
                new Dictionary<Collider, CombatUnit>(), Physics.DefaultRaycastLayers, 8f, (a, b) => _panCount++);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_camera.gameObject);
            UnityEngine.Object.DestroyImmediate(_board);
        }

        private bool Pointer(Vector2 position, bool down = false, bool held = false, bool up = false, bool blocked = false)
        {
            return _reader.ProcessPointer(position, down, held, up, blocked, out _, out _);
        }

        [TestCase(0f)]
        [TestCase(7f)]
        [TestCase(8f)]
        public void Click_OnlyCommitsOnReleaseWithinThreshold(float pixels)
        {
            Assert.IsFalse(Pointer(_center, down: true, held: true));
            Assert.IsFalse(Pointer(_center + Vector2.right * pixels, held: true));
            Assert.IsTrue(Pointer(_center + Vector2.right * pixels, up: true));
            Assert.AreEqual(0, _panCount);
            Assert.IsFalse(Pointer(_center, up: true));
        }

        [Test]
        public void Drag_ReleaseOrReturningToStartNeverCommitsClick()
        {
            Pointer(_center, down: true, held: true);
            Assert.IsFalse(Pointer(_center + Vector2.right * 9f, held: true));
            Assert.IsFalse(Pointer(_center, up: true));
            Assert.Greater(_panCount, 0);
            Pointer(_center, down: true, held: true);
            Assert.IsTrue(Pointer(_center, up: true));
        }

        [Test]
        public void Drag_ThresholdCrossedOnlyOnReleaseStillSuppressesClick()
        {
            Pointer(_center, down: true, held: true);
            Assert.IsFalse(Pointer(_center + Vector2.right * 20f, up: true));
            Assert.AreEqual(1, _panCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UI_CancelsPressAndDragWithoutClickAfterLeavingUI(bool dragging)
        {
            Pointer(_center, down: true, held: true);
            if (dragging) Pointer(_center + Vector2.right * 20f, held: true);
            int count = _panCount;
            Assert.IsFalse(Pointer(_center, held: true, blocked: true));
            Assert.IsFalse(Pointer(_center, up: true));
            Assert.AreEqual(count, _panCount);
            Pointer(_center, down: true, held: true, blocked: true);
            Assert.IsFalse(Pointer(_center, up: true));
        }

        [Test]
        public void CancelOrLostButtonOrViewportExitRequiresNewPress()
        {
            Pointer(_center, down: true, held: true);
            _reader.CancelGesture();
            Assert.IsFalse(Pointer(_center, up: true));
            Pointer(_center, down: true, held: true);
            Pointer(_center);
            Assert.IsFalse(Pointer(_center, up: true));
            Pointer(_center, down: true, held: true);
            Pointer(new Vector2(-1f, 300f), held: true);
            Assert.IsFalse(Pointer(_center, up: true));
            Assert.AreEqual(0, _panCount);
        }

    }
}
