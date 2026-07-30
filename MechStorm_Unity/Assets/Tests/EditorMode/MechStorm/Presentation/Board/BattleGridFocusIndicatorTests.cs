using System;
using MechStorm.Presentation.Board;
using NUnit.Framework;
using UnityEngine;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Tests.Board
{
    public class BattleGridFocusIndicatorTests
    {
        private Material _material;
        private BattleGridFocusIndicator _indicator;

        [SetUp]
        public void SetUp()
        {
            _material = new Material(Shader.Find("Sprites/Default"));
            _indicator = new BattleGridFocusIndicator(
                2f,
                new GridCoordinateConverter(2f, new Vector3(1f, 0f, 3f)),
                _material,
                Color.yellow);
        }

        [TearDown]
        public void TearDown()
        {
            _indicator?.Show(default);
            var root = GameObject.Find("BattleGridFocus");
            if (root != null)
            {
                var mesh = root.GetComponent<MeshFilter>().sharedMesh;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mesh);
            }

            UnityEngine.Object.DestroyImmediate(_material);
        }

        [Test]
        public void Show_PositionsAndDisplaysFourCornerMesh()
        {
            var position = new Vector2Int(2, 1);

            _indicator.Show(position);

            var root = GameObject.Find("BattleGridFocus");
            Assert.IsTrue(_indicator.IsVisible);
            Assert.AreEqual(position, _indicator.FocusedPosition);
            Assert.AreEqual(new Vector3(6f, 0.03f, 6f), root.transform.position);
            Assert.AreEqual(32, root.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            Assert.AreEqual(48, root.GetComponent<MeshFilter>().sharedMesh.triangles.Length);
            Assert.AreEqual(Physics.IgnoreRaycastLayer, root.layer);
            Assert.IsNull(root.GetComponent<Collider>());
        }

        [Test]
        public void Hide_ClearsPositionAndVisibility()
        {
            _indicator.Show(new Vector2Int(1, 1));

            _indicator.Hide();

            Assert.IsFalse(_indicator.IsVisible);
            Assert.IsNull(_indicator.FocusedPosition);
        }

        [Test]
        public void Constructor_WithInvalidArguments_Throws()
        {
            var converter = new GridCoordinateConverter();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BattleGridFocusIndicator(0f, converter, _material, Color.white));
            Assert.Throws<ArgumentNullException>(() =>
                new BattleGridFocusIndicator(1f, null, _material, Color.white));
            Assert.Throws<ArgumentNullException>(() =>
                new BattleGridFocusIndicator(1f, converter, null, Color.white));
        }
    }
}
