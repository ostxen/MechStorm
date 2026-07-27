using System;
using System.Collections.Generic;
using MechStorm.Battle.Outcomes;
using MechStorm.Battle.Units;

namespace MechStorm.Battle.Snapshots
{
    /// <summary>
    /// 记录创建快照时整个战场的完整数据，不记录此前的变化过程。
    /// </summary>
    public sealed class BattleSnapshot
    {
        public int BoardWidth { get; }

        public int BoardHeight { get; }

        public int RoundNumber { get; }

        public CombatFaction CurrentFaction { get; }

        public int CurrentUnitId { get; }

        /// <summary>
        /// 创建快照时的对局结果。
        /// </summary>
        public BattleOutcome Outcome { get; }

        /// <summary>
        /// 创建快照时所有单位的只读副本。
        /// </summary>
        public IReadOnlyList<BattleUnitSnapshot> Units { get; }

        /// <summary>
        /// 创建完整战场快照；未显式提供 Outcome 时表示对局仍在进行。
        /// </summary>
        public BattleSnapshot(int boardWidth, int boardHeight, int roundNumber, CombatFaction currentFaction,
            int currentUnitId, IEnumerable<BattleUnitSnapshot> units, BattleOutcome outcome = null)
        {
            ValidateBasicFields(boardWidth, boardHeight, roundNumber, currentUnitId);

            var unitList = CopyAndValidateUnits(units);
            ValidateCurrentUnit(unitList, currentUnitId, currentFaction);

            BoardWidth = boardWidth;
            BoardHeight = boardHeight;
            RoundNumber = roundNumber;
            CurrentFaction = currentFaction;
            CurrentUnitId = currentUnitId;
            Units = unitList.AsReadOnly();
            Outcome = outcome ?? BattleOutcome.InProgress;
        }

        private static void ValidateBasicFields(int boardWidth, int boardHeight, int roundNumber, int currentUnitId)
        {
            if (boardWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(boardWidth), boardWidth, "Board width must be greater than zero.");
            }

            if (boardHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(boardHeight), boardHeight, "Board height must be greater than zero.");
            }

            if (roundNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roundNumber), roundNumber, "Round number must be greater than zero.");
            }

            if (currentUnitId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentUnitId), currentUnitId, "Current unit ID must be greater than zero.");
            }
        }

        private static List<BattleUnitSnapshot> CopyAndValidateUnits(IEnumerable<BattleUnitSnapshot> units)
        {
            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            var unitList = new List<BattleUnitSnapshot>();
            var unitIds = new HashSet<int>();
            foreach (var unit in units)
            {
                if (unit == null)
                {
                    throw new ArgumentException("Battle snapshot cannot contain a null unit.", nameof(units));
                }

                if (!unitIds.Add(unit.UnitId))
                {
                    throw new ArgumentException("Battle snapshot cannot contain duplicate unit IDs.", nameof(units));
                }

                unitList.Add(unit);
            }

            if (unitList.Count == 0)
            {
                throw new ArgumentException("Battle snapshot must contain at least one unit.", nameof(units));
            }

            return unitList;
        }

        private static void ValidateCurrentUnit(IReadOnlyList<BattleUnitSnapshot> units, int currentUnitId,
            CombatFaction currentFaction)
        {
            BattleUnitSnapshot currentUnit = null;
            foreach (var unit in units)
            {
                if (unit.UnitId == currentUnitId)
                {
                    currentUnit = unit;
                    break;
                }
            }

            if (currentUnit == null)
            {
                throw new ArgumentException("Current unit ID must belong to a snapshot unit.", nameof(currentUnitId));
            }

            if (currentUnit.Faction != currentFaction)
            {
                throw new ArgumentException("Current unit faction must match the current faction.", nameof(currentFaction));
            }

            if (currentUnit.IsDead)
            {
                throw new ArgumentException("Current unit must be alive.", nameof(currentUnitId));
            }
        }
    }
}