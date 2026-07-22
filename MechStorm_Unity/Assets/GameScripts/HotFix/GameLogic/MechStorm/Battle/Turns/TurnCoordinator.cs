using System;
using System.Collections.Generic;
using MechStorm.Battle.Units;

namespace MechStorm.Battle.Turns
{
    public sealed class TurnCoordinator
    {
        private const int InvalidFactionUnitIndex = -1;

        private readonly CombatUnitRegistry _unitRegistry;

        private int _currentFactionUnitIndex;

        public int CurrentRoundNumber { get; private set; } = 1;

        public CombatFaction CurrentFaction { get; private set; } = CombatFaction.TeamA;

        public CombatUnit CurrentCombatUnit
        {
            get
            {
                var currentFactionUnits = GetCurrentFactionUnits();
                if (_currentFactionUnitIndex == InvalidFactionUnitIndex || _currentFactionUnitIndex >= currentFactionUnits.Count)
                {
                    throw new InvalidOperationException("Current faction has no alive combat unit.");
                }

                var currentCombatUnit = currentFactionUnits[_currentFactionUnitIndex];
                if (currentCombatUnit.IsDead())
                {
                    throw new InvalidOperationException("Dead combat unit cannot act.");
                }
                return currentCombatUnit;
            }
        }

        public TurnCoordinator(CombatUnitRegistry unitRegistry)
        {
            _unitRegistry = unitRegistry ?? throw new ArgumentNullException(nameof(unitRegistry));
            _currentFactionUnitIndex = FindNextAliveUnitIndex(GetCurrentFactionUnits(), InvalidFactionUnitIndex);
        }

        public void EndCurrentUnitAction()
        {
            var nextActionUnitIndex = FindNextAliveUnitIndex(GetCurrentFactionUnits(), _currentFactionUnitIndex);
            if (nextActionUnitIndex != InvalidFactionUnitIndex)
            {
                _currentFactionUnitIndex = nextActionUnitIndex;
                RestoreCurrentCombatUnitActionPoint();
                return;
            }

            AdvanceFaction();
            _currentFactionUnitIndex = FindNextAliveUnitIndex(GetCurrentFactionUnits(), InvalidFactionUnitIndex);
            RestoreCurrentCombatUnitActionPoint();
        }

        private IReadOnlyList<CombatUnit> GetCurrentFactionUnits()
        {
            return _unitRegistry.GetFactionUnits(CurrentFaction);
        }

        private void AdvanceFaction()
        {
            if (CurrentFaction == CombatFaction.TeamA)
            {
                CurrentFaction = CombatFaction.TeamB;
                return;
            }

            CurrentFaction = CombatFaction.TeamA;
            CurrentRoundNumber++;
        }

        private static int FindNextAliveUnitIndex(IReadOnlyList<CombatUnit> units, int currentUnitIndex)
        {
            for (var i = currentUnitIndex + 1; i < units.Count; i++)
            {
                if (units[i].IsAlive())
                {
                    return i;
                }
            }

            return InvalidFactionUnitIndex;
        }

        private void RestoreCurrentCombatUnitActionPoint()
        {
            if (_currentFactionUnitIndex == InvalidFactionUnitIndex)
            {
                return;
            }

            var currentCombatUnit = CurrentCombatUnit;
            currentCombatUnit.PilotRuntime.ResetActionPointToMax(currentCombatUnit.Pilot.MaxActionPoint);
        }
    }
}