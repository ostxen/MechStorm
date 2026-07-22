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
            var source = CreateCombatUnit(1, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, new Vector2Int(1, 0));
            var registry = CreateRegistry(source, target);

            var gridException = Assert.Throws<ArgumentNullException>(() =>
                new AbilityResolver(null, registry));
            var registryException = Assert.Throws<ArgumentNullException>(() =>
                new AbilityResolver(new SquareGrid(5, 5), null));

            Assert.AreEqual("squareGrid", gridException.ParamName);
            Assert.AreEqual("combatUnitRegistry", registryException.ParamName);
        }

        [Test]
        public void TryResolveAbility_WithNullArgument_Throws()
        {
            var source = CreateCombatUnit(1, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, new Vector2Int(1, 0));
            var resolver = CreateResolver(source, target);
            var ability = CreateAbility();

            var abilityException = Assert.Throws<ArgumentNullException>(() =>
                resolver.TryResolveAbility(null, source, target));
            var sourceException = Assert.Throws<ArgumentNullException>(() =>
                resolver.TryResolveAbility(ability, null, target));
            var targetException = Assert.Throws<ArgumentNullException>(() =>
                resolver.TryResolveAbility(ability, source, null));

            Assert.AreEqual("ability", abilityException.ParamName);
            Assert.AreEqual("source", sourceException.ParamName);
            Assert.AreEqual("target", targetException.ParamName);
        }

        [Test]
        public void TryResolveAbility_WithUnregisteredUnit_Throws()
        {
            var source = CreateCombatUnit(1, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, new Vector2Int(1, 0));
            var resolver = CreateResolver(source, target);
            var ability = CreateAbility();
            var unregisteredSource = CreateCombatUnit(3, new Vector2Int(0, 1));
            var unregisteredTarget = CreateCombatUnit(4, new Vector2Int(1, 1));

            var sourceException = Assert.Throws<ArgumentException>(() =>
                resolver.TryResolveAbility(ability, unregisteredSource, target));
            var targetException = Assert.Throws<ArgumentException>(() =>
                resolver.TryResolveAbility(ability, source, unregisteredTarget));

            Assert.AreEqual("source", sourceException.ParamName);
            Assert.AreEqual("target", targetException.ParamName);
            Assert.AreEqual(100, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithValidEnemyTarget_AppliesDirectDamage()
        {
            var source = CreateCombatUnit(1, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, new Vector2Int(2, 0));
            var resolver = CreateResolver(source, target);

            var resolved = resolver.TryResolveAbility(CreateAbility(damage: 25), source, target);

            Assert.IsTrue(resolved);
            Assert.AreEqual(8, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(75, target.MechRuntime.CurrentDurability);
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, false)]
        public void TryResolveAbility_UsesInclusiveMinimumAndMaximumRange(int distance, bool expectedSuccess)
        {
            var source = CreateCombatUnit(1, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, new Vector2Int(distance, 0));
            var resolver = CreateResolver(source, target, width: 5, height: 1);

            var resolved = resolver.TryResolveAbility(
                CreateAbility(minRange: 2, maxRange: 3, damage: 25), source, target);

            Assert.AreEqual(expectedSuccess, resolved);
            Assert.AreEqual(expectedSuccess ? 8 : 10, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(expectedSuccess ? 75 : 100, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithSameFactionTarget_ReturnsFalseWithoutDamage()
        {
            var source = CreateCombatUnit(1, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, new Vector2Int(1, 0));
            var enemy = CreateCombatUnit(3, new Vector2Int(4, 4));
            var registry = new CombatUnitRegistry(new[] { source, target }, new[] { enemy });
            var resolver = new AbilityResolver(new SquareGrid(5, 5), registry);

            var resolved = resolver.TryResolveAbility(CreateAbility(), source, target);

            Assert.IsFalse(resolved);
            Assert.AreEqual(10, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(100, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithDeadSource_ReturnsFalseWithoutDamage()
        {
            var source = CreateCombatUnit(1, new Vector2Int(0, 0), currentDurability: 0);
            var target = CreateCombatUnit(2, new Vector2Int(1, 0));
            var resolver = CreateResolver(source, target);

            var resolved = resolver.TryResolveAbility(CreateAbility(), source, target);

            Assert.IsFalse(resolved);
            Assert.AreEqual(10, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(100, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithDeadTarget_ReturnsFalse()
        {
            var source = CreateCombatUnit(1, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, new Vector2Int(1, 0), currentDurability: 0);
            var resolver = CreateResolver(source, target);

            var resolved = resolver.TryResolveAbility(CreateAbility(), source, target);

            Assert.IsFalse(resolved);
            Assert.AreEqual(10, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(0, target.MechRuntime.CurrentDurability);
        }

        [TestCase(-1, 0, 1, 0)]
        [TestCase(0, 0, 5, 0)]
        public void TryResolveAbility_WithUnitOutsideGrid_ReturnsFalseWithoutDamage(
            int sourceX, int sourceY, int targetX, int targetY)
        {
            var source = CreateCombatUnit(1, new Vector2Int(sourceX, sourceY));
            var target = CreateCombatUnit(2, new Vector2Int(targetX, targetY));
            var resolver = CreateResolver(source, target);

            var resolved = resolver.TryResolveAbility(
                CreateAbility(maxRange: 10, damage: 25), source, target);

            Assert.IsFalse(resolved);
            Assert.AreEqual(10, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(100, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithLethalDamage_ClampsDurabilityToZero()
        {
            var source = CreateCombatUnit(1, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, new Vector2Int(1, 0), currentDurability: 20);
            var resolver = CreateResolver(source, target);

            var resolved = resolver.TryResolveAbility(CreateAbility(damage: 25), source, target);

            Assert.IsTrue(resolved);
            Assert.AreEqual(8, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(0, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithZeroDamage_ReturnsTrueWithoutChangingDurability()
        {
            var source = CreateCombatUnit(1, new Vector2Int(0, 0));
            var target = CreateCombatUnit(2, new Vector2Int(1, 0));
            var resolver = CreateResolver(source, target);

            var resolved = resolver.TryResolveAbility(CreateAbility(damage: 0), source, target);

            Assert.IsTrue(resolved);
            Assert.AreEqual(8, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(100, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithInsufficientActionPoint_ReturnsFalseWithoutChangingState()
        {
            var source = CreateCombatUnit(1, new Vector2Int(0, 0), currentActionPoint: 1);
            var target = CreateCombatUnit(2, new Vector2Int(1, 0));
            var resolver = CreateResolver(source, target);

            var resolved = resolver.TryResolveAbility(CreateAbility(actionPointCost: 2), source, target);

            Assert.IsFalse(resolved);
            Assert.AreEqual(1, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(100, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithExactActionPoint_SpendsToZeroAndAppliesDamage()
        {
            var source = CreateCombatUnit(1, new Vector2Int(0, 0), currentActionPoint: 2);
            var target = CreateCombatUnit(2, new Vector2Int(1, 0));
            var resolver = CreateResolver(source, target);

            var resolved = resolver.TryResolveAbility(CreateAbility(actionPointCost: 2), source, target);

            Assert.IsTrue(resolved);
            Assert.AreEqual(0, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(75, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_ContinuesUntilRemainingActionPointIsInsufficient()
        {
            var source = CreateCombatUnit(1, new Vector2Int(0, 0), currentActionPoint: 5);
            var target = CreateCombatUnit(2, new Vector2Int(1, 0));
            var resolver = CreateResolver(source, target);
            var ability = CreateAbility(actionPointCost: 2, damage: 10);

            var firstResolved = resolver.TryResolveAbility(ability, source, target);
            var secondResolved = resolver.TryResolveAbility(ability, source, target);
            var thirdResolved = resolver.TryResolveAbility(ability, source, target);

            Assert.IsTrue(firstResolved);
            Assert.IsTrue(secondResolved);
            Assert.IsFalse(thirdResolved);
            Assert.AreEqual(1, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(80, target.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryResolveAbility_WithZeroActionPointCost_DoesNotChangeActionPoint()
        {
            var source = CreateCombatUnit(1, new Vector2Int(0, 0), currentActionPoint: 3);
            var target = CreateCombatUnit(2, new Vector2Int(1, 0));
            var resolver = CreateResolver(source, target);

            var resolved = resolver.TryResolveAbility(CreateAbility(actionPointCost: 0), source, target);

            Assert.IsTrue(resolved);
            Assert.AreEqual(3, source.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(75, target.MechRuntime.CurrentDurability);
        }

        private static AbilityDefinition CreateAbility(
            int minRange = 1, int maxRange = 3, int damage = 25, int actionPointCost = 2)
        {
            return new AbilityDefinition(
                101, "Test Ability", minRange, maxRange, damage, actionPointCost, TargetRule.EnemyUnit);
        }

        private static AbilityResolver CreateResolver(
            CombatUnit source, CombatUnit target, int width = 5, int height = 5)
        {
            return new AbilityResolver(new SquareGrid(width, height), CreateRegistry(source, target));
        }

        private static CombatUnitRegistry CreateRegistry(CombatUnit source, CombatUnit target)
        {
            return new CombatUnitRegistry(new[] { source }, new[] { target });
        }

        private static CombatUnit CreateCombatUnit(
            int unitId, Vector2Int position, int currentDurability = 100, int currentActionPoint = 10)
        {
            var pilot = new PilotData(unitId, $"Pilot {unitId}", 10);
            var pilotRuntime = new PilotRuntime(currentActionPoint);
            var mech = new MechData(unitId, $"Mech {unitId}", new BasicAttackData(10, 1, 1), 100, 3);
            var mechRuntime = new MechRuntime(currentDurability);

            return new CombatUnit(unitId, pilot, pilotRuntime, mech, mechRuntime, position);
        }
    }
}
