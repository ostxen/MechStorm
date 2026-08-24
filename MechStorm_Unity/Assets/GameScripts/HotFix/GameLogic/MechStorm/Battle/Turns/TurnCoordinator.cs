using System;
using System.Collections.Generic;
using MechStorm.Battle.Units;

namespace MechStorm.Battle.Turns
{
    public sealed class TurnCoordinator
    {
        private readonly CombatUnitRegistry _unitRegistry;
        private readonly HashSet<int> _actedUnitIds = new HashSet<int>();

        private CombatUnit _currentCombatUnit;

        public int CurrentRoundNumber { get; private set; } = 1;

        public CombatFaction CurrentFaction { get; private set; } = CombatFaction.TeamA;

        public CombatUnit CurrentCombatUnit
        {
            get
            {
                if (_currentCombatUnit == null)
                {
                    throw new InvalidOperationException(
                        "Current faction has no alive combat unit.");
                }

                if (_currentCombatUnit.IsDead())
                {
                    throw new InvalidOperationException(
                        "Dead combat unit cannot act.");
                }

                return _currentCombatUnit;
            }
        }

        public TurnCoordinator(CombatUnitRegistry unitRegistry)
        {
            _unitRegistry = unitRegistry ?? throw new ArgumentNullException(nameof(unitRegistry));
            _currentCombatUnit = FindNextAvailableUnit();
        }

        /// <summary>
        /// 查询机兵是否已经提交本阵营回合中的行动。
        /// </summary>
        public bool HasUnitActed(CombatUnit combatUnit)
        {
            if (combatUnit == null)
            {
                throw new ArgumentNullException(nameof(combatUnit));
            }

            return _actedUnitIds.Contains(combatUnit.UnitId);
        }

        /// <summary>
        /// 判断机兵能否成为当前阵营的新行动者。
        /// </summary>
        public bool CanSelectCombatUnit(CombatUnit combatUnit)
        {
            return combatUnit != null &&
                   _unitRegistry.Contains(combatUnit) &&
                   _unitRegistry.GetFaction(combatUnit) == CurrentFaction &&
                   combatUnit.IsAlive() &&
                   !HasUnitActed(combatUnit);
        }

        /// <summary>
        /// 切换当前行动者；调用方必须先处理上一机兵的暂定位置回退。
        /// </summary>
        public void SelectCombatUnit(CombatUnit combatUnit)
        {
            if (!CanSelectCombatUnit(combatUnit))
            {
                throw new InvalidOperationException(
                    "Only an alive, unacted combat unit from the current faction can be selected.");
            }

            _currentCombatUnit = combatUnit;
            RestoreCurrentCombatUnitActionPoint();
        }

        public void EndCurrentUnitAction()
        {
            _actedUnitIds.Add(CurrentCombatUnit.UnitId);
            var nextCombatUnit = FindNextAvailableUnit();
            if (nextCombatUnit != null)
            {
                _currentCombatUnit = nextCombatUnit;
                RestoreCurrentCombatUnitActionPoint();
                return;
            }

            AdvanceFaction();
            _actedUnitIds.Clear();
            _currentCombatUnit = FindNextAvailableUnit();
            RestoreCurrentCombatUnitActionPoint();
        }

        public void EndCurrentFactionTurn()
        {
            AdvanceFaction();
            _actedUnitIds.Clear();
            _currentCombatUnit = FindNextAvailableUnit();
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

        private CombatUnit FindNextAvailableUnit()
        {
            foreach (var combatUnit in GetCurrentFactionUnits())
            {
                if (combatUnit.IsAlive() && !HasUnitActed(combatUnit))
                {
                    return combatUnit;
                }
            }

            return null;
        }

        private void RestoreCurrentCombatUnitActionPoint()
        {
            if (_currentCombatUnit == null)
            {
                return;
            }

            var currentCombatUnit = CurrentCombatUnit;
            currentCombatUnit.PilotRuntime.ResetActionPointToMax(currentCombatUnit.Pilot.MaxActionPoint);
        }
    }
}