using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using MechStorm.Presentation.Board;
using MechStorm.Presentation.Cameras;
using NUnit.Framework;
using UnityEngine;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Tests.Cameras
{
    public class BattleCameraControllerTests
    {
        private Camera _camera;
        private BattleCameraController _controller;
        private GridCoordinateConverter _coordinates;

        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("CameraTest").AddComponent<Camera>();
            _coordinates = new GridCoordinateConverter(2f, new Vector3(-5f, 2f, -5f));
            _controller = new BattleCameraController(_camera, _coordinates,
                new BattleCameraSettings(initialAzimuth: 15f), new Rect(-5f, -5f, 30f, 30f));
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
            UnityEngine.Object.DestroyImmediate(_camera.gameObject);
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void Rotate_EightDirectionsReturnToInitialPose(int direction)
        {
            _controller.SynchronizeActor(CreateUnit(1, new Vector2Int(0, 0)));
            _controller.Tick(0.25f);
            var position = _camera.transform.position;
            var rotation = _camera.transform.rotation;
            for (int i = 1; i <= 8; i++)
            {
                _controller.Rotate(direction);
                _controller.Tick(0.25f);
                Assert.AreEqual((i * direction + 8) % 8, _controller.DirectionIndex);
                Assert.That(Vector3.Distance(_camera.transform.position, _controller.Focus), Is.EqualTo(10f).Within(0.001f));
                Assert.That(_camera.transform.eulerAngles.x, Is.EqualTo(45f).Within(0.001f));
                Assert.That(Quaternion.Angle(_camera.transform.rotation,
                    Quaternion.Euler(45f, 15f + i * direction * 45f, 0f)), Is.LessThan(0.05f));
            }
            Assert.That(Vector3.Distance(position, _camera.transform.position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(rotation, _camera.transform.rotation), Is.LessThan(0.05f));
        }

        [TestCase(0, 0)]
        [TestCase(14, 0)]
        [TestCase(0, 14)]
        [TestCase(14, 14)]
        public void SynchronizeActor_BoardCornersAreCenteredAtEveryDirection(int x, int y)
        {
            var actor = CreateUnit(1, new Vector2Int(x, y));
            _controller.SynchronizeActor(actor);
            _controller.Tick(0.25f);
            for (int i = 0; i < 8; i++)
            {
                var viewport = _camera.WorldToViewportPoint(_coordinates.GridToWorld(actor.Position));
                Assert.That(viewport.x, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(viewport.y, Is.EqualTo(0.5f).Within(0.001f));
                Assert.Greater(viewport.z, 0f);
                _controller.Rotate(1);
                _controller.Tick(0.25f);
            }
        }

        [Test]
        public void SynchronizeActor_ChangedActorPreservesDirectionAndDistance()
        {
            _controller.SynchronizeActor(CreateUnit(1, new Vector2Int(1, 1)));
            _controller.Rotate(-1);
            var actor = CreateUnit(2, new Vector2Int(4, 3));
            _controller.SynchronizeActor(actor);
            Assert.AreEqual(7, _controller.DirectionIndex);
            _controller.Tick(0.25f);
            Assert.AreEqual(_coordinates.GridToWorld(actor.Position), _controller.Focus);
            Assert.That(Vector3.Distance(_camera.transform.position, _controller.Focus), Is.EqualTo(10f).Within(0.001f));
            Assert.AreEqual(new Vector2Int(4, 3), actor.Position);
            Assert.AreEqual(100, actor.MechRuntime.CurrentDurability);
            Assert.AreEqual(3, actor.PilotRuntime.CurrentActionPoint);
        }

        [Test]
        public void SynchronizeActor_UnchangedActorDoesNotPullCameraBackEveryFrame()
        {
            var actor = CreateUnit(1, new Vector2Int(1, 1));
            _controller.SynchronizeActor(actor);
            _controller.Tick(0.25f);
            _camera.transform.position += Vector3.right * 3f;
            var position = _camera.transform.position;
            _controller.SynchronizeActor(actor);
            Assert.AreEqual(position, _camera.transform.position);
        }

        [Test]
        public void SynchronizeActor_MovementAndRollbackRecenterWithoutResettingDirection()
        {
            var actor = CreateUnit(1, new Vector2Int(1, 1));
            var enemy = CreateUnit(2, new Vector2Int(5, 5));
            var session = new MechStorm.Battle.BattleSession(6, 6, new[] { actor }, new[] { enemy });
            _controller.SynchronizeActor(actor);
            _controller.Rotate(1);
            Assert.IsTrue(session.TryMoveCurrentCombatUnit(new Vector2Int(2, 1)).IsSuccess);
            _controller.SynchronizeActor(actor);
            Assert.AreEqual(_coordinates.GridToWorld(new Vector2Int(2, 1)), _controller.Focus);
            Assert.IsTrue(session.TryMoveCurrentCombatUnit(new Vector2Int(1, 1)).IsSuccess);
            _controller.SynchronizeActor(actor);
            Assert.AreEqual(_coordinates.GridToWorld(new Vector2Int(1, 1)), _controller.Focus);
            Assert.AreEqual(1, _controller.DirectionIndex);
        }

        [Test]
        public void Constructor_ConfiguredElevationAndDistanceRemainFixedWhenRotating()
        {
            using var controller = new BattleCameraController(_camera, _coordinates,
                new BattleCameraSettings(initialAzimuth: 30f, elevation: 60f, initialDistance: 20f, maxDistance: 25f),
                new Rect(-5f, -5f, 30f, 30f));
            controller.SynchronizeActor(CreateUnit(1, new Vector2Int(2, 2)));
            controller.Rotate(1);
            controller.Tick(0.25f);
            Assert.That(_camera.transform.eulerAngles.x, Is.EqualTo(60f).Within(0.001f));
            Assert.That(_camera.transform.eulerAngles.y, Is.EqualTo(75f).Within(0.001f));
            Assert.That(Vector3.Distance(controller.Focus, _camera.transform.position), Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void Dispose_RestoresOriginalCameraPoseAndIsIdempotent()
        {
            _controller.SynchronizeActor(CreateUnit(1, new Vector2Int(4, 4)));
            _controller.Rotate(1);
            _controller.Dispose();
            _controller.Dispose();
            Assert.AreEqual(Vector3.zero, _camera.transform.position);
            Assert.That(Quaternion.Angle(Quaternion.identity, _camera.transform.rotation), Is.LessThan(0.05f));
        }

        [TestCase(1f, 9f)]
        [TestCase(-1f, 11f)]
        [TestCase(100f, 6f)]
        [TestCase(-100f, 18f)]
        [TestCase(0.5f, 9.5f)]
        public void Zoom_ClampsAndEasesWithoutChangingFocusOrElevation(float scroll, float expected)
        {
            _controller.SynchronizeActor(CreateUnit(1, new Vector2Int(1, 1)));
            _controller.Tick(0.25f);
            var position = _camera.transform.position;
            var rotation = _camera.transform.rotation;
            var focus = _controller.Focus;
            _controller.Zoom(scroll);
            Assert.AreEqual(expected, _controller.Distance);
            Assert.AreEqual(position, _camera.transform.position);
            _controller.Tick(0.125f);
            Assert.That(Vector3.Distance(focus, _camera.transform.position), Is.EqualTo((10f + expected) * 0.5f).Within(0.001f));
            _controller.Tick(0.125f);
            Assert.That(Vector3.Distance(focus, _camera.transform.position), Is.EqualTo(expected).Within(0.001f));
            Assert.AreEqual(focus, _controller.Focus);
            Assert.That(Quaternion.Angle(rotation, _camera.transform.rotation), Is.LessThan(0.05f));
        }

        [Test]
        public void Zoom_ContinuousInputPreservesCurrentPoseAndRotationAndActorChangesPreserveZoom()
        {
            _controller.SynchronizeActor(CreateUnit(1, new Vector2Int(1, 1)));
            _controller.Tick(0.25f);
            _controller.Zoom(1f);
            _controller.Tick(0.125f);
            var position = _camera.transform.position;
            _controller.Zoom(1f);
            Assert.AreEqual(position, _camera.transform.position);
            _controller.Rotate(1);
            _controller.SynchronizeActor(CreateUnit(2, new Vector2Int(3, 4)));
            _controller.Tick(0.25f);
            Assert.AreEqual(8f, _controller.Distance);
            Assert.That(Vector3.Distance(_controller.Focus, _camera.transform.position), Is.EqualTo(8f).Within(0.001f));
            Assert.That(Quaternion.Angle(_camera.transform.rotation, Quaternion.Euler(45f, 60f, 0f)), Is.LessThan(0.05f));
        }

        [Test]
        public void Zoom_AtLimitOrZeroDoesNotRestartExistingTransition()
        {
            _controller.SynchronizeActor(CreateUnit(1, new Vector2Int(1, 1)));
            _controller.Tick(0.25f);
            _controller.Zoom(100f);
            _controller.Tick(0.125f);
            _controller.Zoom(100f);
            _controller.Zoom(0f);
            _controller.Tick(0.125f);
            Assert.That(Vector3.Distance(_controller.Focus, _camera.transform.position), Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void Rotate_EasesAlongOrbitAndFinishesAtExactDirection()
        {
            _controller.SynchronizeActor(CreateUnit(1, new Vector2Int(1, 1)));
            _controller.Tick(0.25f);
            var startPosition = _camera.transform.position;
            var startRotation = _camera.transform.rotation;
            _controller.Rotate(1);
            Assert.AreEqual(startPosition, _camera.transform.position);
            _controller.Tick(0.0625f);
            Assert.That(Quaternion.Angle(startRotation, _camera.transform.rotation), Is.EqualTo(45f * 0.15625f).Within(0.05f));
            Assert.That(Vector3.Distance(_controller.Focus, _camera.transform.position), Is.EqualTo(10f).Within(0.001f));
            _controller.Tick(1f);
            Assert.That(Quaternion.Angle(_camera.transform.rotation, Quaternion.Euler(45f, 60f, 0f)), Is.LessThan(0.05f));
        }

        [Test]
        public void SynchronizeActor_ChangedActorEasesFromCurrentPose()
        {
            _controller.SynchronizeActor(CreateUnit(1, new Vector2Int(1, 1)));
            _controller.Tick(0.25f);
            var startPosition = _camera.transform.position;
            _controller.SynchronizeActor(CreateUnit(2, new Vector2Int(5, 5)));
            Assert.AreEqual(startPosition, _camera.transform.position);
            _controller.Tick(0.125f);
            var targetRotation = Quaternion.Euler(45f, 15f, 0f);
            var targetPosition = _controller.Focus - targetRotation * Vector3.forward * 10f;
            Assert.That(Vector3.Distance(_camera.transform.position, (startPosition + targetPosition) * 0.5f), Is.LessThan(0.001f));
            _controller.Tick(0.125f);
            Assert.That(Vector3.Distance(_camera.transform.position, targetPosition), Is.LessThan(0.001f));
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void Rotate_RepeatedInputContinuesWithoutJumpAndWrapsInRequestedDirection(int direction)
        {
            _controller.SynchronizeActor(CreateUnit(1, new Vector2Int(1, 1)));
            _controller.Tick(0.25f);
            _controller.Rotate(direction);
            _controller.Tick(0.125f);
            var position = _camera.transform.position;
            var rotation = _camera.transform.rotation;
            _controller.Rotate(direction);
            Assert.AreEqual(position, _camera.transform.position);
            Assert.AreEqual(rotation, _camera.transform.rotation);
            _controller.Tick(0.25f);
            Assert.That(Quaternion.Angle(_camera.transform.rotation,
                Quaternion.Euler(45f, 15f + direction * 90f, 0f)), Is.LessThan(0.05f));
        }

        [TestCase(6f)]
        [TestCase(10f)]
        [TestCase(18f)]
        public void Pan_EveryDirectionFollowsPointerAndPreservesOrbit(float distance)
        {
            _camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
            var actor = CreateUnit(1, new Vector2Int(7, 7));
            _controller.SynchronizeActor(actor);
            _controller.Zoom(10f - distance);
            _controller.Tick(0.25f);
            for (int i = 0; i < 8; i++)
            {
                var plane = new Plane(Vector3.up, _coordinates.Origin);
                var start = new Vector2(400f, 300f);
                var end = start + new Vector2(24f, 15f);
                var ray = _camera.ScreenPointToRay(start);
                Assert.IsTrue(plane.Raycast(ray, out float enter));
                var grabbed = ray.GetPoint(enter);
                var rotation = _camera.transform.rotation;
                _controller.Pan(start, end);
                var screen = _camera.WorldToScreenPoint(grabbed);
                Assert.That(Vector2.Distance(screen, end), Is.LessThan(0.01f));
                Assert.That(Vector3.Distance(_controller.Focus, _camera.transform.position), Is.EqualTo(distance).Within(0.001f));
                Assert.That(Quaternion.Angle(rotation, _camera.transform.rotation), Is.LessThan(0.05f));
                var focus = _controller.Focus;
                _controller.SynchronizeActor(actor);
                _controller.Tick(0.25f);
                Assert.AreEqual(focus, _controller.Focus);
                _controller.Rotate(1);
                _controller.Tick(0.25f);
            }
            _controller.SynchronizeActor(CreateUnit(2, new Vector2Int(14, 14)));
            _controller.Tick(0.25f);
            Assert.AreEqual(_coordinates.GridToWorld(new Vector2Int(14, 14)), _controller.Focus);
            Assert.AreEqual(distance, _controller.Distance);
        }

        [TestCase(-100000f, -100000f)]
        [TestCase(-100000f, 100000f)]
        [TestCase(100000f, -100000f)]
        [TestCase(100000f, 100000f)]
        public void Pan_ClampsFocusToBoardAndStopsWithoutInertia(float x, float z)
        {
            _camera.orthographic = true;
            _controller.SynchronizeActor(CreateUnit(1, new Vector2Int(7, 7)));
            _controller.Tick(0.25f);
            // 极远边界点使用俯视投影，避免测试射线起点已越过平面而朝反方向求交。
            _camera.transform.SetPositionAndRotation(_controller.Focus + Vector3.up * 10f, Quaternion.Euler(90f, 0f, 0f));
            var start = _camera.WorldToScreenPoint(_controller.Focus);
            var end = _camera.WorldToScreenPoint(new Vector3(x, 2f, z));
            _controller.Pan(start, end);
            Assert.That(_controller.Focus.x, Is.InRange(-5f, 25f));
            Assert.That(_controller.Focus.z, Is.InRange(-5f, 25f));
            Assert.That(_controller.Focus.x, Is.EqualTo(x > 0f ? -5f : 25f).Within(0.01f));
            Assert.That(_controller.Focus.z, Is.EqualTo(z > 0f ? -5f : 25f).Within(0.01f));
            var position = _camera.transform.position;
            _controller.Tick(1f);
            Assert.AreEqual(position, _camera.transform.position);
        }

        [Test]
        public void Pan_DuringTransitionKeepsFocusWhileRotationAndZoomFinish()
        {
            _controller.SynchronizeActor(CreateUnit(1, new Vector2Int(7, 7)));
            _controller.Tick(0.25f);
            _controller.Rotate(1);
            _controller.Zoom(2f);
            _controller.Tick(0.125f);
            var center = (Vector2)_camera.WorldToScreenPoint(_controller.Focus);
            _controller.Pan(center, center + Vector2.right * 15f);
            var focus = _controller.Focus;
            _controller.Tick(0.125f);
            Assert.AreEqual(focus, _controller.Focus);
            Assert.That(Vector3.Distance(focus, _camera.transform.position), Is.EqualTo(8f).Within(0.001f));
            Assert.That(Quaternion.Angle(_camera.transform.rotation, Quaternion.Euler(45f, 60f, 0f)), Is.LessThan(0.05f));
        }

        [Test]
        public void CameraOperations_DoNotWriteRuntimeStateIntoSettings()
        {
            var settings = new BattleCameraSettings();
            string before = JsonUtility.ToJson(settings);
            using var controller = new BattleCameraController(_camera, _coordinates, settings, new Rect(-5f, -5f, 30f, 30f));
            controller.SynchronizeActor(CreateUnit(1, new Vector2Int(7, 7)));
            controller.Rotate(1);
            controller.Zoom(2f);
            controller.Tick(0.25f);
            var center = (Vector2)_camera.WorldToScreenPoint(controller.Focus);
            controller.Pan(center, center + Vector2.right * 15f);
            controller.Dispose();
            Assert.AreEqual(before, JsonUtility.ToJson(settings));
        }

        private static CombatUnit CreateUnit(int id, Vector2Int position)
        {
            return new CombatUnitFactory().Create(id, new PilotData(id, "Pilot", 3),
                new MechData(id, "Mech", new BasicAttackData(10, 1, 3, 1), 100, 3), position);
        }
    }
}
