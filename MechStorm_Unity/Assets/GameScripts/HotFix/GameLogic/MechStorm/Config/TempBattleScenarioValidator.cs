using System;
using System.Collections.Generic;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using UnityEngine;

namespace MechStorm.Presentation
{
    /// <summary>
    /// 在创建任何 Runtime 前一次性校验临时战斗场景配置。
    /// </summary>
    public sealed class TempBattleScenarioValidator
    {
        private const int RequiredTeamAUnitCount = 2;
        private const int RequiredTeamBUnitCount = 2;

        public TempBattleScenarioValidationResult Validate(TempBattleScenarioConfig config)
        {
            var errors = new List<string>();
            if (config == null)
            {
                errors.Add("Battle scenario config is required.");
                return new TempBattleScenarioValidationResult(errors);
            }

            ValidateBoard(config, errors);
            ValidateUnits(config, errors);
            return new TempBattleScenarioValidationResult(errors);
        }

        private static void ValidateBoard(TempBattleScenarioConfig config, ICollection<string> errors)
        {
            if (config.BoardWidth <= 0)
            {
                errors.Add("Board width must be greater than zero.");
            }

            if (config.BoardHeight <= 0)
            {
                errors.Add("Board height must be greater than zero.");
            }
        }

        private static void ValidateUnits(TempBattleScenarioConfig config, ICollection<string> errors)
        {
            var units = config.Units;
            if (units == null)
            {
                errors.Add("Battle scenario units are required.");
                return;
            }

            var unitIds = new HashSet<int>();
            var abilityIds = new HashSet<int>();
            var occupiedPositions = new HashSet<Vector2Int>();
            var teamAUnitCount = 0;
            var teamBUnitCount = 0;
            var neutralUnitCount = 0;

            for (var unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                var unit = units[unitIndex];
                if (unit == null)
                {
                    errors.Add($"Unit at index {unitIndex} is required.");
                    continue;
                }

                CountFaction(unit.Faction, ref teamAUnitCount, ref teamBUnitCount, ref neutralUnitCount, errors, unitIndex);
                ValidateUnitId(unit, unitIndex, unitIds, errors);
                ValidatePosition(unit, config.BoardWidth, config.BoardHeight, occupiedPositions, errors);
                ValidatePilot(unit, errors);
                ValidateMech(unit, errors);
                ValidateAbilities(unit, abilityIds, errors);
            }

            if (teamAUnitCount != RequiredTeamAUnitCount ||
                teamBUnitCount != RequiredTeamBUnitCount ||
                neutralUnitCount != 0)
            {
                errors.Add(
                    $"Battle scenario requires exactly {RequiredTeamAUnitCount} TeamA units and " +
                    $"{RequiredTeamBUnitCount} TeamB units with no Neutral units. " +
                    $"Actual: TeamA={teamAUnitCount}, TeamB={teamBUnitCount}, Neutral={neutralUnitCount}.");
            }
        }

        private static void CountFaction(
            CombatFaction faction,
            ref int teamAUnitCount,
            ref int teamBUnitCount,
            ref int neutralUnitCount,
            ICollection<string> errors,
            int unitIndex)
        {
            switch (faction)
            {
                case CombatFaction.TeamA:
                    teamAUnitCount++;
                    break;
                case CombatFaction.TeamB:
                    teamBUnitCount++;
                    break;
                case CombatFaction.Neutral:
                    neutralUnitCount++;
                    break;
                default:
                    errors.Add($"Unit at index {unitIndex} has an undefined faction value: {(int)faction}.");
                    break;
            }
        }

        private static void ValidateUnitId(
            TempCombatUnitConfig unit,
            int unitIndex,
            ISet<int> unitIds,
            ICollection<string> errors)
        {
            if (unit.UnitId <= 0)
            {
                errors.Add($"Unit at index {unitIndex} must have an ID greater than zero.");
                return;
            }

            if (!unitIds.Add(unit.UnitId))
            {
                errors.Add($"Duplicate unit ID: {unit.UnitId}.");
            }
        }

        private static void ValidatePosition(
            TempCombatUnitConfig unit,
            int boardWidth,
            int boardHeight,
            ISet<Vector2Int> occupiedPositions,
            ICollection<string> errors)
        {
            var position = unit.InitialPosition;
            if (boardWidth > 0 &&
                boardHeight > 0 &&
                (position.x < 0 || position.x >= boardWidth || position.y < 0 || position.y >= boardHeight))
            {
                errors.Add(
                    $"Unit {unit.UnitId} position ({position.x}, {position.y}) is outside " +
                    $"board {boardWidth}x{boardHeight}.");
            }

            if (!occupiedPositions.Add(position))
            {
                errors.Add($"Multiple units occupy position ({position.x}, {position.y}).");
            }
        }

