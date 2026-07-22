using System;
using MechStorm.Battle.Data;
using MechStorm.Battle.Spatial;
using MechStorm.Battle.Units;

namespace MechStorm.Battle.Rules
{
    public sealed class AbilityResolver
    {
        private readonly SquareGrid _squareGrid;
        private readonly CombatUnitRegistry _combatUnitRegistry;

        public AbilityResolver(SquareGrid squareGrid, CombatUnitRegistry combatUnitRegistry)
        {
            _squareGrid = squareGrid ?? throw new ArgumentNullException(nameof(squareGrid));
            _combatUnitRegistry = combatUnitRegistry ?? throw new ArgumentNullException(nameof(combatUnitRegistry));
        }

        public bool TryResolveAbility(AbilityDefinition ability, CombatUnit source, CombatUnit target)
        {
            // 技能结算分为三个阶段：
            // 1. 参数合法性校验处理调用错误，失败时抛出异常，避免掩盖接线或对象归属问题。
            ValidateArguments(ability, source, target);

            // 2. 结算前状态校验处理正常玩法限制，失败时返回 false，并且不能修改战斗状态。
            if (!CanResolveAbility(ability, source, target))
            {
                return false;
            }

            if (!source.PilotRuntime.TrySpendActionPoint(ability.ActionPointCost))
            {
                throw new InvalidOperationException("Validated ability cost could not be committed.");
            }

            // 3. 前置条件全部通过后才进入结算，所有状态修改都集中在这个阶段。
            ApplyAbility(ability, target);
            return true;
        }

        private void ValidateArguments(AbilityDefinition ability, CombatUnit source, CombatUnit target)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!_combatUnitRegistry.Contains(source))
            {
                throw new ArgumentException("Source unit does not belong to this battle registry.", nameof(source));
            }

            if (!_combatUnitRegistry.Contains(target))
            {
                throw new ArgumentException("Target unit does not belong to this battle registry.", nameof(target));
            }
        }

        private bool CanResolveAbility(AbilityDefinition ability, CombatUnit source, CombatUnit target)
        {
            if (!source.IsAlive() || !target.IsAlive())
            {
                return false;
            }

            if (!_squareGrid.IsInside(source.Position) || !_squareGrid.IsInside(target.Position))
            {
                return false;
            }

            if (ability.TargetRule != TargetRule.EnemyUnit ||
                _combatUnitRegistry.GetFaction(source) == _combatUnitRegistry.GetFaction(target))
            {
                return false;
            }

            var distance = _squareGrid.GetManhattanDistance(source.Position, target.Position);
            return distance >= ability.MinRange &&
                   distance <= ability.MaxRange &&
                   source.PilotRuntime.HasEnoughActionPoint(ability.ActionPointCost);
        }

        private static void ApplyAbility(AbilityDefinition ability, CombatUnit target)
        {
            target.MechRuntime.TakeDamage(ability.Damage);
        }
    }
}