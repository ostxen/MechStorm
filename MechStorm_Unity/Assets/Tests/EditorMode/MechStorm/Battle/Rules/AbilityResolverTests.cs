using System;
using MechStorm.Battle.Data;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Rules;
using MechStorm.Battle.Spatial;
using MechStorm.Battle.Units;
using NUnit.Framework;

namespace MechStorm.Battle.Tests.Rules
{
    public class AbilityResolverTests
    {
        [Test]
        public void Constructor_WithNullDependency_Throws()
        {
            var registry = CreateRegistry(
                CreateCombatUnit(1, 80, new Vector2Int(0, 0)),
                CreateCombatUnit(2, 80, new Vector2Int(1, 0)));

            Assert.Throws<ArgumentNullException>(() => new AbilityResolver(null, registry));
            Assert.Throws<ArgumentNullException>(() => new AbilityResolver(new SquareGrid(5, 5), null));
        }

        [Test]
        public void TryResolveAbility_WithValidEnemyTarget_AppliesFixedDamage()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(2, 0));
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));

            var resolved = resolver.TryResolveAbility(CreateAbility(25, 1, 3), source, target);

            Assert.IsTrue(resolved);
            Assert.AreEqual(3, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(55, target.MechRuntime.CurrentDurability);
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, false)]
        public void TryResolveAbility_UsesConfiguredRangeBoundaries(int distance, bool expectedSuccess)
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(distance, 0));
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));

            var resolved = resolver.TryResolveAbility(CreateAbility(25, 2, 3), source, target);

            Assert.AreEqual(expectedSuccess, resolved);
            Assert.AreEqual(expectedSuccess ? 3 : 6, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(expectedSuccess ? 55 : 80, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_UsesManhattanDistance()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(2, 1));
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));

            var resolved = resolver.TryResolveAbility(CreateAbility(25, 3, 3), source, target);

            Assert.IsTrue(resolved);
            Assert.AreEqual(55, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithSameFactionTarget_ReturnsFalseWithoutDamage()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(1, 0));
            var otherFactionUnit = CreateCombatUnit(3, 80, new Vector2Int(4, 4));
            var registry = new CombatUnitRegistry(new[] { source, target }, new[] { otherFactionUnit });
            var resolver = new AbilityResolver(new SquareGrid(5, 5), registry);

            var resolved = resolver.TryResolveAbility(CreateAbility(), source, target);

            Assert.IsFalse(resolved);
            Assert.AreEqual(6, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(80, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithNeutralTargetFromDifferentFaction_AppliesDamage()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var otherFactionUnit = CreateCombatUnit(2, 80, new Vector2Int(4, 4));
            var neutralTarget = CreateCombatUnit(3, 80, new Vector2Int(1, 0));
            var registry = new CombatUnitRegistry(
                new[] { source }, new[] { otherFactionUnit }, new[] { neutralTarget });
            var resolver = new AbilityResolver(new SquareGrid(5, 5), registry);

            var resolved = resolver.TryResolveAbility(CreateAbility(), source, neutralTarget);

            Assert.IsTrue(resolved);
            Assert.AreEqual(55, neutralTarget.MechRuntime.CurrentDurability);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TryResolveAbility_WithDeadUnit_ReturnsFalseWithoutDamage(bool sourceIsDead)
        {
            var source = CreateCombatUnit(1, sourceIsDead ? 0 : 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, sourceIsDead ? 80 : 0, new Vector2Int(1, 0));
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));

            var resolved = resolver.TryResolveAbility(CreateAbility(), source, target);

            Assert.IsFalse(resolved);
            Assert.AreEqual(6, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(sourceIsDead ? 80 : 0, target.MechRuntime.CurrentDurability);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TryResolveAbility_WithUnitOutsideGrid_ReturnsFalseWithoutDamage(bool sourceIsOutside)
        {
            var sourcePosition = sourceIsOutside ? new Vector2Int(-1, 0) : new Vector2Int(0, 0);
            var targetPosition = sourceIsOutside ? new Vector2Int(1, 0) : new Vector2Int(5, 0);
            var source = CreateCombatUnit(1, 80, sourcePosition);
            var target = CreateCombatUnit(2, 80, targetPosition);
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));

            var resolved = resolver.TryResolveAbility(CreateAbility(), source, target);

            Assert.IsFalse(resolved);
            Assert.AreEqual(6, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(80, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void CanUseAbility_WithNullArgument_Throws()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(1, 0));
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));
            var ability = CreateAbility();

            Assert.Throws<ArgumentNullException>(() => resolver.CanUseAbility(null, source, target));
            Assert.Throws<ArgumentNullException>(() => resolver.CanUseAbility(ability, null, target));
            Assert.Throws<ArgumentNullException>(() => resolver.CanUseAbility(ability, source, null));
        }

        [Test]
        public void CanUseAbility_WithUnregisteredUnit_Throws()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(1, 0));
            var unknownUnit = CreateCombatUnit(3, 80, new Vector2Int(2, 0));
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));
            var ability = CreateAbility();

            Assert.Throws<ArgumentException>(() => resolver.CanUseAbility(ability, unknownUnit, target));
            Assert.Throws<ArgumentException>(() => resolver.CanUseAbility(ability, source, unknownUnit));
        }

        [Test]
        public void CanUseAbility_DoesNotModifyCombatState()
        {
            var sourcePosition = new Vector2Int(0, 0);
            var targetPosition = new Vector2Int(1, 0);
            var source = CreateCombatUnit(1, 80, sourcePosition);
            var target = CreateCombatUnit(2, 80, targetPosition);
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));

            var canUse = resolver.CanUseAbility(CreateAbility(), source, target);

            Assert.IsTrue(canUse);
            Assert.AreEqual(80, source.MechRuntime.CurrentDurability);
            Assert.AreEqual(80, target.MechRuntime.CurrentDurability);
            Assert.AreEqual(6, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(6, target.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(sourcePosition, source.Position);
            Assert.AreEqual(targetPosition, target.Position);
        }

        [Test]
        public void GetPositionsInRange_ReturnsConfiguredRangeWithinGridWithoutSideEffects()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(4, 4));
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));

            var positions = resolver.GetPositionsInRange(
                CreateAbility(minRange: 2, maxRange: 3), source);

            Assert.AreEqual(7, positions.Count);
            CollectionAssert.Contains(positions, new Vector2Int(2, 0));
            CollectionAssert.Contains(positions, new Vector2Int(1, 2));
            CollectionAssert.DoesNotContain(positions, new Vector2Int(1, 0));
            CollectionAssert.DoesNotContain(positions, new Vector2Int(4, 0));
            Assert.AreEqual(6, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(80, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void IsPositionInRange_UsesClosedRangeAndGridBoundary()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(4, 4));
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));
            var ability = CreateAbility(minRange: 2, maxRange: 3);

            Assert.IsFalse(resolver.IsPositionInRange(ability, source, new Vector2Int(1, 0)));
            Assert.IsTrue(resolver.IsPositionInRange(ability, source, new Vector2Int(2, 0)));
            Assert.IsTrue(resolver.IsPositionInRange(ability, source, new Vector2Int(2, 1)));
            Assert.IsFalse(resolver.IsPositionInRange(ability, source, new Vector2Int(4, 0)));
            Assert.IsFalse(resolver.IsPositionInRange(ability, source, new Vector2Int(-1, 0)));
        }

        [Test]
        public void IsPositionInRange_UsesExplicitRangeSource()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(4, 4));
            var resolver = new AbilityResolver(
                new SquareGrid(5, 5),
                CreateRegistry(source, target));
            var ability = new AbilityDefinition(
                101, "Weapon Ability", 2, 3, 25, 3, TargetRule.EnemyUnit, true);

            Assert.IsFalse(
                resolver.IsPositionInRange(ability, source, new Vector2Int(1, 0)));
            Assert.IsTrue(
                resolver.IsPositionInRange(ability, source, new Vector2Int(2, 0)));
            Assert.IsTrue(resolver.IsPositionInRange(
                ability,
                source,
                new Vector2Int(1, 0),
                AbilityTargetRangeSource.BasicWeapon));
            Assert.IsFalse(resolver.IsPositionInRange(
                ability,
                source,
                new Vector2Int(2, 0),
                AbilityTargetRangeSource.BasicWeapon));
        }

        [Test]
        public void IsPositionInRange_BasicWeaponSourceRequiresWeaponDependency()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(4, 4));
            var resolver = new AbilityResolver(
                new SquareGrid(5, 5),
                CreateRegistry(source, target));

            Assert.Throws<ArgumentException>(() => resolver.IsPositionInRange(
                CreateAbility(),
                source,
                new Vector2Int(1, 0),
                AbilityTargetRangeSource.BasicWeapon));
        }

        [Test]
        public void TryResolveAbility_WithLethalDamage_ClampsDurabilityToZero()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 20, new Vector2Int(1, 0));
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));

            var resolved = resolver.TryResolveAbility(CreateAbility(30), source, target);

            Assert.IsTrue(resolved);
            Assert.AreEqual(0, target.MechRuntime.CurrentDurability);
            Assert.IsTrue(target.IsDead());
        }

        [Test]
        public void TryResolveAbility_WithZeroDamage_SucceedsWithoutChangingDurability()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(1, 0));
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));

            var resolved = resolver.TryResolveAbility(CreateAbility(0), source, target);

            Assert.IsTrue(resolved);
            Assert.AreEqual(80, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithExactActionPointCost_SpendsAllActionPoint()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(1, 0));
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));

            var resolved = resolver.TryResolveAbility(CreateAbility(actionPointCost: 6), source, target);

            Assert.IsTrue(resolved);
            Assert.AreEqual(0, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(55, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithInsufficientActionPoint_ReturnsFalseWithoutSideEffects()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(1, 0));
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));

            var resolved = resolver.TryResolveAbility(CreateAbility(actionPointCost: 7), source, target);

            Assert.IsFalse(resolved);
            Assert.AreEqual(6, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(80, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithZeroActionPointCost_SucceedsWithoutSpendingActionPoint()
        {
            var source = CreateCombatUnit(1, 80, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, 80, new Vector2Int(1, 0));
            var resolver = new AbilityResolver(new SquareGrid(5, 5), CreateRegistry(source, target));

            var resolved = resolver.TryResolveAbility(CreateAbility(actionPointCost: 0), source, target);

            Assert.IsTrue(resolved);
            Assert.AreEqual(6, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(55, target.MechRuntime.CurrentDurability);
        }

        private static AbilityDefinition CreateAbility(int damage = 25, int minRange = 1, int maxRange = 3,
            int actionPointCost = 3)
        {
            return new AbilityDefinition(101, "Test Ability", minRange, maxRange, damage, actionPointCost,
                TargetRule.EnemyUnit);
        }

        private static CombatUnitRegistry CreateRegistry(CombatUnit teamAUnit, CombatUnit teamBUnit)
        {
            return new CombatUnitRegistry(new[] { teamAUnit }, new[] { teamBUnit });
        }

        private static CombatUnit CreateCombatUnit(int unitId, int currentDurability, Vector2Int position)
        {
            var pilot = new PilotData(1, "Test Pilot", 10);
            var pilotRuntime = new PilotRuntime(6);
            var mech = new MechData(101, "Test Mech", new BasicAttackData(10, 1, 1), 120, 5);
            var mechRuntime = new MechRuntime(currentDurability);

            return new CombatUnit(unitId, pilot, pilotRuntime, mech, mechRuntime, position);
        }
    }
}
