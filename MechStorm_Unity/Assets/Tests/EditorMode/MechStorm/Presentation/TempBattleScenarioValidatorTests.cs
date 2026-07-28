using System.Collections.Generic;
using System.Reflection;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using NUnit.Framework;
using UnityEngine;

namespace MechStorm.Presentation.Tests
{
    public sealed class TempBattleScenarioValidatorTests
    {
        private readonly List<TempBattleScenarioConfig> _configs = new();
        private TempBattleScenarioValidator _validator;

        [SetUp]
        public void SetUp()
        {
            _validator = new TempBattleScenarioValidator();
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
        public void Validate_WithValidTwoVersusTwoConfig_ReturnsValidResult()
        {
            var result = _validator.Validate(CreateValidConfig());

            Assert.IsTrue(result.IsValid);
            Assert.IsEmpty(result.Errors);
            Assert.AreEqual(string.Empty, result.ErrorMessage);
        }

        [Test]
        public void Validate_WithNullConfig_ReturnsRequiredError()
        {
            var result = _validator.Validate(null);

            Assert.IsFalse(result.IsValid);
            CollectionAssert.Contains(result.Errors, "Battle scenario config is required.");
        }

        [Test]
        public void Validate_WithInvalidBoardAndNoUnits_ReturnsAllErrors()
        {
            var config = CreateConfig(
                "{\"_boardWidth\":0,\"_boardHeight\":-1,\"_units\":[]}");

            var result = _validator.Validate(config);

            Assert.IsFalse(result.IsValid);
            CollectionAssert.Contains(result.Errors, "Board width must be greater than zero.");
            CollectionAssert.Contains(result.Errors, "Board height must be greater than zero.");
            StringAssert.Contains("TeamA=0, TeamB=0, Neutral=0", result.ErrorMessage);
        }

        [Test]
        public void Validate_WithDuplicateIdentifiersAndPosition_ReturnsAllDuplicateErrors()
        {
            var config = CreateValidConfig();
            var firstUnit = config.Units[0];
            var secondUnit = config.Units[1];
            SetPrivateField(secondUnit, "_unitId", firstUnit.UnitId);
            SetPrivateField(secondUnit, "_initialPosition", firstUnit.InitialPosition);
            SetPrivateField(secondUnit.Abilities[0], "_abilityId", firstUnit.Abilities[0].AbilityId);

            var result = _validator.Validate(config);

            Assert.IsFalse(result.IsValid);
            CollectionAssert.Contains(result.Errors, "Duplicate unit ID: 1.");
            CollectionAssert.Contains(result.Errors, "Duplicate ability ID: 101.");
            CollectionAssert.Contains(result.Errors, "Multiple units occupy position (1, 1).");
        }

        [Test]
        public void Validate_WithNonPositiveIdentifiers_ReturnsIdentifierErrors()
        {
            var config = CreateValidConfig();
            var unit = config.Units[0];
            SetPrivateField(unit, "_unitId", 0);
            SetPrivateField(unit.Pilot, "_id", 0);
            SetPrivateField(unit.Mech, "_id", 0);
            SetPrivateField(unit.Abilities[0], "_abilityId", 0);

            var result = _validator.Validate(config);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("Unit at index 0 must have an ID greater than zero", result.ErrorMessage);
            StringAssert.Contains("pilot ID must be greater than zero", result.ErrorMessage);
            StringAssert.Contains("mech ID must be greater than zero", result.ErrorMessage);
            StringAssert.Contains("ability at index 0 must have an ID greater than zero", result.ErrorMessage);
        }

        [Test]
        public void Validate_WithInvalidNestedValues_ReturnsEveryValueError()
        {
            var config = CreateValidConfig();
            var unit = config.Units[0];
            SetPrivateField(unit, "_initialPosition", new Vector2Int(10, -1));
            SetPrivateField(unit.Pilot, "_name", " ");
            SetPrivateField(unit.Pilot, "_maxActionPoint", -1);
            SetPrivateField(unit.Mech, "_maxDurability", 0);
            SetPrivateField(unit.Mech, "_moveRange", -1);
            SetPrivateField(unit.Mech.BasicAttack, "_damage", -1);
            SetPrivateField(unit.Mech.BasicAttack, "_minRange", 0);
            SetPrivateField(unit.Mech.BasicAttack, "_maxRange", -1);
            SetPrivateField(unit.Abilities[0], "_name", string.Empty);
            SetPrivateField(unit.Abilities[0], "_actionPointCost", -1);
            SetPrivateField(unit.Abilities[0], "_damage", -1);
            SetPrivateField(unit.Abilities[0], "_minRange", 0);
            SetPrivateField(unit.Abilities[0], "_maxRange", -1);
            SetPrivateField(unit.Abilities[0], "_targetRule", (TargetRule)999);

            var result = _validator.Validate(config);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("outside board 10x10", result.ErrorMessage);
            StringAssert.Contains("pilot name is required", result.ErrorMessage);
            StringAssert.Contains("maximum action point must be non-negative", result.ErrorMessage);
            StringAssert.Contains("maximum durability must be greater than zero", result.ErrorMessage);
            StringAssert.Contains("move range must be non-negative", result.ErrorMessage);
            StringAssert.Contains("basic attack damage must be non-negative", result.ErrorMessage);
            StringAssert.Contains("basic attack minimum range must be at least one", result.ErrorMessage);
            StringAssert.Contains("basic attack maximum range must be greater", result.ErrorMessage);
            StringAssert.Contains("Ability 101 name is required", result.ErrorMessage);
            StringAssert.Contains("action point cost must be non-negative", result.ErrorMessage);
            StringAssert.Contains("Ability 101 damage must be non-negative", result.ErrorMessage);
            StringAssert.Contains("Ability 101 minimum range must be at least one", result.ErrorMessage);
            StringAssert.Contains("Ability 101 maximum range must be greater", result.ErrorMessage);
            StringAssert.Contains("Ability 101 has an undefined target rule", result.ErrorMessage);
        }

        [Test]
        public void Validate_WithNeutralAndUndefinedFactions_ReturnsFactionErrors()
        {
            var config = CreateValidConfig();
            SetPrivateField(config.Units[0], "_faction", CombatFaction.Neutral);
            SetPrivateField(config.Units[1], "_faction", (CombatFaction)999);

            var result = _validator.Validate(config);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("undefined faction value: 999", result.ErrorMessage);
            StringAssert.Contains("TeamA=0, TeamB=2, Neutral=1", result.ErrorMessage);
        }

        [Test]
        public void Validate_WithMissingNestedConfigs_ReturnsRequiredErrors()
        {
            var config = CreateValidConfig();
            SetPrivateField<TempCombatUnitConfig, TempPilotConfig>(config.Units[0], "_pilot", null);
            SetPrivateField<TempCombatUnitConfig, TempMechConfig>(config.Units[1], "_mech", null);
            SetPrivateField<TempMechConfig, TempBasicAttackConfig>(
                config.Units[2].Mech,
                "_basicAttack",
                null);
            SetPrivateField<TempCombatUnitConfig, List<TempAbilityConfig>>(
                config.Units[3],
                "_abilities",
                null);

            var result = _validator.Validate(config);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("Unit 1 requires a pilot config", result.ErrorMessage);
            StringAssert.Contains("Unit 2 requires a mech config", result.ErrorMessage);
            StringAssert.Contains("Unit 3 requires a basic attack config", result.ErrorMessage);
            StringAssert.Contains("Unit 4 ability list is required", result.ErrorMessage);
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

        private static void SetPrivateField<TTarget, TValue>(
            TTarget target,
            string fieldName,
            TValue value)
        {
            var field = typeof(TTarget).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field {fieldName} was not found on {typeof(TTarget).Name}.");
            field.SetValue(target, value);
        }
    }
}
