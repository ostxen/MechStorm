using MechStorm.Battle.Actions;
using System;
using MechStorm.Battle.Data;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Outcomes;
using MechStorm.Battle.Units;
using NUnit.Framework;

namespace MechStorm.Battle.Tests.Actions
{
    /// <summary>
    /// 验证每类操作 Result 的字段、变化顺序以及 Result 与 Outcome 的组合约束。
    /// </summary>
    public class BattleActionResultTests
    {
        [Test]
        public void MoveSucceeded_CreatesSuccessfulMovementResult()
        {
            var actorUnit = CreateCombatUnit(1);
            var positionBefore = new Vector2Int(1, 1);
            var positionAfter = new Vector2Int(2, 1);

            var result = BattleActionResult.MoveSucceeded(1, actorUnit, positionBefore, positionAfter);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(BattleActionType.Move, result.ActionType);
            Assert.AreEqual(BattleActionFailureReason.None, result.FailureReason);
            Assert.AreSame(actorUnit, result.ActorUnit);
            Assert.AreEqual(positionBefore, result.PositionBefore);
            Assert.AreEqual(positionAfter, result.PositionAfter);
            CollectionAssert.AreEqual(new[] { BattleActionChangeType.UnitMoved }, result.ChangeTypes);
        }

        [Test]
        public void AttackSucceeded_WithLethalDamage_OrdersDamageBeforeDeath()
        {
            var actorUnit = CreateCombatUnit(1);
            var targetUnit = CreateCombatUnit(2);

            var result = BattleActionResult.AttackSucceeded(2, actorUnit, targetUnit, 30, 0);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreSame(actorUnit, result.ActorUnit);
            Assert.AreSame(targetUnit, result.TargetUnit);
            Assert.AreEqual(30, result.DurabilityBefore);
            Assert.AreEqual(0, result.DurabilityAfter);
            CollectionAssert.AreEqual(
                new[]
                {
                    BattleActionChangeType.DamageApplied,
                    BattleActionChangeType.UnitDied
                },
                result.ChangeTypes);
        }

        [Test]
        public void AttackSucceeded_WithCompletedOutcome_OrdersBattleEndAfterDeath()
        {
            var actorUnit = CreateCombatUnit(1);
            var targetUnit = CreateCombatUnit(2);
            var outcome = BattleOutcome.FactionEliminated(
                CombatFaction.TeamA, CombatFaction.TeamB);

            var result = BattleActionResult.AttackSucceeded(
                2, actorUnit, targetUnit, 30, 0, outcome);

            Assert.AreSame(outcome, result.Outcome);
            CollectionAssert.AreEqual(
                new[]
                {
                    BattleActionChangeType.DamageApplied,
                    BattleActionChangeType.UnitDied,
                    BattleActionChangeType.FactionEliminated,
                    BattleActionChangeType.BattleEnded
                },
                result.ChangeTypes);
        }

        [Test]
        public void ActionEnded_WhenNewRoundStarts_CreatesOrderedChangeTypes()
        {
            var actorUnit = CreateCombatUnit(1);
            var nextCombatUnit = CreateCombatUnit(2);

            var result = BattleActionResult.ActionEnded(3, actorUnit, nextCombatUnit, 1, 2,
                CombatFaction.TeamB, CombatFaction.TeamA, 3, 10);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreSame(nextCombatUnit, result.NextCombatUnit);
            CollectionAssert.AreEqual(
                new[]
                {
                    BattleActionChangeType.UnitActionEnded,
                    BattleActionChangeType.FactionTurnEnded,
                    BattleActionChangeType.RoundStarted,
                    BattleActionChangeType.FactionTurnStarted,
                    BattleActionChangeType.CurrentUnitChanged,
                    BattleActionChangeType.ActionPointRestored
                },
                result.ChangeTypes);
            Assert.AreEqual(3, result.ActionPointBefore);
            Assert.AreEqual(10, result.ActionPointAfter);
        }

        [Test]
        public void FactionTurnEnded_CreatesExplicitTurnResult()
        {
            var actorUnit = CreateCombatUnit(1);
            var nextCombatUnit = CreateCombatUnit(2);

            var result = BattleActionResult.FactionTurnEnded(
                4, actorUnit, nextCombatUnit, 1, 1,
                CombatFaction.TeamA, CombatFaction.TeamB, 3, 10);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(BattleActionType.EndFactionTurn, result.ActionType);
            Assert.AreSame(nextCombatUnit, result.NextCombatUnit);
            CollectionAssert.AreEqual(
                new[]
                {
                    BattleActionChangeType.UnitActionEnded,
                    BattleActionChangeType.FactionTurnEnded,
                    BattleActionChangeType.FactionTurnStarted,
                    BattleActionChangeType.CurrentUnitChanged,
                    BattleActionChangeType.ActionPointRestored
                },
                result.ChangeTypes);
        }

        [Test]
        public void AbilitySucceeded_WithLethalDamage_OrdersActionPointBeforeDamageAndDeath()
        {
            var actorUnit = CreateCombatUnit(1);
            var targetUnit = CreateCombatUnit(2);

            var result = BattleActionResult.AbilitySucceeded(
                4, actorUnit, targetUnit, 101, 10, 7, 30, 0);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(BattleActionType.UseAbility, result.ActionType);
            Assert.AreEqual(101, result.AbilityId);
            Assert.AreSame(targetUnit, result.TargetUnit);
            Assert.AreEqual(10, result.ActionPointBefore);
            Assert.AreEqual(7, result.ActionPointAfter);
            Assert.AreEqual(30, result.DurabilityBefore);
            Assert.AreEqual(0, result.DurabilityAfter);
            CollectionAssert.AreEqual(
                new[]
                {
                    BattleActionChangeType.ActionPointSpent,
                    BattleActionChangeType.DamageApplied,
                    BattleActionChangeType.UnitDied
                },
                result.ChangeTypes);
        }

