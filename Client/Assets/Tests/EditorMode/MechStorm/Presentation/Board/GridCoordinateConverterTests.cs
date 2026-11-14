using MechStorm.Presentation.Board;
using NUnit.Framework;
using UnityEngine;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Tests.Board
{
    public class GridCoordinateConverterTests
    {
        [Test]
        public void GridToWorld_WithDefaultSettings_ReturnsCellCenter()
        {
            var converter = new GridCoordinateConverter();

            var worldPosition = converter.GridToWorld(new Vector2Int(0, 0));

            Assert.AreEqual(new Vector3(0.5f, 0f, 0.5f), worldPosition);
        }

        [Test]
        public void GridToWorld_WithCustomOriginAndCellSize_ReturnsCellCenter()
        {
            var converter = new GridCoordinateConverter(2f, new Vector3(-5f, 1f, -3f));

            var worldPosition = converter.GridToWorld(new Vector2Int(2, 1));

            Assert.AreEqual(new Vector3(0f, 1f, 0f), worldPosition);
        }

        [Test]
        public void WorldToGrid_WithPointInsideCell_ReturnsContainingGridPosition()
        {
            var converter = new GridCoordinateConverter(2f, new Vector3(-5f, 1f, -3f));

            var gridPosition = converter.WorldToGrid(new Vector3(0.9f, 1f, 0.9f));

            Assert.AreEqual(new Vector2Int(2, 1), gridPosition);
        }

        [Test]
        public void WorldToGrid_WithNegativeWorldPosition_UsesFloor()
        {
            var converter = new GridCoordinateConverter(1f, Vector3.zero);

            var gridPosition = converter.WorldToGrid(new Vector3(-0.1f, 0f, -0.1f));

            Assert.AreEqual(new Vector2Int(-1, -1), gridPosition);
        }

        [Test]
        public void Constructor_WithZeroCellSize_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new GridCoordinateConverter(0f, Vector3.zero));
        }
    }
}
