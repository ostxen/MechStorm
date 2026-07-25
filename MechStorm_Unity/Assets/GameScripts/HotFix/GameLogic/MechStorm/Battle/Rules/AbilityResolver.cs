using System;
using System.Collections.Generic;
using MechStorm.Battle.Data;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Spatial;
using MechStorm.Battle.Units;

namespace MechStorm.Battle.Rules
{
    /// <summary>
    /// 校验并结算单体主动技能。
    /// </summary>
    public sealed class AbilityResolver
    {
        private readonly SquareGrid _squareGrid;
        private readonly CombatUnitRegistry _unitRegistry;

        public AbilityResolver(SquareGrid squareGrid, CombatUnitRegistry unitRegistry)
        {
            _squareGrid = squareGrid ?? throw new ArgumentNullException(nameof(squareGrid));
            _unitRegistry = unitRegistry ?? throw new ArgumentNullException(nameof(unitRegistry));
        }

        /// <summary>
        /// 判断技能是否可对目标使用，不修改战斗状态。
        /// </summary>
        public bool CanUseAbility(AbilityDefinition ability, CombatUnit source, CombatUnit target)
        {
            ValidateArguments(ability, source, target);

            if (source.IsDead() || target.IsDead())
            {
                return false;
            }

            if (!_squareGrid.IsInside(source.Position) || !_squareGrid.IsInside(target.Position))
            {
                return false;
            }

            if (!MatchesTargetRule(ability.TargetRule, source, target))
            {
                return false;
            }

            return IsPositionInRange(ability, source, target.Position) &&
                   source.PilotRuntime.HasEnoughActionPoint(ability.ActionPointCost);
        }

        public bool IsPositionInRange(AbilityDefinition ability, CombatUnit source, Vector2Int targetPosition)
        {
            ValidateSource(ability, source);

            if (!_squareGrid.IsInside(source.Position) || !_squareGrid.IsInside(targetPosition))
            {
                return false;
            }

            var distance = _squareGrid.GetManhattanDistance(source.Position, targetPosition);
            return distance >= ability.MinRange && distance <= ability.MaxRange;
        }

        public IReadOnlyList<Vector2Int> GetPositionsInRange(AbilityDefinition ability, CombatUnit source)
        {
            ValidateSource(ability, source);

            var positions = new List<Vector2Int>();
            for (var x = 0; x < _squareGrid.Width; x++)
            {
                for (var y = 0; y < _squareGrid.Height; y++)
                {
                    var position = new Vector2Int(x, y);
                    if (IsPositionInRange(ability, source, position))
                    {
                        positions.Add(position);
                    }
                }
            }

            return positions;
        }

        /// <summary>
        /// 对合法目标支付固定 AP 并结算固定伤害。
        /// </summary>
        public bool TryResolveAbility(AbilityDefinition ability, CombatUnit source, CombatUnit target)
        {
            if (!CanUseAbility(ability, source, target))
            {
                return false;
            }

            if (!source.PilotRuntime.TrySpendActionPoint(ability.ActionPointCost))
            {
                throw new InvalidOperationException(
                    "Validated ability cost could not be committed.");
            }

            target.MechRuntime.TakeDamage(ability.Damage);
            return true;
        }

        private void ValidateArguments(AbilityDefinition ability, CombatUnit source, CombatUnit target)
        {
            ValidateSource(ability, source);

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!_unitRegistry.Contains(target))
            {
                throw new ArgumentException("Target unit does not belong to this battle registry.", nameof(target));
            }
        }

        private void ValidateSource(AbilityDefinition ability, CombatUnit source)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!_unitRegistry.Contains(source))
            {
                throw new ArgumentException("Source unit does not belong to this battle registry.", nameof(source));
            }
        }

        private bool MatchesTargetRule(TargetRule targetRule, CombatUnit source, CombatUnit target)
        {
            switch (targetRule)
            {
                case TargetRule.EnemyUnit:
                    return _unitRegistry.GetFaction(source) != _unitRegistry.GetFaction(target);
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetRule), targetRule, null);
            }
        }
    }
}