        private static void ValidatePilot(TempCombatUnitConfig unit, ICollection<string> errors)
        {
            var pilot = unit.Pilot;
            if (pilot == null)
            {
                errors.Add($"Unit {unit.UnitId} requires a pilot config.");
                return;
            }

            if (pilot.Id <= 0)
            {
                errors.Add($"Unit {unit.UnitId} pilot ID must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(pilot.Name))
            {
                errors.Add($"Unit {unit.UnitId} pilot name is required.");
            }

            if (pilot.MaxActionPoint < 0)
            {
                errors.Add($"Unit {unit.UnitId} pilot maximum action point must be non-negative.");
            }
        }

        private static void ValidateMech(TempCombatUnitConfig unit, ICollection<string> errors)
        {
            var mech = unit.Mech;
            if (mech == null)
            {
                errors.Add($"Unit {unit.UnitId} requires a mech config.");
                return;
            }

            if (mech.Id <= 0)
            {
                errors.Add($"Unit {unit.UnitId} mech ID must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(mech.Name))
            {
                errors.Add($"Unit {unit.UnitId} mech name is required.");
            }

            if (mech.MaxDurability <= 0)
            {
                errors.Add($"Unit {unit.UnitId} mech maximum durability must be greater than zero.");
            }

            if (mech.MoveRange < 0)
            {
                errors.Add($"Unit {unit.UnitId} mech move range must be non-negative.");
            }

            ValidateBasicAttack(unit.UnitId, mech.BasicAttack, errors);
        }

        private static void ValidateBasicAttack(
            int unitId,
            TempBasicAttackConfig basicAttack,
            ICollection<string> errors)
        {
            if (basicAttack == null)
            {
                errors.Add($"Unit {unitId} requires a basic attack config.");
                return;
            }

            if (basicAttack.Damage < 0)
            {
                errors.Add($"Unit {unitId} basic attack damage must be non-negative.");
            }

            ValidateRange(
                $"Unit {unitId} basic attack",
                basicAttack.MinRange,
                basicAttack.MaxRange,
                errors);
        }

        private static void ValidateAbilities(
            TempCombatUnitConfig unit,
            ISet<int> abilityIds,
            ICollection<string> errors)
        {
            var abilities = unit.Abilities;
            if (abilities == null)
            {
                errors.Add($"Unit {unit.UnitId} ability list is required.");
                return;
            }

            for (var abilityIndex = 0; abilityIndex < abilities.Count; abilityIndex++)
            {
                var ability = abilities[abilityIndex];
                if (ability == null)
                {
                    errors.Add($"Unit {unit.UnitId} ability at index {abilityIndex} is required.");
                    continue;
                }

                if (ability.AbilityId <= 0)
                {
                    errors.Add($"Unit {unit.UnitId} ability at index {abilityIndex} must have an ID greater than zero.");
                }
                else if (!abilityIds.Add(ability.AbilityId))
                {
                    errors.Add($"Duplicate ability ID: {ability.AbilityId}.");
                }

                if (string.IsNullOrWhiteSpace(ability.Name))
                {
                    errors.Add($"Ability {ability.AbilityId} name is required.");
                }

                if (ability.ActionPointCost < 0)
                {
                    errors.Add($"Ability {ability.AbilityId} action point cost must be non-negative.");
                }

                if (ability.Damage < 0)
                {
                    errors.Add($"Ability {ability.AbilityId} damage must be non-negative.");
                }

                ValidateRange($"Ability {ability.AbilityId}", ability.MinRange, ability.MaxRange, errors);

                if (!Enum.IsDefined(typeof(TargetRule), ability.TargetRule))
                {
                    errors.Add($"Ability {ability.AbilityId} has an undefined target rule: {(int)ability.TargetRule}.");
                }
            }
        }

        private static void ValidateRange(
            string owner,
            int minRange,
            int maxRange,
            ICollection<string> errors)
        {
            if (minRange < 1)
            {
                errors.Add($"{owner} minimum range must be at least one.");
            }

            if (maxRange < minRange)
            {
                errors.Add($"{owner} maximum range must be greater than or equal to minimum range.");
            }
        }
    }
}
