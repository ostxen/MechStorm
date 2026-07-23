using System;
using System.Collections.Generic;
using MechStorm.Battle.Actions;
using MechStorm.Battle.Data;
using MechStorm.Battle.Diagnostics;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Snapshots;
using MechStorm.Battle.Units;
using NUnit.Framework;

namespace MechStorm.Battle.Tests.Snapshots
{
    public class BattleSnapshotTests
    {
        [Test]
        public void BattleUnitSnapshot_CapturesUnitValues()
        {
            var snapshot = new BattleUnitSnapshot(
                1, CombatFaction.TeamA, new Vector2Int(2, 3), 0, 100, 3, 10);

            Assert.AreEqual(1, snapshot.UnitId);
            Assert.AreEqual(CombatFaction.TeamA, snapshot.Faction);
            Assert.AreEqual(new Vector2Int(2, 3), snapshot.Position);
            Assert.AreEqual(0, snapshot.CurrentDurability);
            Assert.AreEqual(100, snapshot.MaxDurability);
            Assert.AreEqual(3, snapshot.CurrentActionPoint);
            Assert.AreEqual(10, snapshot.MaxActionPoint);
            Assert.IsTrue(snapshot.IsDead);
        }

        [Test]
        public void BattleUnitSnapshot_DoesNotChangeWithCombatUnit()
        {
            var combatUnit = CreateCombatUnit(1);
            var snapshot = new BattleUnitSnapshot(combatUnit.UnitId, CombatFaction.TeamA, combatUnit.Position,
                combatUnit.MechRuntime.CurrentDurability, combatUnit.Mech.MaxDurability,
                combatUnit.PilotRuntime.CurrentActionPoint, combatUnit.Pilot.MaxActionPoint);

            combatUnit.MoveTo(new Vector2Int(4, 4));
            combatUnit.PilotRuntime.TrySpendActionPoint(3);
            combatUnit.MechRuntime.TakeDamage(25);

            Assert.AreEqual(new Vector2Int(1, 0), snapshot.Position);
            Assert.AreEqual(100, snapshot.CurrentDurability);
            Assert.AreEqual(10, snapshot.CurrentActionPoint);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void BattleUnitSnapshot_WithNonPositiveUnitId_Throws(int unitId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BattleUnitSnapshot(unitId, CombatFaction.TeamA, new Vector2Int(1, 1), 100, 100, 10, 10));
        }

