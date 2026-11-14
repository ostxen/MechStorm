using System;
using MechStorm.Battle.Data;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Units;
using NUnit.Framework;

namespace MechStorm.Battle.Tests.Spatial
{
    public class BattleOccupancyTests
    {
        [Test]
        public void PositionQueries_ReturnAliveUnitsAcrossFactionsAndEmptyForVacantCell()
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

            Assert.IsTrue(session.TryGetAliveCombatUnitAt(teamAUnit.Position, out var foundTeamAUnit));
            Assert.AreSame(teamAUnit, foundTeamAUnit);
            Assert.IsTrue(session.IsPositionOccupied(teamAUnit.Position));

            Assert.IsTrue(session.TryGetAliveCombatUnitAt(teamBUnit.Position, out var foundTeamBUnit));
            Assert.AreSame(teamBUnit, foundTeamBUnit);

            Assert.IsTrue(session.TryGetAliveCombatUnitAt(neutralUnit.Position, out var foundNeutralUnit));
            Assert.AreSame(neutralUnit, foundNeutralUnit);

            var vacantPosition = new Vector2Int(4, 4);
            Assert.IsFalse(session.TryGetAliveCombatUnitAt(vacantPosition, out var vacantUnit));
            Assert.IsNull(vacantUnit);
            Assert.IsFalse(session.IsPositionOccupied(vacantPosition));
        }

        [Test]
        public void Constructor_WithOverlappingAliveUnits_Throws()
        {
            var sharedPosition = new Vector2Int(1, 1);
            var teamAUnit = CreateCombatUnit(1, sharedPosition);
            var teamBUnit = CreateCombatUnit(2, sharedPosition);

            Assert.Throws<ArgumentException>(() =>
                CreateBattleSession(teamAUnit, teamBUnit));
        }

        [Test]
        public void PositionQueries_ReleasePosition_WhenUnitDies()
        {
            var teamAUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var teamBUnit = CreateCombatUnit(2, new Vector2Int(3, 1));
            var session = CreateBattleSession(teamAUnit, teamBUnit);

            Assert.IsTrue(session.IsPositionOccupied(teamBUnit.Position));

            teamBUnit.MechRuntime.TakeDamage(teamBUnit.Mech.MaxDurability);

            Assert.IsFalse(session.TryGetAliveCombatUnitAt(teamBUnit.Position, out var combatUnit));
            Assert.IsNull(combatUnit);
            Assert.IsFalse(session.IsPositionOccupied(teamBUnit.Position));
        }

        [Test]
        public void Constructor_AllowsDeadUnitToSharePositionWithAliveUnit()
        {
            var sharedPosition = new Vector2Int(1, 1);
            var teamAUnit = CreateCombatUnit(1, sharedPosition);
            var deadTeamBUnit = CreateCombatUnit(2, sharedPosition);
            deadTeamBUnit.MechRuntime.TakeDamage(deadTeamBUnit.Mech.MaxDurability);

            var session = CreateBattleSession(teamAUnit, deadTeamBUnit);

            Assert.IsTrue(session.TryGetAliveCombatUnitAt(sharedPosition, out var combatUnit));
            Assert.AreSame(teamAUnit, combatUnit);
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(5, 0)]
        [TestCase(0, 5)]
        public void PositionQueries_WithPositionOutsideGrid_Throw(int x, int y)
        {
            var teamAUnit = CreateCombatUnit(1, new Vector2Int(1, 1));
            var teamBUnit = CreateCombatUnit(2, new Vector2Int(3, 1));
            var session = CreateBattleSession(teamAUnit, teamBUnit);
            var outsidePosition = new Vector2Int(x, y);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.TryGetAliveCombatUnitAt(outsidePosition, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.IsPositionOccupied(outsidePosition));
        }

