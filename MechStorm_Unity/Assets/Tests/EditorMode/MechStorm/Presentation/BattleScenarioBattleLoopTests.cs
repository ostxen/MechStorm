using System.Linq;
using MechStorm.Battle.Actions;
using MechStorm.Battle.Outcomes;
using MechStorm.Battle.Units;
using MechStorm.Config;
using NUnit.Framework;
using UnityEngine;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Tests
{
    public sealed class BattleScenarioBattleLoopTests
    {
        private BattleScenarioConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = BattleScenarioTestConfigFactory.CreateValid();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void FixedTwoByTwoScenario_CompletesAndRebuildsFromOriginalConfig()
        {
            var builder = new BattleScenarioBuilder();
            Assert.IsTrue(builder.TryBuild(_config, out var firstBuild, out _));

            CompleteBattle(firstBuild);

            var completedSession = firstBuild.BattleSession;
            Assert.IsTrue(completedSession.IsBattleEnded);
            Assert.AreEqual(6, completedSession.CurrentRoundNumber);
            Assert.AreEqual(CombatFaction.TeamA, completedSession.Outcome.WinningFaction);
            Assert.AreEqual(CombatFaction.TeamB, completedSession.Outcome.EliminatedFaction);
            Assert.AreEqual(BattleEndReason.FactionEliminated, completedSession.Outcome.EndReason);
            Assert.IsTrue(completedSession.ActionLogs.Any(log => log.ActionType == BattleActionType.Move));
            Assert.IsTrue(completedSession.ActionLogs.Any(log => log.ActionType == BattleActionType.Attack));
            Assert.IsTrue(completedSession.ActionLogs.Any(log => log.ActionType == BattleActionType.UseAbility));

            Assert.IsTrue(builder.TryBuild(_config, out var rebuilt, out _));

            AssertFreshScenario(rebuilt, completedSession);
        }

        private static void CompleteBattle(BattleScenarioBuildResult build)
        {
            MoveAndWait(build, 1, new Vector2Int(4, 2));
            MoveAndWait(build, 2, new Vector2Int(5, 3));
            MoveAndWait(build, 3, new Vector2Int(7, 5));
            MoveAndWait(build, 4, new Vector2Int(7, 7));

            Move(build, 1, new Vector2Int(6, 4));
            UseAbility(build, 1, 3);
            UseAbility(build, 2, 3);
            UseAbility(build, 3, 1);
            Move(build, 4, new Vector2Int(6, 6));
            UseAbility(build, 4, 2);

            UseAbility(build, 1, 3);
            UseAbility(build, 2, 3);
            UseAbility(build, 4, 2);

            UseBasicAttack(build, 1, 4);
            UseAbility(build, 2, 4);
            UseAbility(build, 4, 1);

            UseBasicAttack(build, 1, 4);
            UseAbility(build, 2, 4);
            UseAbility(build, 4, 1);

            UseBasicAttack(build, 1, 4);
            UseAbility(build, 2, 4);
        }

        private static void MoveAndWait(
            BattleScenarioBuildResult build, int actorUnitId, Vector2Int position)
        {
            Move(build, actorUnitId, position);
            var result = build.BattleSession.EndCurrentUnitAction();
            Assert.IsTrue(result.IsSuccess, result.FailureReason.ToString());
        }

        private static void Move(
            BattleScenarioBuildResult build, int actorUnitId, Vector2Int position)
        {
            AssertCurrentUnit(build, actorUnitId);
            var result = build.BattleSession.TryMoveCurrentCombatUnit(position);
            Assert.IsTrue(result.IsSuccess, result.FailureReason.ToString());
        }

        private static void UseBasicAttack(
            BattleScenarioBuildResult build, int actorUnitId, int targetUnitId)
        {
            AssertCurrentUnit(build, actorUnitId);
            var result = build.BattleSession.AttackTargetCombatUnit(
                GetUnit(build, targetUnitId));
            Assert.IsTrue(result.IsSuccess, result.FailureReason.ToString());
        }

        private static void UseAbility(
            BattleScenarioBuildResult build, int actorUnitId, int targetUnitId)
        {
            AssertCurrentUnit(build, actorUnitId);
            var ability = build.GetAbilitiesForUnit(actorUnitId)[0];
            var result = build.BattleSession.TryUseAbility(
                ability,
                GetUnit(build, targetUnitId));
            Assert.IsTrue(result.IsSuccess, result.FailureReason.ToString());
        }

        private static void AssertCurrentUnit(
            BattleScenarioBuildResult build, int expectedUnitId)
        {
            Assert.AreEqual(
                expectedUnitId,
                build.BattleSession.CurrentCombatUnit.UnitId);
        }

        private void AssertFreshScenario(
            BattleScenarioBuildResult rebuilt,
            MechStorm.Battle.BattleSession completedSession)
        {
            Assert.AreNotSame(completedSession, rebuilt.BattleSession);
            Assert.IsFalse(rebuilt.BattleSession.IsBattleEnded);
            Assert.AreSame(BattleOutcome.InProgress, rebuilt.BattleSession.Outcome);
            Assert.AreEqual(1, rebuilt.BattleSession.CurrentRoundNumber);
            Assert.AreEqual(CombatFaction.TeamA, rebuilt.BattleSession.CurrentFaction);
            Assert.AreEqual(1, rebuilt.BattleSession.CurrentCombatUnit.UnitId);
            Assert.IsEmpty(rebuilt.BattleSession.ActionLogs);

            foreach (var unitConfig in _config.Units)
            {
                var unit = GetUnit(rebuilt, unitConfig.UnitId);
                Assert.AreEqual(
                    new Vector2Int(
                        unitConfig.InitialPosition.x,
                        unitConfig.InitialPosition.y),
                    unit.Position);
                Assert.AreEqual(
                    unitConfig.Mech.MaxDurability,
                    unit.MechRuntime.CurrentDurability);
                Assert.AreEqual(
                    unitConfig.Pilot.MaxActionPoint,
                    unit.PilotRuntime.CurrentActionPoint);
                Assert.IsFalse(unit.IsDead());
            }
        }

        private static CombatUnit GetUnit(
            BattleScenarioBuildResult build, int unitId)
        {
            return build.Units.Single(unit => unit.UnitId == unitId);
        }
    }
}
