using System;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Controllers
{
    /// <summary>
    /// 当前已接入的行动来源；交互状态不按具体武器或技能拆分。
    /// </summary>
    public enum BattleActionSelectionKind
    {
        BasicAttack,
        ActiveAbility,
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
    /// 表现层使用的不可变行动选择，统一承载基础攻击与主动技能。
    /// </summary>
    public sealed class BattleActionSelection
    {
        private BattleActionSelection(BattleActionSelectionKind kind, BattleActionTargetKind targetKind,
            AbilityDefinition ability)
        {
            Kind = kind;
            TargetKind = targetKind;
            Ability = ability;
        }

        public BattleActionSelectionKind Kind { get; }

        public BattleActionTargetKind TargetKind { get; }

        public AbilityDefinition Ability { get; }

        public static BattleActionSelection ForBasicAttack()
        {
            return new BattleActionSelection(
                BattleActionSelectionKind.BasicAttack,
                BattleActionTargetKind.Unit,
                null);
        }

        public static BattleActionSelection ForAbility(AbilityDefinition ability)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            return new BattleActionSelection(
                BattleActionSelectionKind.ActiveAbility,
                BattleActionTargetKind.Unit,
                ability);
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
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            return new BattleActionTargetSelection(BattleActionTargetKind.Unit, unit, null);
        }

        public static BattleActionTargetSelection ForGrid(Vector2Int gridPosition)
        {
            return new BattleActionTargetSelection(BattleActionTargetKind.Grid, null, gridPosition);
        }

        public static BattleActionTargetSelection ForSelf(CombatUnit unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            return new BattleActionTargetSelection(BattleActionTargetKind.Self, unit, null);
        }

        public static BattleActionTargetSelection WithoutTarget()
        {
            return new BattleActionTargetSelection(BattleActionTargetKind.None, null, null);
        }
    }
}
