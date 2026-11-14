using MechStorm.Battle.Foundation;
using NUnit.Framework;

namespace MechStorm.Battle.Tests.Foundation
{
    public class Vector2IntTests
    {
        [Test]
        public void Equals_ReturnsTrue_WhenCoordinatesAreSame()
        {
            var a = new Vector2Int(1, 2);
            var b = new Vector2Int(1, 2);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
        }

        [Test]
        public void Add_ReturnsSummedCoordinates()
        {
            var a = new Vector2Int(1, 2);
            var b = new Vector2Int(3, 4);

            var result = a + b;

            Assert.AreEqual(new Vector2Int(4, 6), result);
        }

        [Test]
        public void Subtract_ReturnsSubtractedCoordinates()
        {
            var a = new Vector2Int(5, 7);
            var b = new Vector2Int(2, 3);

            var result = a - b;

            Assert.AreEqual(new Vector2Int(3, 4), result);
        }
    }
}