        [TestCase(CombatFaction.TeamA)]
        [TestCase(CombatFaction.TeamB)]
        [TestCase(CombatFaction.Neutral)]
        public void TryMoveCurrentCombatUnit_RejectsOccupiedTargetAcrossFactions(CombatFaction blockingFaction)
        {
            var actor = CreateCombatUnit(1, new Vector2Int(0, 1), moveRange: 1);
            var blocker = CreateCombatUnit(2, new Vector2Int(1, 1));
            var teamAUnits = blockingFaction == CombatFaction.TeamA
                ? new[] { actor, blocker }
                : new[] { actor };
            var teamBUnits = blockingFaction == CombatFaction.TeamB
                ? new[] { blocker }
                : new[] { CreateCombatUnit(3, new Vector2Int(4, 4)) };
            var neutralUnits = blockingFaction == CombatFaction.Neutral
                ? new[] { blocker }
                : null;
            var session = new BattleSession(5, 5, teamAUnits, teamBUnits, neutralUnits);

            var result = session.TryMoveCurrentCombatUnit(blocker.Position);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(new Vector2Int(0, 1), actor.Position);
            Assert.AreEqual(1, session.ActionLogs.Count);
            Assert.IsTrue(session.TryGetAliveCombatUnitAt(actor.Position, out var unitAtStart));
            Assert.AreSame(actor, unitAtStart);
            Assert.IsTrue(session.TryGetAliveCombatUnitAt(blocker.Position, out var unitAtTarget));
            Assert.AreSame(blocker, unitAtTarget);
        }

        [Test]
        public void TryMoveCurrentCombatUnit_CannotPassThroughOccupiedPosition()
        {
            var actor = CreateCombatUnit(1, new Vector2Int(0, 1), moveRange: 2);
            var blocker = CreateCombatUnit(2, new Vector2Int(1, 1));
            var session = new BattleSession(5, 5, new[] { actor }, new[] { blocker });

            var result = session.TryMoveCurrentCombatUnit(new Vector2Int(2, 1));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(new Vector2Int(0, 1), actor.Position);
            Assert.AreEqual(1, session.ActionLogs.Count);
        }

        [Test]
        public void TryMoveCurrentCombatUnit_CanDetourAroundOccupiedPosition()
        {
            var actor = CreateCombatUnit(1, new Vector2Int(0, 1), moveRange: 4);
            var blocker = CreateCombatUnit(2, new Vector2Int(1, 1));
            var target = new Vector2Int(2, 1);
            var session = new BattleSession(5, 5, new[] { actor }, new[] { blocker });

            var result = session.TryMoveCurrentCombatUnit(target);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(target, actor.Position);
        }

        [Test]
        public void TryMoveCurrentCombatUnit_AllowsPositionOfDeadUnit()
        {
            var actor = CreateCombatUnit(1, new Vector2Int(0, 1), moveRange: 1);
            var deadUnit = CreateCombatUnit(2, new Vector2Int(1, 1));
            deadUnit.MechRuntime.TakeDamage(deadUnit.Mech.MaxDurability);
            var session = new BattleSession(5, 5, new[] { actor }, new[] { deadUnit });

            var result = session.TryMoveCurrentCombatUnit(deadUnit.Position);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(deadUnit.Position, actor.Position);
        }

        [Test]
        public void TryMoveCurrentCombatUnit_AllowsCurrentUnitStartPosition()
        {
            var actor = CreateCombatUnit(1, new Vector2Int(1, 1));
            var enemy = CreateCombatUnit(2, new Vector2Int(4, 4));
            var session = new BattleSession(5, 5, new[] { actor }, new[] { enemy });

            var result = session.TryMoveCurrentCombatUnit(actor.Position);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(new Vector2Int(1, 1), actor.Position);
        }

        private static BattleSession CreateBattleSession(CombatUnit teamAUnit, CombatUnit teamBUnit)
        {
            return new BattleSession(5, 5, new[] { teamAUnit }, new[] { teamBUnit });
        }

        private static CombatUnit CreateCombatUnit(int unitId, Vector2Int position, int moveRange = 3)
        {
            var pilot = new PilotData(unitId, $"Pilot {unitId}", 10);
            var mech = new MechData(unitId, $"Mech {unitId}", new BasicAttackData(10, 1, 1), 100, moveRange);
            return new CombatUnitFactory().Create(unitId, pilot, mech, position);
        }
    }
}
