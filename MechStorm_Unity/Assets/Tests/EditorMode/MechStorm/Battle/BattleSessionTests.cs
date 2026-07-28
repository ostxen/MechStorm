using System;
using System.Collections.Generic;
using MechStorm.Battle;
using MechStorm.Battle.Actions;
using MechStorm.Battle.Data;
using MechStorm.Battle.Diagnostics;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Outcomes;
using MechStorm.Battle.Units;
using NUnit.Framework;

namespace MechStorm.Battle.Tests
{
    /// <summary>
    /// 验证 BattleSession 的战场操作、回合推进、全灭判定和结束后无副作用规则。
    /// </summary>
    public class BattleSessionTests
    {
        [Test]
        public void Constructor_InitializesBattleStateAndUnits()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(3, 1));

            var session = CreateBattleSession(playerUnit, enemyUnit);

            Assert.AreEqual(5, session.Grid.Width);
            Assert.AreEqual(5, session.Grid.Height);
            Assert.AreEqual(2, session.CombatUnits.Count);
            Assert.AreSame(playerUnit, session.CurrentCombatUnit);
            Assert.AreEqual(CombatFaction.TeamA, session.CurrentFaction);
            Assert.AreEqual(1, session.CurrentRoundNumber);
            Assert.IsFalse(session.IsBattleEnded);
            Assert.AreSame(BattleOutcome.InProgress, session.Outcome);
        }

        [Test]
        public void CreateSnapshot_CapturesCurrentStateWithoutChangingPreviousSnapshot()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1), attack: 25, moveRange: 3);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(3, 1), durability: 80);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var initialSnapshot = session.CreateSnapshot();
            session.TryMoveCurrentCombatUnit(new Vector2Int(2, 1));
            session.AttackTargetCombatUnit(enemyUnit);
            var updatedSnapshot = session.CreateSnapshot();

            Assert.AreEqual(5, initialSnapshot.BoardWidth);
            Assert.AreEqual(5, initialSnapshot.BoardHeight);
            Assert.AreEqual(1, initialSnapshot.RoundNumber);
            Assert.AreEqual(CombatFaction.TeamA, initialSnapshot.CurrentFaction);
            Assert.AreEqual(1, initialSnapshot.CurrentUnitId);
            Assert.AreEqual(new Vector2Int(1, 1), initialSnapshot.Units[0].Position);
            Assert.AreEqual(10, initialSnapshot.Units[0].CurrentActionPoint);
            Assert.AreEqual(10, initialSnapshot.Units[0].MaxActionPoint);
            Assert.AreEqual(80, initialSnapshot.Units[1].CurrentDurability);

            Assert.AreEqual(new Vector2Int(2, 1), updatedSnapshot.Units[0].Position);
            Assert.AreEqual(55, updatedSnapshot.Units[1].CurrentDurability);
            Assert.AreEqual(1, updatedSnapshot.Units[0].UnitId);
            Assert.AreEqual(CombatFaction.TeamB, updatedSnapshot.Units[1].Faction);
        }

        [Test]
        public void Constructor_WithEmptyUnitList_Throws()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(3, 1));

            Assert.Throws<ArgumentException>(() =>
                CreateBattleSession(Array.Empty<CombatUnit>(), new[] { enemyUnit }));
            Assert.Throws<ArgumentException>(() =>
                CreateBattleSession(new[] { playerUnit }, Array.Empty<CombatUnit>()));
        }

        [Test]
        public void Constructor_WithUnitOutsideGrid_Throws()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(-1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(3, 1));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CreateBattleSession(playerUnit, enemyUnit));
        }

        [Test]
        public void Constructor_WithDuplicateUnit_Throws()
        {
            var sharedUnit = CreateCombatUnit(1, new Vector2Int(1, 1));

            Assert.Throws<ArgumentException>(() =>
                CreateBattleSession(sharedUnit, sharedUnit));
        }

        [Test]
        public void Constructor_WithDuplicateUnitId_Throws()
        {
            var teamAUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var teamBUnit = CreateCombatUnit(1, new Vector2Int(3, 1));

            Assert.Throws<ArgumentException>(() =>
                CreateBattleSession(teamAUnit, teamBUnit));
        }

        [Test]
        public void TryMoveCurrentCombatUnit_WithReachableTarget_MovesPlayerUnit()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1), moveRange: 3);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(4, 4));
            var session = CreateBattleSession(playerUnit, enemyUnit);
            var target = new Vector2Int(2, 2);

            var result = session.TryMoveCurrentCombatUnit(target);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Sequence);
            Assert.AreEqual(BattleActionType.Move, result.ActionType);
            Assert.AreSame(playerUnit, result.ActorUnit);
            Assert.AreEqual(new Vector2Int(1, 1), result.PositionBefore);
            Assert.AreEqual(target, result.PositionAfter);
            CollectionAssert.AreEqual(new[] { BattleActionChangeType.UnitMoved }, result.ChangeTypes);
            Assert.AreEqual(target, playerUnit.Position);
        }

        [Test]
        public void TryMoveCurrentCombatUnit_WithUnreachableTarget_ReturnsFailedResult()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1), moveRange: 1);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(4, 4));
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var result = session.TryMoveCurrentCombatUnit(new Vector2Int(3, 1));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(BattleActionFailureReason.InvalidMoveTarget, result.FailureReason);
            Assert.AreSame(playerUnit, result.ActorUnit);
            Assert.IsEmpty(result.ChangeTypes);
            Assert.AreEqual(new Vector2Int(1, 1), playerUnit.Position);
        }

        [Test]
        public void TryMoveCurrentCombatUnit_AfterAdjustment_UsesFixedActionStartRange()
        {
            var playerUnit = CreateCombatUnit(
                1, new Vector2Int(1, 1), moveRange: 2);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(4, 4));
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var firstMove = session.TryMoveCurrentCombatUnit(
                new Vector2Int(3, 1));
            var outOfOriginalRange = session.TryMoveCurrentCombatUnit(
                new Vector2Int(4, 1));
            var secondAdjustment = session.TryMoveCurrentCombatUnit(
                new Vector2Int(1, 2));

            Assert.IsTrue(firstMove.IsSuccess);
            Assert.IsFalse(outOfOriginalRange.IsSuccess);
            Assert.IsTrue(secondAdjustment.IsSuccess);
            Assert.AreEqual(new Vector2Int(1, 1),
                session.CurrentActionStartPosition);
            Assert.AreEqual(new Vector2Int(1, 2), playerUnit.Position);
        }

        [Test]
        public void TrySelectCurrentFactionCombatUnit_RollsBackPreviousTentativeMove()
        {
            var firstPlayer = CreateCombatUnit(
                1, new Vector2Int(1, 1), moveRange: 2);
            var secondPlayer = CreateCombatUnit(
                2, new Vector2Int(1, 3));
            var enemyUnit = CreateCombatUnit(3, new Vector2Int(4, 4));
            var session = CreateBattleSession(
                new[] { firstPlayer, secondPlayer },
                new[] { enemyUnit });
            session.TryMoveCurrentCombatUnit(new Vector2Int(2, 1));

            var result =
                session.TrySelectCurrentFactionCombatUnit(secondPlayer);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(BattleActionType.SelectUnit, result.ActionType);
            Assert.AreSame(firstPlayer, result.ActorUnit);
            Assert.AreSame(secondPlayer, result.NextCombatUnit);
            Assert.AreEqual(new Vector2Int(2, 1), result.PositionBefore);
            Assert.AreEqual(new Vector2Int(1, 1), result.PositionAfter);
            CollectionAssert.Contains(
                result.ChangeTypes, BattleActionChangeType.UnitPositionReset);
            CollectionAssert.Contains(
                result.ChangeTypes, BattleActionChangeType.CurrentUnitChanged);
            Assert.AreEqual(new Vector2Int(1, 1), firstPlayer.Position);
            Assert.AreSame(secondPlayer, session.CurrentCombatUnit);
            Assert.AreEqual(new Vector2Int(1, 3),
                session.CurrentActionStartPosition);
            Assert.AreEqual(BattleActionType.SelectUnit,
                session.ActionLogs[1].ActionType);
            Assert.AreEqual(secondPlayer.UnitId,
                session.ActionLogs[1].NextUnitId);
        }

        [Test]
        public void TrySelectCurrentFactionCombatUnit_RejectsAlreadyActedUnit()
        {
            var firstPlayer = CreateCombatUnit(
                1, new Vector2Int(1, 1));
            var secondPlayer = CreateCombatUnit(
                2, new Vector2Int(1, 3));
            var enemyUnit = CreateCombatUnit(3, new Vector2Int(4, 4));
            var session = CreateBattleSession(
                new[] { firstPlayer, secondPlayer },
                new[] { enemyUnit });
            session.TrySelectCurrentFactionCombatUnit(secondPlayer);
            session.EndCurrentUnitAction();

            var result =
                session.TrySelectCurrentFactionCombatUnit(secondPlayer);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(
                BattleActionFailureReason.InvalidUnitSelection,
                result.FailureReason);
            Assert.AreSame(firstPlayer, session.CurrentCombatUnit);
        }

        [Test]
        public void GetCurrentCombatUnitReachablePositions_ReturnsOccupancyAwarePositions()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(0, 1), moveRange: 4);
            var blockingUnit = CreateCombatUnit(2, new Vector2Int(1, 1));
            var session = CreateBattleSession(playerUnit, blockingUnit);

            var positions = session.GetCurrentCombatUnitReachablePositions();

            CollectionAssert.Contains(positions, playerUnit.Position);
            CollectionAssert.DoesNotContain(positions, blockingUnit.Position);
            CollectionAssert.Contains(positions, new Vector2Int(2, 1));
        }

        [Test]
        public void GetCurrentCombatUnitBasicAttackPositions_ReturnsConfiguredRangeWithinGrid()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(0, 0),
                minAttackRange: 2, maxAttackRange: 3);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(4, 4));
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var positions = session.GetCurrentCombatUnitBasicAttackPositions();

            Assert.AreEqual(7, positions.Count);
            CollectionAssert.DoesNotContain(positions, new Vector2Int(1, 0));
            CollectionAssert.Contains(positions, new Vector2Int(2, 0));
            CollectionAssert.Contains(positions, new Vector2Int(1, 2));
            CollectionAssert.Contains(positions, new Vector2Int(3, 0));
            CollectionAssert.DoesNotContain(positions, new Vector2Int(4, 0));
        }

        [Test]
        public void GetCurrentCombatUnitBasicAttackTargets_FiltersFactionDeathAndRange()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(0, 0),
                minAttackRange: 2, maxAttackRange: 3);
            var alliedUnit = CreateCombatUnit(2, new Vector2Int(2, 0));
            var nearEnemy = CreateCombatUnit(3, new Vector2Int(1, 0));
            var validEnemy = CreateCombatUnit(4, new Vector2Int(1, 1));
            var deadEnemy = CreateCombatUnit(5, new Vector2Int(0, 2));
            var farEnemy = CreateCombatUnit(6, new Vector2Int(4, 0));
            var neutralUnit = CreateCombatUnit(7, new Vector2Int(0, 3));
            deadEnemy.MechRuntime.TakeDamage(deadEnemy.Mech.MaxDurability);
            var session = new BattleSession(
                5,
                5,
                new[] { playerUnit, alliedUnit },
                new[] { nearEnemy, validEnemy, deadEnemy, farEnemy },
                new[] { neutralUnit });

            var targets = session.GetCurrentCombatUnitBasicAttackTargets();

            CollectionAssert.AreEqual(new[] { validEnemy, neutralUnit }, targets);
        }

        [Test]
        public void GetCurrentCombatUnitAbilityPositions_ReturnsConfiguredRangeWithinGrid()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(0, 0));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(4, 4));
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var positions = session.GetCurrentCombatUnitAbilityPositions(
                CreateAbility(minRange: 2, maxRange: 3));

            Assert.AreEqual(7, positions.Count);
            CollectionAssert.DoesNotContain(positions, new Vector2Int(1, 0));
            CollectionAssert.Contains(positions, new Vector2Int(2, 0));
            CollectionAssert.Contains(positions, new Vector2Int(1, 2));
            CollectionAssert.Contains(positions, new Vector2Int(3, 0));
            CollectionAssert.DoesNotContain(positions, new Vector2Int(4, 0));
        }

        [Test]
        public void GetCurrentCombatUnitAbilityTargets_FiltersRuleDeathRangeAndActionPoint()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(0, 0));
            var alliedUnit = CreateCombatUnit(2, new Vector2Int(1, 0));
            var validEnemy = CreateCombatUnit(3, new Vector2Int(2, 0));
            var deadEnemy = CreateCombatUnit(4, new Vector2Int(0, 2));
            var farEnemy = CreateCombatUnit(5, new Vector2Int(4, 4));
            var neutralUnit = CreateCombatUnit(6, new Vector2Int(0, 3));
            deadEnemy.MechRuntime.TakeDamage(deadEnemy.Mech.MaxDurability);
            var session = new BattleSession(
                5,
                5,
                new[] { playerUnit, alliedUnit },
                new[] { validEnemy, deadEnemy, farEnemy },
                new[] { neutralUnit });
            var ability = CreateAbility(minRange: 2, maxRange: 3, actionPointCost: 3);

            CollectionAssert.AreEqual(
                new[] { validEnemy, neutralUnit },
                session.GetCurrentCombatUnitAbilityTargets(ability));

            playerUnit.PilotRuntime.TrySpendActionPoint(8);

            Assert.IsEmpty(session.GetCurrentCombatUnitAbilityTargets(ability));
        }

        [Test]
        public void CurrentCombatUnitQueries_DoNotMutateBattleState()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(0, 0), moveRange: 4,
                minAttackRange: 2, maxAttackRange: 3);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 0));
            var session = CreateBattleSession(playerUnit, enemyUnit);
            var jsonBefore = BattleDebugJsonSerializer.Serialize(session.CreateSnapshot(), session.ActionLogs);

            session.GetCurrentCombatUnitReachablePositions();
            session.GetCurrentCombatUnitBasicAttackPositions();
            session.GetCurrentCombatUnitBasicAttackTargets();
            session.GetCurrentCombatUnitAbilityPositions(CreateAbility());
            session.GetCurrentCombatUnitAbilityTargets(CreateAbility());

            var jsonAfter = BattleDebugJsonSerializer.Serialize(session.CreateSnapshot(), session.ActionLogs);
            Assert.AreEqual(jsonBefore, jsonAfter);
            Assert.IsEmpty(session.ActionLogs);
        }

        [Test]
        public void AttackTargetCombatUnit_WithAdjacentTarget_AppliesDamage()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1), attack: 25);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1), durability: 80);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var result = session.AttackTargetCombatUnit(enemyUnit);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(BattleActionType.Attack, result.ActionType);
            Assert.AreSame(playerUnit, result.ActorUnit);
            Assert.AreSame(enemyUnit, result.TargetUnit);
            Assert.AreEqual(80, result.DurabilityBefore);
            Assert.AreEqual(55, result.DurabilityAfter);
            CollectionAssert.AreEqual(new[] { BattleActionChangeType.DamageApplied }, result.ChangeTypes);
            Assert.AreEqual(55, enemyUnit.MechRuntime.CurrentDurability);
        }

        [Test]
        public void AttackTargetCombatUnit_OnSuccess_EndsActorActionAutomatically()
        {
            var firstPlayer = CreateCombatUnit(
                1, new Vector2Int(1, 1), attack: 25);
            var nextPlayer = CreateCombatUnit(
                2, new Vector2Int(1, 3));
            var enemyUnit = CreateCombatUnit(
                3, new Vector2Int(2, 1), durability: 80);
            var session = CreateBattleSession(
                new[] { firstPlayer, nextPlayer },
                new[] { enemyUnit });

            var result = session.AttackTargetCombatUnit(enemyUnit);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreSame(nextPlayer, session.CurrentCombatUnit);
            Assert.AreEqual(2, session.ActionLogs.Count);
            Assert.AreEqual(BattleActionType.Attack,
                session.ActionLogs[0].ActionType);
            Assert.AreEqual(BattleActionType.EndUnitAction,
                session.ActionLogs[1].ActionType);
            var snapshot = session.CreateSnapshot();
            Assert.IsTrue(snapshot.Units[0].HasActed);
            Assert.IsFalse(snapshot.Units[1].HasActed);
            Assert.AreEqual(nextPlayer.Position,
                snapshot.CurrentActionStartPosition);
        }

        [Test]
        public void AttackTargetCombatUnit_WithNonAdjacentTarget_ReturnsFailedResultWithoutApplyingDamage()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1), attack: 25);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(3, 1), durability: 80);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var result = session.AttackTargetCombatUnit(enemyUnit);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(BattleActionFailureReason.TargetOutOfRange, result.FailureReason);
            Assert.IsEmpty(result.ChangeTypes);
            Assert.AreEqual(80, enemyUnit.MechRuntime.CurrentDurability);
            Assert.AreSame(playerUnit, session.CurrentCombatUnit);
        }

        [TestCase(1, 0, false)]
        [TestCase(2, 0, true)]
        [TestCase(1, 2, true)]
        [TestCase(4, 0, false)]
        public void AttackTargetCombatUnit_UsesConfiguredMinimumAndMaximumRange(int targetX, int targetY, bool expectedSuccess)
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(0, 0), attack: 25,
                minAttackRange: 2, maxAttackRange: 3);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(targetX, targetY), durability: 80);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var result = session.AttackTargetCombatUnit(enemyUnit);

            Assert.AreEqual(expectedSuccess, result.IsSuccess);
            Assert.AreEqual(
                expectedSuccess ? BattleActionFailureReason.None : BattleActionFailureReason.TargetOutOfRange,
                result.FailureReason);
            Assert.AreEqual(expectedSuccess ? 55 : 80, enemyUnit.MechRuntime.CurrentDurability);
            Assert.AreEqual(expectedSuccess ? 1 : 0, result.ChangeTypes.Count);
        }

        [Test]
        public void AttackTargetCombatUnit_WithLethalDamage_DestroysTarget()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1), attack: 50);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1), durability: 30);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var result = session.AttackTargetCombatUnit(enemyUnit);

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(
                new[]
                {
                    BattleActionChangeType.DamageApplied,
                    BattleActionChangeType.UnitDied,
                    BattleActionChangeType.FactionEliminated,
                    BattleActionChangeType.BattleEnded
                },
                result.ChangeTypes);
            Assert.AreEqual(0, enemyUnit.MechRuntime.CurrentDurability);
            Assert.IsTrue(enemyUnit.IsDead());
            Assert.IsTrue(session.IsBattleEnded);
            Assert.AreEqual(CombatFaction.TeamA, session.Outcome.WinningFaction);
            Assert.AreEqual(CombatFaction.TeamB, session.Outcome.EliminatedFaction);
            Assert.AreEqual(BattleEndReason.FactionEliminated, session.Outcome.EndReason);
            Assert.AreSame(session.Outcome, result.Outcome);
            Assert.AreSame(session.Outcome, session.ActionLogs[0].Outcome);
        }

        [Test]
        public void AttackTargetCombatUnit_WhenAnotherEnemySurvives_DoesNotEndBattle()
        {
            var playerUnit = CreateCombatUnit(
                1, new Vector2Int(1, 1), attack: 50);
            var defeatedEnemy = CreateCombatUnit(
                2, new Vector2Int(2, 1), durability: 30);
            var survivingEnemy = CreateCombatUnit(
                3, new Vector2Int(3, 1));
            var session = CreateBattleSession(
                new[] { playerUnit },
                new[] { defeatedEnemy, survivingEnemy });

            var result = session.AttackTargetCombatUnit(defeatedEnemy);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(session.IsBattleEnded);
            Assert.AreSame(BattleOutcome.InProgress, result.Outcome);
            CollectionAssert.AreEqual(
                new[]
                {
                    BattleActionChangeType.DamageApplied,
                    BattleActionChangeType.UnitDied
                },
                result.ChangeTypes);
        }

        [Test]
        public void AttackTargetCombatUnit_WithNullTarget_Throws()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1));
            var session = CreateBattleSession(playerUnit, enemyUnit);

            Assert.Throws<ArgumentNullException>(() =>
                session.AttackTargetCombatUnit(null));
        }

        [Test]
        public void AttackTargetCombatUnit_WithUnknownTarget_Throws()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1));
            var unknownUnit = CreateCombatUnit(3, new Vector2Int(1, 2));
            var session = CreateBattleSession(playerUnit, enemyUnit);

            Assert.Throws<ArgumentException>(() =>
                session.AttackTargetCombatUnit(unknownUnit));
        }

        [Test]
        public void AttackTargetCombatUnit_WithSameFactionTarget_ReturnsFailedResultWithoutApplyingDamage()
        {
            var playerA = CreateCombatUnit(1, new Vector2Int(1, 1), attack: 25);
            var playerB = CreateCombatUnit(2, new Vector2Int(2, 1), durability: 80);
            var enemyUnit = CreateCombatUnit(3, new Vector2Int(4, 4));
            var session = CreateBattleSession(
                new[] { playerA, playerB },
                new[] { enemyUnit });

            var result = session.AttackTargetCombatUnit(playerB);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(BattleActionFailureReason.SameFactionTarget, result.FailureReason);
            Assert.AreEqual(80, playerB.MechRuntime.CurrentDurability);
        }

        [Test]
        public void AttackTargetCombatUnit_WithDeadTarget_ReturnsFailedResult()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var deadEnemy = CreateCombatUnit(2, new Vector2Int(2, 1));
            var aliveEnemy = CreateCombatUnit(3, new Vector2Int(3, 1));
            deadEnemy.MechRuntime.TakeDamage(deadEnemy.Mech.MaxDurability);
            var session = CreateBattleSession(
                new[] { playerUnit },
                new[] { deadEnemy, aliveEnemy });

            var result = session.AttackTargetCombatUnit(deadEnemy);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(BattleActionFailureReason.TargetAlreadyDead, result.FailureReason);
            Assert.AreEqual(0, deadEnemy.MechRuntime.CurrentDurability);
        }

        [Test]
        public void AttackTargetCombatUnit_WhenTeamBActs_AppliesDamageToTeamA()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1), durability: 80);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1), attack: 25);
            var session = CreateBattleSession(playerUnit, enemyUnit);
            session.EndCurrentUnitAction();

            var result = session.AttackTargetCombatUnit(playerUnit);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, result.Sequence);
            Assert.AreSame(enemyUnit, result.ActorUnit);
            Assert.AreSame(playerUnit, session.CurrentCombatUnit);
            Assert.AreEqual(2, session.CurrentRoundNumber);
            Assert.AreEqual(55, playerUnit.MechRuntime.CurrentDurability);
        }

        [Test]
        public void AttackTargetCombatUnit_WhenTeamBEliminatesTeamA_EndsBattleForTeamB()
        {
            var playerUnit = CreateCombatUnit(
                1, new Vector2Int(1, 1), durability: 20);
            var enemyUnit = CreateCombatUnit(
                2, new Vector2Int(2, 1), attack: 30);
            var session = CreateBattleSession(playerUnit, enemyUnit);
            session.EndCurrentUnitAction();

            var result = session.AttackTargetCombatUnit(playerUnit);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(session.IsBattleEnded);
            Assert.AreEqual(CombatFaction.TeamB, session.Outcome.WinningFaction);
            Assert.AreEqual(CombatFaction.TeamA, session.Outcome.EliminatedFaction);
            Assert.AreEqual(CombatFaction.TeamB, session.CurrentFaction);
            Assert.AreSame(enemyUnit, session.CurrentCombatUnit);
            Assert.AreSame(session.Outcome, result.Outcome);
        }

        [Test]
        public void AttackTargetCombatUnit_WhenNeutralIsDestroyed_DoesNotEndBattle()
        {
            var playerUnit = CreateCombatUnit(
                1, new Vector2Int(1, 1), attack: 50);
            var enemyUnit = CreateCombatUnit(
                2, new Vector2Int(4, 4));
            var neutralUnit = CreateCombatUnit(
                3, new Vector2Int(2, 1), durability: 30);
            var session = new BattleSession(
                5, 5, new[] { playerUnit }, new[] { enemyUnit },
                new[] { neutralUnit });

            var result = session.AttackTargetCombatUnit(neutralUnit);

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(neutralUnit.IsDead());
            Assert.IsFalse(session.IsBattleEnded);
            Assert.AreSame(BattleOutcome.InProgress, result.Outcome);
            CollectionAssert.DoesNotContain(
                result.ChangeTypes, BattleActionChangeType.BattleEnded);
        }

        [Test]
        public void TryUseAbility_WithValidTarget_SpendsActionPointAndRecordsResult()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1), durability: 80);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var result = session.TryUseAbility(CreateAbility(), enemyUnit);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(BattleActionType.UseAbility, result.ActionType);
            Assert.AreEqual(101, result.AbilityId);
            Assert.AreSame(playerUnit, result.ActorUnit);
            Assert.AreSame(enemyUnit, result.TargetUnit);
            Assert.AreEqual(10, result.ActionPointBefore);
            Assert.AreEqual(7, result.ActionPointAfter);
            Assert.AreEqual(80, result.DurabilityBefore);
            Assert.AreEqual(55, result.DurabilityAfter);
            Assert.AreEqual(7, playerUnit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(55, enemyUnit.MechRuntime.CurrentDurability);
            CollectionAssert.AreEqual(
                new[]
                {
                    BattleActionChangeType.ActionPointSpent,
                    BattleActionChangeType.DamageApplied
                },
                result.ChangeTypes);

            Assert.AreEqual(2, session.ActionLogs.Count);
            Assert.AreEqual(101, session.ActionLogs[0].AbilityId);
            Assert.AreEqual(enemyUnit.UnitId, session.ActionLogs[0].TargetUnitId);
            Assert.AreEqual(BattleActionType.EndUnitAction,
                session.ActionLogs[1].ActionType);
            Assert.AreSame(enemyUnit, session.CurrentCombatUnit);
        }

        [Test]
        public void TryUseAbility_WithInsufficientActionPoint_ReturnsFailureWithoutSideEffects()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1), durability: 80);
            playerUnit.PilotRuntime.TrySpendActionPoint(8);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var result = session.TryUseAbility(CreateAbility(actionPointCost: 3), enemyUnit);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(BattleActionFailureReason.InsufficientActionPoint, result.FailureReason);
            Assert.AreEqual(101, result.AbilityId);
            Assert.AreSame(enemyUnit, result.TargetUnit);
            Assert.IsEmpty(result.ChangeTypes);
            Assert.AreEqual(2, playerUnit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(80, enemyUnit.MechRuntime.CurrentDurability);
            Assert.AreEqual(BattleActionFailureReason.InsufficientActionPoint,
                session.ActionLogs[0].FailureReason);
        }

        [Test]
        public void TryUseAbility_WithOutOfRangeTarget_ReturnsFailureWithoutSpendingActionPoint()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(0, 0));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(4, 4), durability: 80);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var result = session.TryUseAbility(CreateAbility(maxRange: 3), enemyUnit);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(BattleActionFailureReason.TargetOutOfRange, result.FailureReason);
            Assert.AreEqual(10, playerUnit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(80, enemyUnit.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryUseAbility_WithSameFactionTarget_ReturnsFailureWithoutSpendingActionPoint()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var alliedUnit = CreateCombatUnit(2, new Vector2Int(2, 1), durability: 80);
            var enemyUnit = CreateCombatUnit(3, new Vector2Int(4, 4));
            var session = CreateBattleSession(
                new[] { playerUnit, alliedUnit },
                new[] { enemyUnit });

            var result = session.TryUseAbility(CreateAbility(), alliedUnit);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(BattleActionFailureReason.SameFactionTarget, result.FailureReason);
            Assert.AreEqual(10, playerUnit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(80, alliedUnit.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryUseAbility_WithDeadTarget_ReturnsFailureWithoutSpendingActionPoint()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var deadEnemy = CreateCombatUnit(2, new Vector2Int(2, 1));
            var aliveEnemy = CreateCombatUnit(3, new Vector2Int(3, 1));
            deadEnemy.MechRuntime.TakeDamage(deadEnemy.Mech.MaxDurability);
            var session = CreateBattleSession(
                new[] { playerUnit },
                new[] { deadEnemy, aliveEnemy });

            var result = session.TryUseAbility(CreateAbility(), deadEnemy);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(BattleActionFailureReason.TargetAlreadyDead, result.FailureReason);
            Assert.AreEqual(10, playerUnit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(0, deadEnemy.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryUseAbility_WithLethalDamage_RecordsUnitDeath()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1), durability: 20);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var result = session.TryUseAbility(CreateAbility(damage: 30), enemyUnit);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, enemyUnit.MechRuntime.CurrentDurability);
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
            Assert.IsTrue(session.IsBattleEnded);
            Assert.AreEqual(CombatFaction.TeamA, session.Outcome.WinningFaction);
            Assert.AreSame(session.Outcome, result.Outcome);
        }

        [Test]
        public void TryUseAbility_WhenTeamBActs_UsesCurrentTeamBUnit()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1), durability: 80);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1));
            var session = CreateBattleSession(playerUnit, enemyUnit);
            session.EndCurrentUnitAction();

            var result = session.TryUseAbility(CreateAbility(), playerUnit);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreSame(enemyUnit, result.ActorUnit);
            Assert.AreEqual(7, enemyUnit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(55, playerUnit.MechRuntime.CurrentDurability);
        }

        [Test]
        public void TryUseAbility_WithInvalidContractArgument_DoesNotRecordAction()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1));
            var unknownUnit = CreateCombatUnit(3, new Vector2Int(3, 1));
            var session = CreateBattleSession(playerUnit, enemyUnit);

            Assert.Throws<ArgumentNullException>(() => session.TryUseAbility(null, enemyUnit));
            Assert.Throws<ArgumentNullException>(() => session.TryUseAbility(CreateAbility(), null));
            Assert.Throws<ArgumentException>(() => session.TryUseAbility(CreateAbility(), unknownUnit));
            Assert.IsEmpty(session.ActionLogs);

            var moveResult = session.TryMoveCurrentCombatUnit(new Vector2Int(1, 2));
            Assert.AreEqual(1, moveResult.Sequence);
        }

        [Test]
        public void EndCurrentUnitAction_AdvancesUnitsTurnsAndRounds()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(3, 1));
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var firstResult = session.EndCurrentUnitAction();

            Assert.IsTrue(firstResult.IsSuccess);
            Assert.AreEqual(1, firstResult.Sequence);
            Assert.AreSame(playerUnit, firstResult.ActorUnit);
            Assert.AreSame(enemyUnit, firstResult.NextCombatUnit);
            CollectionAssert.AreEqual(
                new[]
                {
                    BattleActionChangeType.UnitActionEnded,
                    BattleActionChangeType.FactionTurnEnded,
                    BattleActionChangeType.FactionTurnStarted,
                    BattleActionChangeType.CurrentUnitChanged
                },
                firstResult.ChangeTypes);
            Assert.AreSame(enemyUnit, session.CurrentCombatUnit);
            Assert.AreEqual(CombatFaction.TeamB, session.CurrentFaction);
            Assert.AreEqual(1, session.CurrentRoundNumber);

            var secondResult = session.EndCurrentUnitAction();

            Assert.IsTrue(secondResult.IsSuccess);
            Assert.AreEqual(2, secondResult.Sequence);
            Assert.AreSame(enemyUnit, secondResult.ActorUnit);
            Assert.AreSame(playerUnit, secondResult.NextCombatUnit);
            CollectionAssert.AreEqual(
                new[]
                {
                    BattleActionChangeType.UnitActionEnded,
                    BattleActionChangeType.FactionTurnEnded,
                    BattleActionChangeType.RoundStarted,
                    BattleActionChangeType.FactionTurnStarted,
                    BattleActionChangeType.CurrentUnitChanged
                },
                secondResult.ChangeTypes);
            Assert.AreSame(playerUnit, session.CurrentCombatUnit);
            Assert.AreEqual(CombatFaction.TeamA, session.CurrentFaction);
            Assert.AreEqual(2, session.CurrentRoundNumber);
        }

        [Test]
        public void EndCurrentUnitAction_RestoresNextUnitActionPointInResultAndLog()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(3, 1));
            enemyUnit.PilotRuntime.TrySpendActionPoint(7);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            var result = session.EndCurrentUnitAction();

            Assert.AreEqual(3, result.ActionPointBefore);
            Assert.AreEqual(10, result.ActionPointAfter);
            CollectionAssert.Contains(
                result.ChangeTypes,
                BattleActionChangeType.ActionPointRestored);
            Assert.AreEqual(10, enemyUnit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(3, session.ActionLogs[0].ActionPointBefore);
            Assert.AreEqual(10, session.ActionLogs[0].ActionPointAfter);
        }

        [Test]
        public void EndCurrentFactionTurn_SkipsRemainingUnitsAndRecordsResult()
        {
            var playerA = CreateCombatUnit(1, new Vector2Int(1, 1));
            var playerB = CreateCombatUnit(2, new Vector2Int(1, 2));
            var enemyUnit = CreateCombatUnit(3, new Vector2Int(3, 1));
            playerB.PilotRuntime.TrySpendActionPoint(9);
            enemyUnit.PilotRuntime.TrySpendActionPoint(7);
            var session = CreateBattleSession(
                new[] { playerA, playerB },
                new[] { enemyUnit });

            var result = session.EndCurrentFactionTurn();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(BattleActionType.EndFactionTurn, result.ActionType);
            Assert.AreSame(playerA, result.ActorUnit);
            Assert.AreSame(enemyUnit, result.NextCombatUnit);
            Assert.AreEqual(CombatFaction.TeamA, result.FactionBefore);
            Assert.AreEqual(CombatFaction.TeamB, result.FactionAfter);
            Assert.AreEqual(1, result.RoundNumberBefore);
            Assert.AreEqual(1, result.RoundNumberAfter);
            Assert.AreEqual(3, result.ActionPointBefore);
            Assert.AreEqual(10, result.ActionPointAfter);
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
            Assert.AreSame(enemyUnit, session.CurrentCombatUnit);
            Assert.AreEqual(1, playerB.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(BattleActionType.EndFactionTurn, session.ActionLogs[0].ActionType);
            Assert.AreEqual(enemyUnit.UnitId, session.ActionLogs[0].NextUnitId);
        }

        [Test]
        public void EndCurrentFactionTurn_RollsBackTentativeMoveBeforeAdvancing()
        {
            var playerUnit = CreateCombatUnit(
                1, new Vector2Int(1, 1), moveRange: 2);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(4, 4));
            var session = CreateBattleSession(playerUnit, enemyUnit);
            session.TryMoveCurrentCombatUnit(new Vector2Int(2, 1));

            var result = session.EndCurrentFactionTurn();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(new Vector2Int(2, 1), result.PositionBefore);
            Assert.AreEqual(new Vector2Int(1, 1), result.PositionAfter);
            CollectionAssert.AreEqual(
                new[]
                {
                    BattleActionChangeType.UnitPositionReset,
                    BattleActionChangeType.UnitActionEnded,
                    BattleActionChangeType.FactionTurnEnded,
                    BattleActionChangeType.FactionTurnStarted,
                    BattleActionChangeType.CurrentUnitChanged
                },
                result.ChangeTypes);
            Assert.AreEqual(new Vector2Int(1, 1), playerUnit.Position);
            Assert.AreEqual(new Vector2Int(2, 1),
                session.ActionLogs[1].PositionBefore);
            Assert.AreEqual(new Vector2Int(1, 1),
                session.ActionLogs[1].PositionAfter);
        }

        [Test]
        public void BattleEnded_RejectsStateChangingActionsWithoutGameplaySideEffects()
        {
            var playerUnit = CreateCombatUnit(
                1, new Vector2Int(1, 1), attack: 50);
            var enemyUnit = CreateCombatUnit(
                2, new Vector2Int(2, 1), durability: 30);
            var session = CreateBattleSession(playerUnit, enemyUnit);
            session.AttackTargetCombatUnit(enemyUnit);
            var positionBefore = playerUnit.Position;
            var actionPointBefore = playerUnit.PilotRuntime.CurrentActionPoint;
            var roundBefore = session.CurrentRoundNumber;
            var factionBefore = session.CurrentFaction;

            var moveResult = session.TryMoveCurrentCombatUnit(new Vector2Int(1, 2));
            var attackResult = session.AttackTargetCombatUnit(enemyUnit);
            var abilityResult = session.TryUseAbility(CreateAbility(), enemyUnit);
            var endActionResult = session.EndCurrentUnitAction();
            var endFactionResult = session.EndCurrentFactionTurn();

            AssertBattleAlreadyEnded(moveResult, session.Outcome);
            AssertBattleAlreadyEnded(attackResult, session.Outcome);
            AssertBattleAlreadyEnded(abilityResult, session.Outcome);
            AssertBattleAlreadyEnded(endActionResult, session.Outcome);
            AssertBattleAlreadyEnded(endFactionResult, session.Outcome);
            Assert.AreEqual(positionBefore, playerUnit.Position);
            Assert.AreEqual(actionPointBefore, playerUnit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(0, enemyUnit.MechRuntime.CurrentDurability);
            Assert.AreEqual(roundBefore, session.CurrentRoundNumber);
            Assert.AreEqual(factionBefore, session.CurrentFaction);
            Assert.AreSame(playerUnit, session.CurrentCombatUnit);
            Assert.AreEqual(6, session.ActionLogs.Count);
            Assert.AreEqual(2, moveResult.Sequence);
            Assert.AreEqual(6, endFactionResult.Sequence);
            Assert.IsNull(endActionResult.NextCombatUnit);
            Assert.IsNull(endFactionResult.NextCombatUnit);
        }

        [Test]
        public void BattleEnded_CurrentActionQueriesReturnEmptyAndSnapshotKeepsOutcome()
        {
            var playerUnit = CreateCombatUnit(
                1, new Vector2Int(1, 1), attack: 50);
            var enemyUnit = CreateCombatUnit(
                2, new Vector2Int(2, 1), durability: 30);
            var session = CreateBattleSession(playerUnit, enemyUnit);
            session.AttackTargetCombatUnit(enemyUnit);

            Assert.IsEmpty(session.GetCurrentCombatUnitReachablePositions());
            Assert.IsEmpty(session.GetCurrentCombatUnitBasicAttackPositions());
            Assert.IsEmpty(session.GetCurrentCombatUnitBasicAttackTargets());
            Assert.IsEmpty(session.GetCurrentCombatUnitAbilityPositions(CreateAbility()));
            Assert.IsEmpty(session.GetCurrentCombatUnitAbilityTargets(CreateAbility()));

            var snapshot = session.CreateSnapshot();
            Assert.AreSame(session.Outcome, snapshot.Outcome);
            Assert.IsTrue(snapshot.Outcome.IsBattleEnded);
            Assert.AreEqual(playerUnit.UnitId, snapshot.CurrentUnitId);
        }

        [Test]
        public void ActionLogs_RecordSuccessfulAndFailedActionsInSequence()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1), moveRange: 1);
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(4, 4));
            var session = CreateBattleSession(playerUnit, enemyUnit);

            session.TryMoveCurrentCombatUnit(new Vector2Int(3, 1));
            session.TryMoveCurrentCombatUnit(new Vector2Int(2, 1));
            session.AttackTargetCombatUnit(enemyUnit);
            session.EndCurrentUnitAction();

            Assert.AreEqual(4, session.ActionLogs.Count);

            var failedMoveLog = session.ActionLogs[0];
            Assert.AreEqual(1, failedMoveLog.Sequence);
            Assert.AreEqual(BattleActionType.Move, failedMoveLog.ActionType);
            Assert.IsFalse(failedMoveLog.IsSuccess);
            Assert.AreEqual(BattleActionFailureReason.InvalidMoveTarget, failedMoveLog.FailureReason);
            Assert.AreEqual(1, failedMoveLog.ActorUnitId);

            var successfulMoveLog = session.ActionLogs[1];
            Assert.AreEqual(2, successfulMoveLog.Sequence);
            Assert.IsTrue(successfulMoveLog.IsSuccess);
            Assert.AreEqual(new Vector2Int(1, 1), successfulMoveLog.PositionBefore);
            Assert.AreEqual(new Vector2Int(2, 1), successfulMoveLog.PositionAfter);

            var failedAttackLog = session.ActionLogs[2];
            Assert.AreEqual(3, failedAttackLog.Sequence);
            Assert.AreEqual(BattleActionType.Attack, failedAttackLog.ActionType);
            Assert.AreEqual(BattleActionFailureReason.TargetOutOfRange, failedAttackLog.FailureReason);
            Assert.AreEqual(2, failedAttackLog.TargetUnitId);

            var endActionLog = session.ActionLogs[3];
            Assert.AreEqual(4, endActionLog.Sequence);
            Assert.AreEqual(BattleActionType.EndUnitAction, endActionLog.ActionType);
            Assert.AreEqual(1, endActionLog.ActorUnitId);
            Assert.AreEqual(2, endActionLog.NextUnitId);
            Assert.AreEqual(CombatFaction.TeamB, endActionLog.FactionAfter);
        }

        [Test]
        public void GetCurrentFactionCombatUnits_ReturnsUnitsForCurrentFaction()
        {
            var playerA = CreateCombatUnit(1, new Vector2Int(1, 1));
            var playerB = CreateCombatUnit(2, new Vector2Int(1, 2));
            var enemyUnit = CreateCombatUnit(3, new Vector2Int(3, 1));
            var session = CreateBattleSession(
                new[] { playerA, playerB },
                new[] { enemyUnit });

            var playerUnits = session.GetCurrentFactionCombatUnits();

            CollectionAssert.AreEqual(new[] { playerA, playerB }, playerUnits);
            CollectionAssert.AreEqual(
                new[] { enemyUnit },
                session.GetFactionCombatUnits(CombatFaction.TeamB));

            session.EndCurrentUnitAction();
            session.EndCurrentUnitAction();

            CollectionAssert.AreEqual(
                new[] { enemyUnit },
                session.GetCurrentFactionCombatUnits());
        }

        [Test]
        public void AliveAndDeadQueries_ReturnExpectedUnits()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(3, 1));
            enemyUnit.MechRuntime.TakeDamage(enemyUnit.Mech.MaxDurability);
            var session = CreateBattleSession(playerUnit, enemyUnit);

            CollectionAssert.AreEqual(
                new[] { playerUnit },
                session.GetAllAliveCombatUnits());
            CollectionAssert.AreEqual(
                new[] { enemyUnit },
                session.GetAllDeadCombatUnits());
            Assert.IsFalse(session.AreFactionUnitsDead(CombatFaction.TeamA));
            Assert.IsTrue(session.AreFactionUnitsDead(CombatFaction.TeamB));
        }

        [Test]
        public void FactionQueries_IncludeNeutralUnits()
        {
            var teamAUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var teamBUnit = CreateCombatUnit(2, new Vector2Int(3, 1));
            var neutralUnit = CreateCombatUnit(3, new Vector2Int(2, 3));
            var session = new BattleSession(
                5,
                5,
                new[] { teamAUnit },
                new[] { teamBUnit },
                new[] { neutralUnit });

            Assert.AreEqual(3, session.CombatUnits.Count);
            CollectionAssert.AreEqual(
                new[] { neutralUnit },
                session.GetFactionCombatUnits(CombatFaction.Neutral));
            Assert.AreEqual(
                CombatFaction.Neutral,
                session.GetCombatUnitFaction(neutralUnit));
            Assert.IsFalse(session.AreFactionUnitsDead(CombatFaction.Neutral));

            neutralUnit.MechRuntime.TakeDamage(neutralUnit.Mech.MaxDurability);

            Assert.IsTrue(session.AreFactionUnitsDead(CombatFaction.Neutral));
        }

        [Test]
        public void CombatUnitRegistry_ProvidesFactionAndMembershipQueries()
        {
            var teamAUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var teamBUnit = CreateCombatUnit(2, new Vector2Int(3, 1));
            var neutralUnit = CreateCombatUnit(3, new Vector2Int(2, 3));
            var registry = new CombatUnitRegistry(
                new[] { teamAUnit },
                new[] { teamBUnit },
                new[] { neutralUnit });

            Assert.IsTrue(registry.Contains(teamAUnit));
            Assert.AreEqual(
                CombatFaction.TeamA,
                registry.GetFaction(teamAUnit));
            Assert.AreEqual(
                CombatFaction.TeamB,
                registry.GetFaction(teamBUnit));
            Assert.AreEqual(
                CombatFaction.Neutral,
                registry.GetFaction(neutralUnit));
            Assert.AreSame(teamBUnit, registry.GetCombatUnit(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => registry.GetCombatUnit(0));
            Assert.Throws<KeyNotFoundException>(() => registry.GetCombatUnit(99));
        }

        [Test]
        public void ConstructorAndTurnAdvance_SkipDeadUnits()
        {
            var deadPlayer = CreateCombatUnit(1, new Vector2Int(1, 1));
            var alivePlayer = CreateCombatUnit(2, new Vector2Int(1, 2));
            var deadEnemy = CreateCombatUnit(3, new Vector2Int(3, 1));
            var aliveEnemy = CreateCombatUnit(4, new Vector2Int(3, 2));
            deadPlayer.MechRuntime.TakeDamage(deadPlayer.Mech.MaxDurability);
            deadEnemy.MechRuntime.TakeDamage(deadEnemy.Mech.MaxDurability);
            var session = CreateBattleSession(
                new[] { deadPlayer, alivePlayer },
                new[] { deadEnemy, aliveEnemy });

            Assert.AreSame(alivePlayer, session.CurrentCombatUnit);

            session.EndCurrentUnitAction();

            Assert.AreEqual(CombatFaction.TeamB, session.CurrentFaction);
            Assert.AreSame(aliveEnemy, session.CurrentCombatUnit);
        }

        [Test]
        public void DeadCurrentCombatUnit_CannotMoveOrAttack()
        {
            var playerUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemyUnit = CreateCombatUnit(2, new Vector2Int(2, 1));
            var session = CreateBattleSession(playerUnit, enemyUnit);
            playerUnit.MechRuntime.TakeDamage(playerUnit.Mech.MaxDurability);

            Assert.Throws<InvalidOperationException>(() =>
                session.TryMoveCurrentCombatUnit(new Vector2Int(1, 2)));
            Assert.Throws<InvalidOperationException>(() =>
                session.AttackTargetCombatUnit(enemyUnit));
        }

        private static void AssertBattleAlreadyEnded(
            BattleActionResult result, BattleOutcome expectedOutcome)
        {
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(
                BattleActionFailureReason.BattleAlreadyEnded,
                result.FailureReason);
            Assert.AreSame(expectedOutcome, result.Outcome);
            Assert.IsEmpty(result.ChangeTypes);
        }

        private static BattleSession CreateBattleSession(
            CombatUnit playerUnit,
            CombatUnit enemyUnit)
        {
            return CreateBattleSession(
                new[] { playerUnit },
                new[] { enemyUnit });
        }

        private static BattleSession CreateBattleSession(
            CombatUnit[] playerUnits,
            CombatUnit[] enemyUnits)
        {
            return new BattleSession(5, 5, playerUnits, enemyUnits);
        }

        private static AbilityDefinition CreateAbility(int damage = 25, int minRange = 1,
            int maxRange = 3, int actionPointCost = 3)
        {
            return new AbilityDefinition(101, "Test Ability", minRange, maxRange, damage,
                actionPointCost, TargetRule.EnemyUnit);
        }

        private static CombatUnit CreateCombatUnit(
            int id,
            Vector2Int position,
            int attack = 10,
            int durability = 100,
            int moveRange = 3,
            int minAttackRange = 1,
            int maxAttackRange = 1)
        {
            var pilot = new PilotData(id, $"Pilot {id}", 10);
            var basicAttack = new BasicAttackData(attack, minAttackRange, maxAttackRange);
            var mech = new MechData(id, $"Mech {id}", basicAttack, durability, moveRange);
            var factory = new CombatUnitFactory();

            return factory.Create(id, pilot, mech, position);
        }
    }
}
