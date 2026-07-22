using System;
using MechStorm.Battle.Data;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Rules;
using MechStorm.Battle.Spatial;
using MechStorm.Battle.Units;
using NUnit.Framework;

namespace MechStorm.Battle.Tests.Units
{
    public class CombatUnitTests
    {
        [Test]
        public void PilotData_StoresStaticValues()
        {
            var pilot = new PilotData(1, "Test Pilot", 10);

            Assert.AreEqual(1, pilot.Id);
            Assert.AreEqual("Test Pilot", pilot.Name);
            Assert.AreEqual(10, pilot.MaxActionPoint);
        }

        [Test]
        public void PilotRuntime_StoresCurrentActionPoint()
        {
            var pilotRuntime = new PilotRuntime(6);

            Assert.AreEqual(6, pilotRuntime.CurrentActionPoint);
        }

        [Test]
        public void PilotData_WithNegativeMaxActionPoint_Throws()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PilotData(1, "Test Pilot", -1));

            Assert.AreEqual("maxActionPoint", exception.ParamName);
        }

        [Test]
        public void PilotRuntime_WithNegativeCurrentActionPoint_Throws()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PilotRuntime(-1));

            Assert.AreEqual("currentActionPoint", exception.ParamName);
        }

        [TestCase(10, 0, true)]
        [TestCase(10, 10, true)]
        [TestCase(10, 11, false)]
        public void HasEnoughActionPoint_ReturnsWhetherCurrentValueCanPay(
            int currentActionPoint, int amount, bool expected)
        {
            var pilotRuntime = new PilotRuntime(currentActionPoint);

            Assert.AreEqual(expected, pilotRuntime.HasEnoughActionPoint(amount));
        }

        [Test]
        public void TrySpendActionPoint_WithEnoughValue_DeductsAmount()
        {
            var pilotRuntime = new PilotRuntime(10);

            var spent = pilotRuntime.TrySpendActionPoint(4);

            Assert.IsTrue(spent);
            Assert.AreEqual(6, pilotRuntime.CurrentActionPoint);
        }

        [Test]
        public void TrySpendActionPoint_WithExactValue_DeductsToZero()
        {
            var pilotRuntime = new PilotRuntime(4);

            var spent = pilotRuntime.TrySpendActionPoint(4);

            Assert.IsTrue(spent);
            Assert.AreEqual(0, pilotRuntime.CurrentActionPoint);
        }

        [Test]
        public void TrySpendActionPoint_WithInsufficientValue_ReturnsFalseWithoutChange()
        {
            var pilotRuntime = new PilotRuntime(3);

            var spent = pilotRuntime.TrySpendActionPoint(4);

            Assert.IsFalse(spent);
            Assert.AreEqual(3, pilotRuntime.CurrentActionPoint);
        }

        [Test]
        public void ResetActionPointToMax_ReplacesCurrentValue()
        {
            var pilotRuntime = new PilotRuntime(2);

            pilotRuntime.ResetActionPointToMax(10);

            Assert.AreEqual(10, pilotRuntime.CurrentActionPoint);
        }

        [Test]
        public void ActionPointMethods_WithNegativeAmount_Throw()
        {
            var pilotRuntime = new PilotRuntime(10);

            var enoughException = Assert.Throws<ArgumentOutOfRangeException>(() =>
                pilotRuntime.HasEnoughActionPoint(-1));
            var spendException = Assert.Throws<ArgumentOutOfRangeException>(() =>
                pilotRuntime.TrySpendActionPoint(-1));
            var resetException = Assert.Throws<ArgumentOutOfRangeException>(() =>
                pilotRuntime.ResetActionPointToMax(-1));

            Assert.AreEqual("amount", enoughException.ParamName);
            Assert.AreEqual("amount", spendException.ParamName);
            Assert.AreEqual("maxActionPoint", resetException.ParamName);
            Assert.AreEqual(10, pilotRuntime.CurrentActionPoint);
        }

        [Test]
        public void MechData_StoresStaticValues()
        {
            var basicAttack = new BasicAttackData(25, 1, 3);
            var mech = new MechData(101, "Test Mech", basicAttack, 120, 5);

            Assert.AreEqual(101, mech.Id);
            Assert.AreEqual("Test Mech", mech.Name);
            Assert.AreSame(basicAttack, mech.BasicAttack);
            Assert.AreEqual(25, mech.BasicAttack.Damage);
            Assert.AreEqual(1, mech.BasicAttack.MinRange);
            Assert.AreEqual(3, mech.BasicAttack.MaxRange);
            Assert.AreEqual(120, mech.MaxDurability);
            Assert.AreEqual(5, mech.MoveRange);
        }

        [Test]
        public void BasicAttackData_WithInvalidValues_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BasicAttackData(-1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BasicAttackData(10, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BasicAttackData(10, 3, 2));
        }

        [Test]
        public void MechData_WithNullBasicAttack_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MechData(101, "Test Mech", null, 120, 5));
        }

        [Test]
        public void MechRuntime_StoresCurrentDurability()
        {
            var mechRuntime = new MechRuntime(80);

            Assert.AreEqual(80, mechRuntime.CurrentDurability);
            Assert.IsFalse(mechRuntime.IsDestroyed);
        }

        [Test]
        public void TakeDamage_ReducesCurrentDurability()
        {
            var mechRuntime = new MechRuntime(80);

            mechRuntime.TakeDamage(25);

            Assert.AreEqual(55, mechRuntime.CurrentDurability);
            Assert.IsFalse(mechRuntime.IsDestroyed);
        }

        [Test]
        public void TakeDamage_ClampsCurrentDurabilityToZero()
        {
            var mechRuntime = new MechRuntime(30);

            mechRuntime.TakeDamage(50);

            Assert.AreEqual(0, mechRuntime.CurrentDurability);
            Assert.IsTrue(mechRuntime.IsDestroyed);
        }

        [Test]
        public void TakeDamage_WithZeroDamage_DoesNotChangeCurrentDurability()
        {
            var mechRuntime = new MechRuntime(30);

            mechRuntime.TakeDamage(0);

            Assert.AreEqual(30, mechRuntime.CurrentDurability);
            Assert.IsFalse(mechRuntime.IsDestroyed);
        }

        [Test]
        public void TakeDamage_WithNegativeDamage_Throws()
        {
            var mechRuntime = new MechRuntime(30);

            Assert.Throws<ArgumentOutOfRangeException>(() => mechRuntime.TakeDamage(-1));
        }

        [Test]
        public void CombatUnit_ComposesDataRuntimeAndPosition()
        {
            var pilot = new PilotData(1, "Test Pilot", 10);
            var pilotRuntime = new PilotRuntime(6);
            var mech = new MechData(101, "Test Mech", new BasicAttackData(25, 1, 1), 120, 5);
            var mechRuntime = new MechRuntime(80);
            var position = new Vector2Int(2, 3);

            var unit = new CombatUnit(7, pilot, pilotRuntime, mech, mechRuntime, position);

            Assert.AreEqual(7, unit.UnitId);
            Assert.AreSame(pilot, unit.Pilot);
            Assert.AreSame(pilotRuntime, unit.PilotRuntime);
            Assert.AreSame(mech, unit.Mech);
            Assert.AreSame(mechRuntime, unit.MechRuntime);
            Assert.AreEqual(position, unit.Position);
        }

        [Test]
        public void CombatUnitFactory_CreatesUnitWithProvidedDataAndPosition()
        {
            var factory = new CombatUnitFactory();
            var pilot = new PilotData(1, "Test Pilot", 10);
            var mech = new MechData(101, "Test Mech", new BasicAttackData(25, 1, 1), 120, 5);
            var position = new Vector2Int(2, 3);

            var unit = factory.Create(7, pilot, mech, position);

            Assert.AreEqual(7, unit.UnitId);
            Assert.AreSame(pilot, unit.Pilot);
            Assert.AreSame(mech, unit.Mech);
            Assert.AreEqual(position, unit.Position);
        }

        [Test]
        public void CombatUnitFactory_InitializesRuntimeFromData()
        {
            var factory = new CombatUnitFactory();
            var pilot = new PilotData(1, "Test Pilot", 10);
            var mech = new MechData(101, "Test Mech", new BasicAttackData(25, 1, 1), 120, 5);

            var unit = factory.Create(7, pilot, mech, new Vector2Int(2, 3));

            Assert.AreEqual(10, unit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(120, unit.MechRuntime.CurrentDurability);
        }

        [Test]
        public void CombatUnitFactory_CreatesIndependentRuntimeForEachUnit()
        {
            var factory = new CombatUnitFactory();
            var pilot = new PilotData(1, "Test Pilot", 10);
            var mech = new MechData(101, "Test Mech", new BasicAttackData(25, 1, 1), 120, 5);

            var firstUnit = factory.Create(1, pilot, mech, new Vector2Int(2, 3));
            var secondUnit = factory.Create(2, pilot, mech, new Vector2Int(3, 3));

            Assert.AreNotSame(firstUnit.PilotRuntime, secondUnit.PilotRuntime);
            Assert.AreNotSame(firstUnit.MechRuntime, secondUnit.MechRuntime);

            firstUnit.MechRuntime.TakeDamage(25);

            Assert.AreEqual(95, firstUnit.MechRuntime.CurrentDurability);
            Assert.AreEqual(120, secondUnit.MechRuntime.CurrentDurability);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void CombatUnit_WithNonPositiveUnitId_Throws(int unitId)
        {
            var pilot = new PilotData(1, "Test Pilot", 10);
            var pilotRuntime = new PilotRuntime(6);
            var mech = new MechData(101, "Test Mech", new BasicAttackData(25, 1, 1), 120, 5);
            var mechRuntime = new MechRuntime(80);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CombatUnit(unitId, pilot, pilotRuntime, mech, mechRuntime, new Vector2Int(2, 3)));
        }

        [Test]
        public void MoveTo_UpdatesPosition()
        {
            var pilot = new PilotData(1, "Test Pilot", 10);
            var pilotRuntime = new PilotRuntime(6);
            var mech = new MechData(101, "Test Mech", new BasicAttackData(25, 1, 1), 120, 5);
            var mechRuntime = new MechRuntime(80);
            var unit = new CombatUnit(1, pilot, pilotRuntime, mech, mechRuntime, new Vector2Int(2, 3));

            unit.MoveTo(new Vector2Int(4, 5));

            Assert.AreEqual(new Vector2Int(4, 5), unit.Position);
        }

        [Test]
        public void TryAttack_ReducesTargetDurabilityWhenTargetIsInRange()
        {
            var grid = new SquareGrid(5, 5);
            var resolver = new AttackResolver(grid);
            var attacker = CreateCombatUnit(1, 25, 100, new Vector2Int(1, 1));
            var target = CreateCombatUnit(2, 10, 80, new Vector2Int(2, 1));

            var attacked = resolver.TryAttack(attacker, target);

            Assert.IsTrue(attacked);
            Assert.AreEqual(55, target.MechRuntime.CurrentDurability);
            Assert.IsFalse(target.MechRuntime.IsDestroyed);
        }

        [Test]
        public void TryAttack_ClampsTargetDurabilityToZero()
        {
            var grid = new SquareGrid(5, 5);
            var resolver = new AttackResolver(grid);
            var attacker = CreateCombatUnit(1, 50, 100, new Vector2Int(1, 1));
            var target = CreateCombatUnit(2, 10, 30, new Vector2Int(2, 1));

            var attacked = resolver.TryAttack(attacker, target);

            Assert.IsTrue(attacked);
            Assert.AreEqual(0, target.MechRuntime.CurrentDurability);
            Assert.IsTrue(target.MechRuntime.IsDestroyed);
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, false)]
        public void TryAttack_UsesConfiguredMinimumAndMaximumRange(int distance, bool expectedSuccess)
        {
            var grid = new SquareGrid(5, 1);
            var resolver = new AttackResolver(grid);
            var attacker = CreateCombatUnit(1, 25, 100, new Vector2Int(0, 0), minRange: 2, maxRange: 3);
            var target = CreateCombatUnit(2, 10, 80, new Vector2Int(distance, 0));

            var attacked = resolver.TryAttack(attacker, target);

            Assert.AreEqual(expectedSuccess, attacked);
            Assert.AreEqual(expectedSuccess ? 55 : 80, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void Constructor_WithNullGrid_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AttackResolver(null));
        }

        [Test]
        public void TryAttack_WithNullUnit_Throws()
        {
            var resolver = new AttackResolver(new SquareGrid(5, 5));
            var unit = CreateCombatUnit(1, 25, 100, new Vector2Int(1, 1));

            Assert.Throws<ArgumentNullException>(() => resolver.TryAttack(null, unit));
            Assert.Throws<ArgumentNullException>(() => resolver.TryAttack(unit, null));
        }

        [Test]
        public void IsPositionInRange_WithInvalidInput_ReturnsFalseOrThrows()
        {
            var resolver = new AttackResolver(new SquareGrid(5, 5));
            var unit = CreateCombatUnit(1, 25, 100, new Vector2Int(1, 1));

            Assert.IsFalse(resolver.IsPositionInRange(unit, new Vector2Int(-1, 1)));
            Assert.Throws<ArgumentNullException>(() =>
                resolver.IsPositionInRange(null, new Vector2Int(1, 1)));
        }

        private static CombatUnit CreateCombatUnit(int unitId, int attack, int currentDurability,
            Vector2Int position, int minRange = 1, int maxRange = 1)
        {
            var pilot = new PilotData(1, "Test Pilot", 10);
            var pilotRuntime = new PilotRuntime(6);
            var mech = new MechData(101, "Test Mech", new BasicAttackData(attack, minRange, maxRange), 120, 5);
            var mechRuntime = new MechRuntime(currentDurability);

            return new CombatUnit(unitId, pilot, pilotRuntime, mech, mechRuntime, position);
        }
    }
}
