using System;
using MechStorm.Battle.Data;
using NUnit.Framework;

namespace MechStorm.Battle.Tests.Data
{
    public class AbilityDefinitionTests
    {
        [Test]
        public void Constructor_WithValidValues_StoresValues()
        {
            var ability = new AbilityDefinition(101, "Test Ability", 2, 4, 25, 2, TargetRule.EnemyUnit);

            Assert.AreEqual(101, ability.AbilityId);
            Assert.AreEqual("Test Ability", ability.Name);
            Assert.AreEqual(2, ability.MinRange);
            Assert.AreEqual(4, ability.MaxRange);
            Assert.AreEqual(25, ability.Damage);
            Assert.AreEqual(2, ability.ActionPointCost);
            Assert.AreEqual(TargetRule.EnemyUnit, ability.TargetRule);
            Assert.IsFalse(ability.RequiresBasicWeapon);
        }

        [Test]
        public void Constructor_WithBasicWeaponDependency_StoresDependency()
        {
            var ability = new AbilityDefinition(
                101, "Weapon Ability", 2, 4, 25, 2, TargetRule.EnemyUnit, true);

            Assert.IsTrue(ability.RequiresBasicWeapon);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_WithNonPositiveAbilityId_Throws(int abilityId)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new AbilityDefinition(abilityId, "Test Ability", 1, 2, 25, 2, TargetRule.EnemyUnit));

            Assert.AreEqual("abilityId", exception.ParamName);
        }

        [Test]
        public void Constructor_WithNullName_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new AbilityDefinition(101, null, 1, 2, 25, 2, TargetRule.EnemyUnit));

            Assert.AreEqual("name", exception.ParamName);
        }

        [TestCase("")]
        [TestCase(" ")]
        public void Constructor_WithEmptyOrWhitespaceName_Throws(string name)
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                new AbilityDefinition(101, name, 1, 2, 25, 2, TargetRule.EnemyUnit));

            Assert.AreEqual("name", exception.ParamName);
        }

        [Test]
        public void Constructor_WithMinimumRangeBelowOne_Throws()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new AbilityDefinition(101, "Test Ability", 0, 2, 25, 2, TargetRule.EnemyUnit));

            Assert.AreEqual("minRange", exception.ParamName);
        }

        [Test]
        public void Constructor_WithMaximumRangeBelowMinimumRange_Throws()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new AbilityDefinition(101, "Test Ability", 3, 2, 25, 2, TargetRule.EnemyUnit));

            Assert.AreEqual("maxRange", exception.ParamName);
        }

        [Test]
        public void Constructor_WithNegativeDamage_Throws()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new AbilityDefinition(101, "Test Ability", 1, 2, -1, 2, TargetRule.EnemyUnit));

            Assert.AreEqual("damage", exception.ParamName);
        }

        [Test]
        public void Constructor_WithZeroDamage_StoresZero()
        {
            var ability = new AbilityDefinition(101, "Test Ability", 1, 2, 0, 2, TargetRule.EnemyUnit);

            Assert.AreEqual(0, ability.Damage);
        }

        [Test]
        public void Constructor_WithNegativeActionPointCost_Throws()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new AbilityDefinition(101, "Test Ability", 1, 2, 25, -1, TargetRule.EnemyUnit));

            Assert.AreEqual("actionPointCost", exception.ParamName);
        }

        [Test]
        public void Constructor_WithZeroActionPointCost_StoresZero()
        {
            var ability = new AbilityDefinition(101, "Test Ability", 1, 2, 25, 0, TargetRule.EnemyUnit);

            Assert.AreEqual(0, ability.ActionPointCost);
        }

        [Test]
        public void Constructor_WithUndefinedTargetRule_Throws()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new AbilityDefinition(101, "Test Ability", 1, 2, 25, 2, (TargetRule)int.MaxValue));

            Assert.AreEqual("targetRule", exception.ParamName);
        }
    }
}