        [TestCase(-1, 10)]
        [TestCase(11, 10)]
        [TestCase(0, -1)]
        public void BattleUnitSnapshot_WithInvalidActionPoint_Throws(
            int currentActionPoint, int maxActionPoint)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BattleUnitSnapshot(1, CombatFaction.TeamA, new Vector2Int(1, 1), 100, 100,
                    currentActionPoint, maxActionPoint));
        }

        [Test]
        public void BattleSnapshot_CopiesUnitCollection()
        {
            var currentUnit = new BattleUnitSnapshot(
                1, CombatFaction.TeamA, new Vector2Int(1, 1), 80, 100, 7, 10);
            var enemyUnit = new BattleUnitSnapshot(
                2, CombatFaction.TeamB, new Vector2Int(3, 1), 100, 100, 10, 10);
            var units = new[] { currentUnit, enemyUnit };

            var snapshot = new BattleSnapshot(5, 4, 2, CombatFaction.TeamA, 1, units);
            units[0] = enemyUnit;

            Assert.AreEqual(5, snapshot.BoardWidth);
            Assert.AreEqual(4, snapshot.BoardHeight);
            Assert.AreEqual(2, snapshot.RoundNumber);
            Assert.AreEqual(CombatFaction.TeamA, snapshot.CurrentFaction);
            Assert.AreEqual(1, snapshot.CurrentUnitId);
            Assert.AreSame(currentUnit, snapshot.Units[0]);
            Assert.AreSame(enemyUnit, snapshot.Units[1]);
        }

        [Test]
        public void BattleSnapshot_WithMismatchedCurrentUnit_Throws()
        {
            var unit = new BattleUnitSnapshot(
                1, CombatFaction.TeamA, new Vector2Int(1, 1), 80, 100, 10, 10);

            Assert.Throws<ArgumentException>(() =>
                new BattleSnapshot(5, 4, 1, CombatFaction.TeamA, 2, new[] { unit }));
        }

        [Test]
        public void BattleActionLog_CopiesActionResultValues()
        {
            var actorUnit = CreateCombatUnit(1);
            var targetUnit = CreateCombatUnit(2);
            var result = BattleActionResult.AttackSucceeded(3, actorUnit, targetUnit, 80, 55);

            var log = new BattleActionLog(result, actorUnit.UnitId, targetUnitId: targetUnit.UnitId);

            Assert.AreEqual(3, log.Sequence);
            Assert.AreEqual(BattleActionType.Attack, log.ActionType);
            Assert.IsTrue(log.IsSuccess);
            Assert.AreEqual(BattleActionFailureReason.None, log.FailureReason);
            Assert.AreEqual(1, log.ActorUnitId);
            Assert.AreEqual(2, log.TargetUnitId);
            Assert.IsNull(log.NextUnitId);
            Assert.AreEqual(80, log.DurabilityBefore);
            Assert.AreEqual(55, log.DurabilityAfter);
            CollectionAssert.AreEqual(new[] { BattleActionChangeType.DamageApplied }, log.ChangeTypes);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<BattleActionChangeType>)log.ChangeTypes)[0] = BattleActionChangeType.UnitDied);
        }

        [Test]
        public void BattleActionLog_CopiesAbilityIdentityAndActionPointValues()
        {
            var actorUnit = CreateCombatUnit(1);
            var targetUnit = CreateCombatUnit(2);
            var result = BattleActionResult.AbilitySucceeded(
                4, actorUnit, targetUnit, 101, 10, 7, 80, 55);

            var log = new BattleActionLog(result, actorUnit.UnitId, targetUnitId: targetUnit.UnitId);

            Assert.AreEqual(BattleActionType.UseAbility, log.ActionType);
            Assert.AreEqual(101, log.AbilityId);
            Assert.AreEqual(10, log.ActionPointBefore);
            Assert.AreEqual(7, log.ActionPointAfter);
            Assert.AreEqual(targetUnit.UnitId, log.TargetUnitId);
            CollectionAssert.AreEqual(
                new[]
                {
                    BattleActionChangeType.ActionPointSpent,
                    BattleActionChangeType.DamageApplied
                },
                log.ChangeTypes);
        }

        [Test]
        public void BattleActionLog_AbilityRequiresTargetUnitId()
        {
            var actorUnit = CreateCombatUnit(1);
            var targetUnit = CreateCombatUnit(2);
            var result = BattleActionResult.AbilityFailed(
                4, actorUnit, targetUnit, 101, BattleActionFailureReason.TargetOutOfRange);

            Assert.Throws<ArgumentException>(() =>
                new BattleActionLog(result, actorUnit.UnitId));
        }

        [Test]
        public void BattleDebugJsonSerializer_ExportsVersionedSnapshotAndActionLogs()
        {
            var actorUnit = CreateCombatUnit(1);
            var targetUnit = CreateCombatUnit(2);
            var unitSnapshots = new[]
            {
                new BattleUnitSnapshot(1, CombatFaction.TeamA, new Vector2Int(1, 1), 100, 100, 7, 10),
                new BattleUnitSnapshot(2, CombatFaction.TeamB, new Vector2Int(2, 1), 55, 100, 10, 10),
            };
            var snapshot = new BattleSnapshot(5, 4, 1, CombatFaction.TeamA, 1, unitSnapshots);
            var result = BattleActionResult.AttackSucceeded(1, actorUnit, targetUnit, 80, 55);
            var actionLog = new BattleActionLog(result, actorUnit.UnitId, targetUnitId: targetUnit.UnitId);
            var failedResult = BattleActionResult.Failed(2, BattleActionType.Attack, actorUnit,
                BattleActionFailureReason.TargetOutOfRange);
            var failedActionLog = new BattleActionLog(failedResult, actorUnit.UnitId, targetUnitId: targetUnit.UnitId);
            var abilityResult = BattleActionResult.AbilitySucceeded(
                3, actorUnit, targetUnit, 101, 10, 7, 80, 55);
            var abilityActionLog = new BattleActionLog(
                abilityResult, actorUnit.UnitId, targetUnitId: targetUnit.UnitId);
            var failedAbilityResult = BattleActionResult.AbilityFailed(
                4, actorUnit, targetUnit, 101, BattleActionFailureReason.InsufficientActionPoint);
            var failedAbilityActionLog = new BattleActionLog(
                failedAbilityResult, actorUnit.UnitId, targetUnitId: targetUnit.UnitId);

            var json = BattleDebugJsonSerializer.Serialize(snapshot,
                new[] { actionLog, failedActionLog, abilityActionLog, failedAbilityActionLog });

            StringAssert.Contains("\"schemaVersion\": 3", json);
            StringAssert.Contains("\"currentFaction\": \"TeamA\"", json);
            StringAssert.Contains("\"currentUnitId\": 1", json);
            StringAssert.Contains("\"x\": 1", json);
            StringAssert.Contains("\"currentActionPoint\": 7", json);
            StringAssert.Contains("\"maxActionPoint\": 10", json);
            StringAssert.Contains("\"actionType\": \"Attack\"", json);
            StringAssert.Contains("\"actionType\": \"UseAbility\"", json);
            StringAssert.Contains("\"changeTypes\": [", json);
            StringAssert.Contains("\"DamageApplied\"", json);
            StringAssert.Contains("\"failureReason\": \"TargetOutOfRange\"", json);
            StringAssert.Contains("\"failureReason\": \"InsufficientActionPoint\"", json);
            StringAssert.Contains("\"targetUnitId\": 2", json);
            StringAssert.Contains("\"abilityId\": 101", json);
            StringAssert.Contains("\"actionPointBefore\": 10", json);
            StringAssert.Contains("\"actionPointAfter\": 7", json);
            StringAssert.Contains("\"nextUnitId\": null", json);
        }

        private static CombatUnit CreateCombatUnit(int id)
        {
            var pilot = new PilotData(id, $"Pilot {id}", 10);
            var mech = new MechData(id, $"Mech {id}", new BasicAttackData(10, 1, 1), 100, 3);
            var factory = new CombatUnitFactory();

            return factory.Create(id, pilot, mech, new Vector2Int(id, 0));
        }
    }
}
