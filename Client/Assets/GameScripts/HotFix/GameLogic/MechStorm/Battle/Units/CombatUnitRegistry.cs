using System;
using System.Collections.Generic;

namespace MechStorm.Battle.Units
{
    public sealed class CombatUnitRegistry
    {
        private readonly IReadOnlyList<CombatUnit> _teamAUnits;
        private readonly IReadOnlyList<CombatUnit> _teamBUnits;
        private readonly IReadOnlyList<CombatUnit> _neutralUnits;
        private readonly IReadOnlyList<CombatUnit> _combatUnits;
        private readonly IReadOnlyDictionary<int, CombatUnit> _combatUnitDict;

        public CombatUnitRegistry(IEnumerable<CombatUnit> teamAUnits, IEnumerable<CombatUnit> teamBUnits, IEnumerable<CombatUnit> neutralUnits = null)
        {
            _teamAUnits = CreateValidatedUnitList(teamAUnits, nameof(teamAUnits), false);
            _teamBUnits = CreateValidatedUnitList(teamBUnits, nameof(teamBUnits), false);
            _neutralUnits = CreateValidatedUnitList(neutralUnits, nameof(neutralUnits), true);

            var combatUnits = new List<CombatUnit>(_teamAUnits.Count + _teamBUnits.Count + _neutralUnits.Count);
            AddFactionUnits(combatUnits, _teamAUnits, nameof(teamAUnits));
            AddFactionUnits(combatUnits, _teamBUnits, nameof(teamBUnits));
            AddFactionUnits(combatUnits, _neutralUnits, nameof(neutralUnits));
            _combatUnits = combatUnits.AsReadOnly();
            _combatUnitDict = CreateUnitLookup(combatUnits);
        }

        public IReadOnlyList<CombatUnit> CombatUnits => _combatUnits;

        public bool Contains(CombatUnit unit)
        {
            return ContainsUnit(_combatUnits, unit);
        }

        public CombatUnit GetCombatUnit(int unitId)
        {
            if (unitId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitId), unitId, "Unit ID must be greater than zero.");
            }

            return _combatUnitDict.TryGetValue(unitId, out var unit) ? unit 
                : throw new KeyNotFoundException($"Combat unit ID {unitId} does not belong to this battle registry.");
        }

        public CombatFaction GetFaction(CombatUnit unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            if (ContainsUnit(_teamAUnits, unit))
            {
                return CombatFaction.TeamA;
            }

            if (ContainsUnit(_teamBUnits, unit))
            {
                return CombatFaction.TeamB;
            }

            if (ContainsUnit(_neutralUnits, unit))
            {
                return CombatFaction.Neutral;
            }

            throw new ArgumentException(
                "Combat unit does not belong to this battle registry.",
                nameof(unit));
        }

        public IReadOnlyList<CombatUnit> GetFactionUnits(CombatFaction faction)
        {
            switch (faction)
            {
                case CombatFaction.TeamA:
                    return _teamAUnits;
                case CombatFaction.TeamB:
                    return _teamBUnits;
                case CombatFaction.Neutral:
                    return _neutralUnits;
                default:
                    throw new ArgumentOutOfRangeException(nameof(faction), faction, null);
            }
        }

        public IReadOnlyList<CombatUnit> GetAliveUnits()
        {
            return FindUnitsByAliveState(true);
        }

        public IReadOnlyList<CombatUnit> GetDeadUnits()
        {
            return FindUnitsByAliveState(false);
        }

        public bool AreFactionUnitsDead(CombatFaction faction)
        {
            foreach (var unit in GetFactionUnits(faction))
            {
                if (unit.IsAlive())
                {
                    return false;
                }
            }

            return true;
        }

        private IReadOnlyList<CombatUnit> FindUnitsByAliveState(bool isAlive)
        {
            var units = new List<CombatUnit>();
            foreach (var unit in _combatUnits)
            {
                if (unit.IsAlive() == isAlive)
                {
                    units.Add(unit);
                }
            }

            return units.AsReadOnly();
        }

        private static IReadOnlyList<CombatUnit> CreateValidatedUnitList(IEnumerable<CombatUnit> combatUnits, string parameterName, bool canBeEmpty)
        {
            if (combatUnits == null)
            {
                if (canBeEmpty)
                {
                    return Array.Empty<CombatUnit>();
                }

                throw new ArgumentNullException(parameterName);
            }

            var unitList = new List<CombatUnit>();
            foreach (var unit in combatUnits)
            {
                if (unit == null)
                {
                    throw new ArgumentException(
                        "Battle registry cannot contain a null unit.",
                        parameterName);
                }

                if (ContainsUnit(unitList, unit))
                {
                    throw new ArgumentException(
                        "A combat unit can only be added to a battle faction once.",
                        parameterName);
                }

                unitList.Add(unit);
            }

            if (!canBeEmpty && unitList.Count == 0)
            {
                throw new ArgumentException(
                    "Battle faction must contain at least one unit.",
                    parameterName);
            }

            return unitList.AsReadOnly();
        }

        private static void AddFactionUnits(List<CombatUnit> combatUnits, IReadOnlyList<CombatUnit> factionUnits, string parameterName)
        {
            foreach (var unit in factionUnits)
            {
                if (ContainsUnit(combatUnits, unit))
                {
                    throw new ArgumentException(
                        "A combat unit can only belong to one battle faction.",
                        parameterName);
                }

                combatUnits.Add(unit);
            }
        }

        private static IReadOnlyDictionary<int, CombatUnit> CreateUnitLookup(IReadOnlyList<CombatUnit> combatUnits)
        {
            var unitDict = new Dictionary<int, CombatUnit>(combatUnits.Count);
            foreach (var unit in combatUnits)
            {
                if (unit.UnitId <= 0)
                {
                    throw new ArgumentException("Battle registry requires unit IDs greater than zero.", nameof(combatUnits));
                }

                if (!unitDict.TryAdd(unit.UnitId, unit))
                {
                    throw new ArgumentException($"Battle registry cannot contain duplicate unit ID {unit.UnitId}.", nameof(combatUnits));
                }
            }

            return unitDict;
        }

        private static bool ContainsUnit(IReadOnlyList<CombatUnit> units, CombatUnit targetUnit)
        {
            for (var i = 0; i < units.Count; i++)
            {
                if (ReferenceEquals(units[i], targetUnit))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
