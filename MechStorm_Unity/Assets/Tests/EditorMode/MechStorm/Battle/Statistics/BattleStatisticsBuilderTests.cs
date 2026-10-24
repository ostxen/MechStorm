using MechStorm.Battle;
using MechStorm.Battle.Data;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Statistics;
using MechStorm.Battle.Units;
using NUnit.Framework;

namespace MechStorm.Battle.Tests
{
    /// <summary>
    /// 验证 BattleStatisticsBuilder 对实际伤害与击破数的只读聚合规则。
    /// </summary>
    public class BattleStatisticsBuilderTests
    {
        [Test]
        public void Build_BasicAttack_AccumulatesDamageForBothSides()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1), attack: 25);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1), durability: 80);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var result = session.AttackTargetCombatUnit(enemyUnit);

            Assert.IsTrue(result.IsSuccess);
            var report = BattleStatisticsBuilder.Build(session);
            Assert.AreEqual(2, report.Units.Count);
            Assert.AreEqual(1, report.Units[0].UnitId);
            Assert.AreEqual("Pilot 1", report.Units[0].PilotName);
            Assert.AreEqual(25, report.Units[0].DamageDealt);
            Assert.AreEqual(0, report.Units[0].DamageTaken);
            Assert.AreEqual(0, report.Units[0].KillCount);
            Assert.AreEqual(2, report.Units[1].UnitId);
            Assert.AreEqual(0, report.Units[1].DamageDealt);
            Assert.AreEqual(25, report.Units[1].DamageTaken);
            Assert.AreEqual(0, report.Units[1].KillCount);
        }

        [Test]
        public void Build_AbilityDamage_CountsTowardActorStatistics()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1), durability: 80);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var result = session.TryUseAbility(CreateAbility(damage: 30), enemyUnit);

            Assert.IsTrue(result.IsSuccess);
            var report = BattleStatisticsBuilder.Build(session);
            Assert.AreEqual(30, report.Units[0].DamageDealt);
            Assert.AreEqual(0, report.Units[0].KillCount);
            Assert.AreEqual(30, report.Units[1].DamageTaken);
        }

        [Test]
        public void Build_LethalOverkillAttack_CountsKillWithActualDamageOnly()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1), attack: 40);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1), durability: 30);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var result = session.AttackTargetCombatUnit(enemyUnit);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(session.IsBattleEnded);
            var report = BattleStatisticsBuilder.Build(session);
            Assert.AreEqual(30, report.Units[0].DamageDealt);
            Assert.AreEqual(1, report.Units[0].KillCount);
            Assert.AreEqual(30, report.Units[1].DamageTaken);
            Assert.AreEqual(2, report.Units[1].UnitId);
            Assert.AreEqual("Pilot 2", report.Units[1].PilotName);
        }

        [Test]
        public void Build_BothFactions_TracksEachSideActualDamage()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1), attack: 25, durability: 80);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1), attack: 15, durability: 80);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            // 普攻成功后行动自动结束并交接阵营，第二次攻击由 TeamB 执行。
            var playerAttack = session.AttackTargetCombatUnit(enemyUnit);
            var enemyAttack = session.AttackTargetCombatUnit(playerUnit);

            Assert.IsTrue(playerAttack.IsSuccess);
            Assert.IsTrue(enemyAttack.IsSuccess);
            var report = BattleStatisticsBuilder.Build(session);
            Assert.AreEqual(25, report.Units[0].DamageDealt);
            Assert.AreEqual(15, report.Units[0].DamageTaken);
            Assert.AreEqual(15, report.Units[1].DamageDealt);
            Assert.AreEqual(25, report.Units[1].DamageTaken);
        }

        [Test]
        public void Build_FailedAttack_IsNotCounted()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var deadEnemy = CreateCombatUnit(2, new Vector2Int(2, 1), durability: 10);
            var session = CreateBattleSession(playerUnit, deadEnemy);
            deadEnemy.MechRuntime.TakeDamage(deadEnemy.Mech.MaxDurability);

            var result = session.AttackTargetCombatUnit(deadEnemy);

            Assert.IsFalse(result.IsSuccess);
            var report = BattleStatisticsBuilder.Build(session);
            Assert.AreEqual(0, report.Units[0].DamageDealt);
            Assert.AreEqual(0, report.Units[0].KillCount);
            Assert.AreEqual(0, report.Units[1].DamageTaken);
        }

        [Test]
        public void Build_WithoutCombat_LogsKeepsAllUnitsAtZero()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1));
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var report = BattleStatisticsBuilder.Build(session);

            Assert.AreEqual(2, report.Units.Count);
            Assert.AreEqual(0, report.Units[0].DamageDealt);
            Assert.AreEqual(0, report.Units[0].DamageTaken);
            Assert.AreEqual(0, report.Units[0].KillCount);
            Assert.AreEqual(0, report.Units[1].DamageDealt);
            Assert.AreEqual(0, report.Units[1].DamageTaken);
            Assert.AreEqual(0, report.Units[1].KillCount);
        }

        private static BattleSession CreateBattleSession(CombatUnit playerUnit, CombatUnit enemyUnit)
        {
            return new BattleSession(5, 5, new[] { playerUnit }, new[] { enemyUnit });
        }

        private static AbilityDefinition CreateAbility(int damage = 25, int minRange = 1,
            int maxRange = 3, int actionPointCost = 3, bool requiresBasicWeapon = false)
        {
            return new AbilityDefinition(101, "Test Ability", minRange, maxRange, damage,
                actionPointCost, TargetRule.EnemyUnit, requiresBasicWeapon);
        }

        private static CombatUnit CreateCombatUnit(
            int id,
            Vector2Int position,
            int attack = 10,
            int durability = 100,
            int moveRange = 3,
            int minAttackRange = 1,
            int maxAttackRange = 1,
            int basicAttackActionPointCost = 0)
        {
            var pilot = new PilotData(id, $"Pilot {id}", 10);
            var basicAttack = new BasicAttackData(
                attack,
                minAttackRange,
                maxAttackRange,
                basicAttackActionPointCost);
            var mech = new MechData(id, $"Mech {id}", basicAttack, durability, moveRange);
            var factory = new CombatUnitFactory();
            return factory.Create(id, pilot, mech, position);
        }
    }
}