        [Test]
        public void AbilitySucceeded_WithCompletedOutcome_OrdersBattleEndAfterDeath()
        {
            var actorUnit = CreateCombatUnit(1);
            var targetUnit = CreateCombatUnit(2);
            var outcome = BattleOutcome.FactionEliminated(
                CombatFaction.TeamA, CombatFaction.TeamB);

            var result = BattleActionResult.AbilitySucceeded(
                4, actorUnit, targetUnit, 101, 10, 7, 30, 0, outcome);

            Assert.AreSame(outcome, result.Outcome);
            CollectionAssert.AreEqual(
                new[]
                {
                    BattleActionChangeType.ActionPointSpent,
                    BattleActionChangeType.DamageApplied,
                    BattleActionChangeType.UnitDied,
                    BattleActionChangeType.FactionEliminated,
                    BattleActionChangeType.BattleEnded
                },
                result.ChangeTypes);
        }

        [Test]
        public void AbilityFailed_PreservesAbilityAndTargetWithoutChanges()
        {
            var actorUnit = CreateCombatUnit(1);
            var targetUnit = CreateCombatUnit(2);

            var result = BattleActionResult.AbilityFailed(5, actorUnit, targetUnit, 101,
                BattleActionFailureReason.InsufficientActionPoint);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(BattleActionType.UseAbility, result.ActionType);
            Assert.AreEqual(BattleActionFailureReason.InsufficientActionPoint, result.FailureReason);
            Assert.AreEqual(101, result.AbilityId);
            Assert.AreSame(targetUnit, result.TargetUnit);
            Assert.IsNull(result.ActionPointBefore);
            Assert.IsNull(result.ActionPointAfter);
            Assert.IsEmpty(result.ChangeTypes);
        }

        [Test]
        public void AbilitySucceeded_WithoutActionPointChange_DoesNotReportSpend()
        {
            var actorUnit = CreateCombatUnit(1);
            var targetUnit = CreateCombatUnit(2);

            var result = BattleActionResult.AbilitySucceeded(
                5, actorUnit, targetUnit, 101, 10, 10, 80, 55);

            CollectionAssert.AreEqual(
                new[] { BattleActionChangeType.DamageApplied },
                result.ChangeTypes);
        }

        [Test]
        public void AbilitySucceeded_WithoutDurabilityChange_DoesNotReportDamage()
        {
            var actorUnit = CreateCombatUnit(1);
            var targetUnit = CreateCombatUnit(2);

            var result = BattleActionResult.AbilitySucceeded(
                5, actorUnit, targetUnit, 101, 10, 7, 80, 80);

            CollectionAssert.AreEqual(
                new[] { BattleActionChangeType.ActionPointSpent },
                result.ChangeTypes);
        }

        [Test]
        public void ActionEnded_WithoutActionPointChange_DoesNotReportRestore()
        {
            var actorUnit = CreateCombatUnit(1);
            var nextCombatUnit = CreateCombatUnit(2);

            var result = BattleActionResult.ActionEnded(6, actorUnit, nextCombatUnit, 1, 1,
                CombatFaction.TeamA, CombatFaction.TeamA, 10, 10);

            CollectionAssert.DoesNotContain(
                result.ChangeTypes,
                BattleActionChangeType.ActionPointRestored);
        }

        [Test]
        public void Failed_CreatesFailureWithoutChangeTypes()
        {
            var actorUnit = CreateCombatUnit(1);

            var result = BattleActionResult.Failed(4, BattleActionType.Move, actorUnit,
                BattleActionFailureReason.InvalidMoveTarget);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(BattleActionFailureReason.InvalidMoveTarget, result.FailureReason);
            Assert.IsEmpty(result.ChangeTypes);
        }

        [Test]
        public void Failed_AfterBattleEnded_PreservesCompletedOutcome()
        {
            var actorUnit = CreateCombatUnit(1);
            var outcome = BattleOutcome.FactionEliminated(
                CombatFaction.TeamA, CombatFaction.TeamB);

            var result = BattleActionResult.Failed(
                4, BattleActionType.Move, actorUnit,
                BattleActionFailureReason.BattleAlreadyEnded, outcome);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreSame(outcome, result.Outcome);
            Assert.IsEmpty(result.ChangeTypes);
        }

        [Test]
        public void Failed_WithBattleAlreadyEndedAndInProgressOutcome_Throws()
        {
            var actorUnit = CreateCombatUnit(1);

            Assert.Throws<ArgumentException>(() =>
                BattleActionResult.Failed(
                    4, BattleActionType.Move, actorUnit,
                    BattleActionFailureReason.BattleAlreadyEnded));
        }

        [Test]
        public void Failed_WithOtherReasonAndCompletedOutcome_Throws()
        {
            var actorUnit = CreateCombatUnit(1);
            var outcome = BattleOutcome.FactionEliminated(
                CombatFaction.TeamA, CombatFaction.TeamB);

            Assert.Throws<ArgumentException>(() =>
                BattleActionResult.Failed(
                    4, BattleActionType.Move, actorUnit,
                    BattleActionFailureReason.InvalidMoveTarget, outcome));
        }

        [Test]
        public void AttackSucceeded_WithNonLethalCompletedOutcome_Throws()
        {
            var actorUnit = CreateCombatUnit(1);
            var targetUnit = CreateCombatUnit(2);
            var outcome = BattleOutcome.FactionEliminated(
                CombatFaction.TeamA, CombatFaction.TeamB);

            Assert.Throws<ArgumentException>(() =>
                BattleActionResult.AttackSucceeded(
                    4, actorUnit, targetUnit, 30, 10, outcome));
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
