using System;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Interaction
{
    /// <summary>
    /// 当前已接入的行动来源；交互状态不按具体武器或技能拆分。
    /// </summary>
    public enum BattleActionSelectionKind
    {
        BasicAttack,
        Ability,
    }

    /// <summary>
    /// 描述行动需要的目标形态；Sprint 4 只实际执行单位目标。
    /// </summary>
    public enum BattleActionTargetKind
    {
        Unit,
        Grid,
        Self,
        None,
    }

    /// <summary>
    /// 不可变的行动选择；技能分支同时承载武器技能和角色指令。
    /// </summary>
    public sealed class BattleActionSelection
    {
        private BattleActionSelection(BattleActionSelectionKind kind, BattleActionTargetKind targetKind,
            AbilityDefinition ability, AbilityTargetRangeSource rangeSource)
        {
            Kind = kind;
            TargetKind = targetKind;
            Ability = ability;
            RangeSource = rangeSource;
        }

        public BattleActionSelectionKind Kind { get; }

        public BattleActionTargetKind TargetKind { get; }

        public AbilityDefinition Ability { get; }

        /// <summary>
        /// 固化本次选择的范围来源，保证查询、高亮和确认使用同一规则。
        /// </summary>
        public AbilityTargetRangeSource RangeSource { get; }

        public static BattleActionSelection ForBasicAttack()
        {
            return new BattleActionSelection(
                BattleActionSelectionKind.BasicAttack,
                BattleActionTargetKind.Unit,
                null,
                AbilityTargetRangeSource.BasicWeapon);
        }

        public static BattleActionSelection ForAbility(AbilityDefinition ability,
            AbilityTargetRangeSource rangeSource = AbilityTargetRangeSource.Ability)
        {
            ValidateAbilitySelection(ability, rangeSource);

            return new BattleActionSelection(
                BattleActionSelectionKind.Ability,
                BattleActionTargetKind.Unit,
                ability,
                rangeSource);
        }

        private static void ValidateAbilitySelection(AbilityDefinition ability, AbilityTargetRangeSource rangeSource)
        {
            BattleInteractionArguments.RequireNotNull(ability, nameof(ability));

            if (!Enum.IsDefined(typeof(AbilityTargetRangeSource), rangeSource))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rangeSource),
                    rangeSource,
                    "Ability range source must be defined.");
            }

            if (rangeSource == AbilityTargetRangeSource.BasicWeapon &&
                !ability.RequiresBasicWeapon)
            {
                throw new ArgumentException(
                    "Ability must require the basic weapon to use its range.",
                    nameof(rangeSource));
            }
        }

        public bool Matches(BattleActionSelection other)
        {
            if (other == null || Kind != other.Kind)
            {
                return false;
            }

            return Kind != BattleActionSelectionKind.Ability ||
                   (ReferenceEquals(Ability, other.Ability) &&
                    RangeSource == other.RangeSource);
        }
    }

    /// <summary>
    /// 表现层使用的不可变目标选择，为后续地格、自身和无目标行动保留统一契约。
    /// </summary>
    public sealed class BattleActionTargetSelection
    {
        private BattleActionTargetSelection(BattleActionTargetKind kind, CombatUnit unit,
            Vector2Int? gridPosition)
        {
            Kind = kind;
            Unit = unit;
            GridPosition = gridPosition;
        }

        public BattleActionTargetKind Kind { get; }

        public CombatUnit Unit { get; }

        public Vector2Int? GridPosition { get; }

        public static BattleActionTargetSelection ForUnit(CombatUnit unit)
        {
            BattleInteractionArguments.RequireNotNull(unit, nameof(unit));

            return new BattleActionTargetSelection(BattleActionTargetKind.Unit, unit, null);
        }

        public static BattleActionTargetSelection ForGrid(Vector2Int gridPosition)
        {
            return new BattleActionTargetSelection(BattleActionTargetKind.Grid, null, gridPosition);
        }

        public static BattleActionTargetSelection ForSelf(CombatUnit unit)
        {
            BattleInteractionArguments.RequireNotNull(unit, nameof(unit));

            return new BattleActionTargetSelection(BattleActionTargetKind.Self, unit, null);
        }

        public static BattleActionTargetSelection WithoutTarget()
        {
            return new BattleActionTargetSelection(BattleActionTargetKind.None, null, null);
        }
    }

    // 只处理调用参数契约，AP、射程及交互状态仍由各自规则判断。
    internal static class BattleInteractionArguments
    {
        internal static T RequireNotNull<T>(T value, string parameterName) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }
    }
}
