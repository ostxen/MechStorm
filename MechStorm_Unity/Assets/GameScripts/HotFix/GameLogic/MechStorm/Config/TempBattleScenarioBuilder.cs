using System;
using System.Collections.Generic;
using MechStorm.Battle;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using BattleVector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation
{
    /// <summary>
    /// 将通过校验的 Unity 配置单向转换为全新的纯战斗逻辑对象。
    /// </summary>
    public sealed class TempBattleScenarioBuilder
    {
        private readonly TempBattleScenarioValidator _validator;

        public TempBattleScenarioBuilder()
            : this(new TempBattleScenarioValidator())
        {
        }

        internal TempBattleScenarioBuilder(TempBattleScenarioValidator validator)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public bool TryBuild(
            TempBattleScenarioConfig config,
            out TempBattleScenarioBuildResult buildResult,
            out TempBattleScenarioValidationResult validationResult)
        {
            validationResult = _validator.Validate(config);
            if (!validationResult.IsValid)
            {
                buildResult = null;
                return false;
            }

            var unitFactory = new CombatUnitFactory();
            var allUnits = new List<CombatUnit>(config.Units.Count);
            var teamAUnits = new List<CombatUnit>();
            var teamBUnits = new List<CombatUnit>();
            var abilitiesByUnitId = new Dictionary<int, IReadOnlyList<AbilityDefinition>>();

            foreach (var unitConfig in config.Units)
            {
                var pilot = BuildPilot(unitConfig.Pilot);
                var mech = BuildMech(unitConfig.Mech);
                var unit = unitFactory.Create(
                    unitConfig.UnitId,
                    pilot,
                    mech,
                    new BattleVector2Int(
                        unitConfig.InitialPosition.x,
                        unitConfig.InitialPosition.y));

                allUnits.Add(unit);
                AddUnitToFaction(unitConfig.Faction, unit, teamAUnits, teamBUnits);
                abilitiesByUnitId.Add(unit.UnitId, BuildAbilities(unitConfig.Abilities));
            }

            var battleSession = new BattleSession(
                config.BoardWidth,
                config.BoardHeight,
                teamAUnits,
                teamBUnits);
            buildResult = new TempBattleScenarioBuildResult(
                battleSession,
                allUnits,
                abilitiesByUnitId);
            return true;
        }

        private static PilotData BuildPilot(TempPilotConfig config)
        {
            return new PilotData(config.Id, config.Name, config.MaxActionPoint);
        }

        private static MechData BuildMech(TempMechConfig config)
        {
            var basicAttack = new BasicAttackData(
                config.BasicAttack.Damage,
                config.BasicAttack.MinRange,
                config.BasicAttack.MaxRange,
                config.BasicAttack.ActionPointCost);
            return new MechData(
                config.Id,
                config.Name,
                basicAttack,
                config.MaxDurability,
                config.MoveRange);
        }

        private static IReadOnlyList<AbilityDefinition> BuildAbilities(
            IReadOnlyList<TempAbilityConfig> configs)
        {
            var abilities = new List<AbilityDefinition>(configs.Count);
            foreach (var config in configs)
            {
                abilities.Add(new AbilityDefinition(
                    config.AbilityId,
                    config.Name,
                    config.MinRange,
                    config.MaxRange,
                    config.Damage,
                    config.ActionPointCost,
                    config.TargetRule,
                    config.RequiresBasicWeapon));
            }

            return abilities.AsReadOnly();
        }

        private static void AddUnitToFaction(
            CombatFaction faction,
            CombatUnit unit,
            ICollection<CombatUnit> teamAUnits,
            ICollection<CombatUnit> teamBUnits)
        {
            switch (faction)
            {
                case CombatFaction.TeamA:
                    teamAUnits.Add(unit);
                    break;
                case CombatFaction.TeamB:
                    teamBUnits.Add(unit);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(faction),
                        faction,
                        "Only TeamA and TeamB units can be built for the temporary 2v2 scenario.");
            }
        }
    }
}
