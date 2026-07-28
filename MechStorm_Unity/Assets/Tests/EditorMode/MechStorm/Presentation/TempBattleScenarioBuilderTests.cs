using System.Collections.Generic;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using NUnit.Framework;
using UnityEngine;
using BattleVector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Tests
{
    public sealed class TempBattleScenarioBuilderTests
    {
        private readonly List<TempBattleScenarioConfig> _configs = new();
        private TempBattleScenarioBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _builder = new TempBattleScenarioBuilder();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var config in _configs)
            {
                Object.DestroyImmediate(config);
            }

            _configs.Clear();
        }

        [Test]
        public void TryBuild_WithValidConfig_CreatesSessionUnitsAndAbilities()
        {
            var config = CreateValidConfig();

            var succeeded = _builder.TryBuild(config, out var buildResult, out var validationResult);

            Assert.IsTrue(succeeded);
            Assert.IsTrue(validationResult.IsValid);
            Assert.IsNotNull(buildResult);
            Assert.IsNotNull(buildResult.BattleSession);
            Assert.AreEqual(4, buildResult.Units.Count);
            Assert.AreEqual(
                2,
                buildResult.BattleSession.GetFactionCombatUnits(CombatFaction.TeamA).Count);
            Assert.AreEqual(
                2,
                buildResult.BattleSession.GetFactionCombatUnits(CombatFaction.TeamB).Count);

            var unit = buildResult.Units[0];
            Assert.AreEqual(1, unit.UnitId);
            Assert.AreEqual(new BattleVector2Int(1, 1), unit.Position);
            Assert.AreEqual(11, unit.Pilot.Id);
            Assert.AreEqual("Pilot A1", unit.Pilot.Name);
            Assert.AreEqual(8, unit.Pilot.MaxActionPoint);
            Assert.AreEqual(8, unit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(21, unit.Mech.Id);
            Assert.AreEqual("Mech A1", unit.Mech.Name);
            Assert.AreEqual(100, unit.Mech.MaxDurability);
            Assert.AreEqual(100, unit.MechRuntime.CurrentDurability);
            Assert.AreEqual(4, unit.Mech.MoveRange);
            Assert.AreEqual(20, unit.Mech.BasicAttack.Damage);
            Assert.AreEqual(1, unit.Mech.BasicAttack.MinRange);
            Assert.AreEqual(2, unit.Mech.BasicAttack.MaxRange);

            var ability = buildResult.GetAbilitiesForUnit(unit.UnitId)[0];
            Assert.AreEqual(101, ability.AbilityId);
            Assert.AreEqual("Ability A1", ability.Name);
            Assert.AreEqual(3, ability.ActionPointCost);
            Assert.AreEqual(30, ability.Damage);
            Assert.AreEqual(1, ability.MinRange);
            Assert.AreEqual(3, ability.MaxRange);
            Assert.AreEqual(TargetRule.EnemyUnit, ability.TargetRule);
            Assert.IsEmpty(buildResult.GetAbilitiesForUnit(999));
        }

        [Test]
        public void TryBuild_WithInvalidConfig_ReturnsErrorsWithoutPartialResult()
        {
            var config = CreateConfig(
                "{\"_boardWidth\":0,\"_boardHeight\":10,\"_units\":[]}");

            var succeeded = _builder.TryBuild(config, out var buildResult, out var validationResult);

            Assert.IsFalse(succeeded);
            Assert.IsNull(buildResult);
            Assert.IsFalse(validationResult.IsValid);
            CollectionAssert.Contains(validationResult.Errors, "Board width must be greater than zero.");
            StringAssert.Contains("TeamA=0, TeamB=0, Neutral=0", validationResult.ErrorMessage);
        }

        [Test]
        public void TryBuild_Twice_CreatesIndependentObjectGraphs()
        {
            var config = CreateValidConfig();

            Assert.IsTrue(_builder.TryBuild(config, out var firstBuild, out _));
            Assert.IsTrue(_builder.TryBuild(config, out var secondBuild, out _));

            Assert.AreNotSame(firstBuild.BattleSession, secondBuild.BattleSession);
            for (var index = 0; index < firstBuild.Units.Count; index++)
            {
                var firstUnit = firstBuild.Units[index];
                var secondUnit = secondBuild.Units[index];
                Assert.AreNotSame(firstUnit, secondUnit);
                Assert.AreNotSame(firstUnit.Pilot, secondUnit.Pilot);
                Assert.AreNotSame(firstUnit.PilotRuntime, secondUnit.PilotRuntime);
                Assert.AreNotSame(firstUnit.Mech, secondUnit.Mech);
                Assert.AreNotSame(firstUnit.Mech.BasicAttack, secondUnit.Mech.BasicAttack);
                Assert.AreNotSame(firstUnit.MechRuntime, secondUnit.MechRuntime);
                Assert.AreNotSame(
                    firstBuild.GetAbilitiesForUnit(firstUnit.UnitId)[0],
                    secondBuild.GetAbilitiesForUnit(secondUnit.UnitId)[0]);
            }
        }

        [Test]
        public void TryBuild_AfterFirstRuntimeChanges_UsesOriginalConfigValues()
        {
            var config = CreateValidConfig();
            Assert.IsTrue(_builder.TryBuild(config, out var firstBuild, out _));
            var firstUnit = firstBuild.Units[0];
            firstUnit.MoveTo(new BattleVector2Int(5, 5));
            firstUnit.MechRuntime.TakeDamage(40);
            Assert.IsTrue(firstUnit.PilotRuntime.TrySpendActionPoint(3));

            Assert.IsTrue(_builder.TryBuild(config, out var secondBuild, out _));

            var secondUnit = secondBuild.Units[0];
            Assert.AreEqual(new BattleVector2Int(1, 1), secondUnit.Position);
            Assert.AreEqual(100, secondUnit.MechRuntime.CurrentDurability);
            Assert.AreEqual(8, secondUnit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(new Vector2Int(1, 1), config.Units[0].InitialPosition);
            Assert.AreEqual(100, config.Units[0].Mech.MaxDurability);
            Assert.AreEqual(8, config.Units[0].Pilot.MaxActionPoint);
        }

        [Test]
        public void TryBuild_DoesNotWriteBackToScriptableObject()
        {
            var config = CreateValidConfig();
            var before = JsonUtility.ToJson(config);

            Assert.IsTrue(_builder.TryBuild(config, out _, out _));

            Assert.AreEqual(before, JsonUtility.ToJson(config));
        }

        private TempBattleScenarioConfig CreateValidConfig()
        {
            return Register(TempBattleScenarioTestConfigFactory.CreateValid());
        }

        private TempBattleScenarioConfig CreateConfig(string json)
        {
            return Register(TempBattleScenarioTestConfigFactory.CreateFromJson(json));
        }

        private TempBattleScenarioConfig Register(TempBattleScenarioConfig config)
        {
            _configs.Add(config);
            return config;
        }
    }
}
