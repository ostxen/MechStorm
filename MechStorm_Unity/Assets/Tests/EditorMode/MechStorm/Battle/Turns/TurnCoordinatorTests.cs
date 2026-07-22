using System;
using MechStorm.Battle.Data;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Turns;
using MechStorm.Battle.Units;
using NUnit.Framework;

namespace MechStorm.Battle.Tests.Turns
{
    public class TurnCoordinatorTests
    {
        [Test]
        public void Constructor_StartsFromTeamAAndRoundOne()
        {
            var teamAUnit = CreateCombatUnit(1);
            var teamBUnit = CreateCombatUnit(2);
            var coordinator = CreateTurnCoordinator(
                new[] { teamAUnit },
                new[] { teamBUnit });

            Assert.AreEqual(CombatFaction.TeamA, coordinator.CurrentFaction);
            Assert.AreSame(teamAUnit, coordinator.CurrentCombatUnit);
            Assert.AreEqual(1, coordinator.CurrentRoundNumber);
        }

        [Test]
        public void EndCurrentUnitAction_AdvancesToNextAliveFactionUnit()
        {
            var firstTeamAUnit = CreateCombatUnit(1);
            var deadTeamAUnit = CreateCombatUnit(2);
            var nextTeamAUnit = CreateCombatUnit(3, currentActionPoint: 0);
            var teamBUnit = CreateCombatUnit(4);
            deadTeamAUnit.MechRuntime.TakeDamage(
                deadTeamAUnit.Mech.MaxDurability);
            var coordinator = CreateTurnCoordinator(
                new[] { firstTeamAUnit, deadTeamAUnit, nextTeamAUnit },
                new[] { teamBUnit });

            coordinator.EndCurrentUnitAction();

            Assert.AreEqual(CombatFaction.TeamA, coordinator.CurrentFaction);
            Assert.AreSame(nextTeamAUnit, coordinator.CurrentCombatUnit);
            Assert.AreEqual(10, nextTeamAUnit.PilotRuntime.CurrentActionPoint);
        }

        [Test]
        public void EndCurrentUnitAction_AfterFactionCompletes_SwitchesFaction()
        {
            var teamAUnit = CreateCombatUnit(1);
            var teamBUnit = CreateCombatUnit(2, currentActionPoint: 0);
            var coordinator = CreateTurnCoordinator(
                new[] { teamAUnit },
                new[] { teamBUnit });

            coordinator.EndCurrentUnitAction();

            Assert.AreEqual(CombatFaction.TeamB, coordinator.CurrentFaction);
            Assert.AreSame(teamBUnit, coordinator.CurrentCombatUnit);
            Assert.AreEqual(1, coordinator.CurrentRoundNumber);
            Assert.AreEqual(10, teamBUnit.PilotRuntime.CurrentActionPoint);
        }

        [Test]
        public void EndCurrentUnitAction_AfterFullRound_ResetsTeamAProgress()
        {
            var firstTeamAUnit = CreateCombatUnit(1, currentActionPoint: 1);
            var secondTeamAUnit = CreateCombatUnit(2, currentActionPoint: 0);
            var teamBUnit = CreateCombatUnit(3, currentActionPoint: 0);
            var coordinator = CreateTurnCoordinator(
                new[] { firstTeamAUnit, secondTeamAUnit },
                new[] { teamBUnit });

            coordinator.EndCurrentUnitAction();
            coordinator.EndCurrentUnitAction();
            coordinator.EndCurrentUnitAction();

            Assert.AreEqual(CombatFaction.TeamA, coordinator.CurrentFaction);
            Assert.AreSame(firstTeamAUnit, coordinator.CurrentCombatUnit);
            Assert.AreEqual(2, coordinator.CurrentRoundNumber);
            Assert.AreEqual(10, firstTeamAUnit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(10, secondTeamAUnit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(10, teamBUnit.PilotRuntime.CurrentActionPoint);
        }

        [Test]
        public void EndCurrentUnitAction_DoesNotAddNeutralToTurnOrder()
        {
            var teamAUnit = CreateCombatUnit(1);
            var teamBUnit = CreateCombatUnit(2);
            var neutralUnit = CreateCombatUnit(3, currentActionPoint: 0);
            var registry = new CombatUnitRegistry(
                new[] { teamAUnit },
                new[] { teamBUnit },
                new[] { neutralUnit });
            var coordinator = new TurnCoordinator(registry);

            coordinator.EndCurrentUnitAction();
            coordinator.EndCurrentUnitAction();

            Assert.AreEqual(CombatFaction.TeamA, coordinator.CurrentFaction);
            Assert.AreSame(teamAUnit, coordinator.CurrentCombatUnit);
            Assert.AreEqual(0, neutralUnit.PilotRuntime.CurrentActionPoint);
        }

        [Test]
        public void EndCurrentUnitAction_RestoresOnlyTheNextCombatUnit()
        {
            var firstTeamAUnit = CreateCombatUnit(1, currentActionPoint: 3);
            var secondTeamAUnit = CreateCombatUnit(2, currentActionPoint: 0);
            var teamBUnit = CreateCombatUnit(3, currentActionPoint: 0);
            var coordinator = CreateTurnCoordinator(
                new[] { firstTeamAUnit, secondTeamAUnit },
                new[] { teamBUnit });

            coordinator.EndCurrentUnitAction();

            Assert.AreSame(secondTeamAUnit, coordinator.CurrentCombatUnit);
            Assert.AreEqual(3, firstTeamAUnit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(10, secondTeamAUnit.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(0, teamBUnit.PilotRuntime.CurrentActionPoint);
        }

        [Test]
        public void Constructor_WithNullRegistry_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TurnCoordinator(null));
        }

        private static TurnCoordinator CreateTurnCoordinator(
            CombatUnit[] teamAUnits,
            CombatUnit[] teamBUnits)
        {
            var registry = new CombatUnitRegistry(teamAUnits, teamBUnits);
            return new TurnCoordinator(registry);
        }

        private static CombatUnit CreateCombatUnit(
            int id, int maxActionPoint = 10, int currentActionPoint = 10)
        {
            var pilot = new PilotData(id, $"Pilot {id}", maxActionPoint);
            var pilotRuntime = new PilotRuntime(currentActionPoint);
            var mech = new MechData(id, $"Mech {id}", new BasicAttackData(10, 1, 1), 100, 3);
            var mechRuntime = new MechRuntime(mech.MaxDurability);
            return new CombatUnit(id, pilot, pilotRuntime, mech, mechRuntime, new Vector2Int(id, 0));
        }
    }
}
