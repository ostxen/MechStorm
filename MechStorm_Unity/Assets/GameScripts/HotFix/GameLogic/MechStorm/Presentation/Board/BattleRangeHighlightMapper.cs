using System;
using System.Collections.Generic;
using MechStorm.Battle.Units;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Board
{
    /// <summary>
    /// 将 Battle 查询结果合并为每格唯一的高亮类型。
    /// </summary>
    public sealed class BattleRangeHighlightMapper
    {
        /// <summary>
        /// 按移动、攻击、合法目标的优先级生成高亮映射。
        /// </summary>
        public IReadOnlyDictionary<Vector2Int, BattleCellHighlightType> Build(IReadOnlyList<Vector2Int> movePositions,
            IReadOnlyList<Vector2Int> attackPositions, IReadOnlyList<CombatUnit> attackTargets,
            CombatUnit selectedTarget = null)
        {
            if (movePositions == null)
            {
                throw new ArgumentNullException(nameof(movePositions));
            }

            if (attackPositions == null)
            {
                throw new ArgumentNullException(nameof(attackPositions));
            }

            if (attackTargets == null)
            {
                throw new ArgumentNullException(nameof(attackTargets));
            }

            var highlights = new Dictionary<Vector2Int, BattleCellHighlightType>();
            foreach (var position in movePositions)
            {
                highlights[position] = BattleCellHighlightType.Move;
            }

            foreach (var position in attackPositions)
            {
                highlights[position] = highlights.TryGetValue(position, out var existingType) && existingType == BattleCellHighlightType.Move
                    ? BattleCellHighlightType.MoveAndAttack
                    : BattleCellHighlightType.Attack;
            }

            foreach (var target in attackTargets)
            {
                if (target == null)
                {
                    throw new ArgumentException("Attack targets cannot contain null.", nameof(attackTargets));
                }

                highlights[target.Position] = BattleCellHighlightType.ValidTarget;
            }

            if (selectedTarget != null)
            {
                if (!ContainsTarget(attackTargets, selectedTarget))
                {
                    throw new ArgumentException(
                        "Selected attack target must be present in attack targets.",
                        nameof(selectedTarget));
                }

                highlights[selectedTarget.Position] = BattleCellHighlightType.SelectedAttackTarget;
            }

            return highlights;
        }

        /// <summary>
        /// 生成技能范围与合法技能目标的高亮映射。
        /// </summary>
        public IReadOnlyDictionary<Vector2Int, BattleCellHighlightType> BuildAbility(
            IReadOnlyList<Vector2Int> abilityPositions,
            IReadOnlyList<CombatUnit> abilityTargets,
            CombatUnit selectedTarget = null)
        {
            if (abilityPositions == null)
            {
                throw new ArgumentNullException(nameof(abilityPositions));
            }

            if (abilityTargets == null)
            {
                throw new ArgumentNullException(nameof(abilityTargets));
            }

            var highlights = new Dictionary<Vector2Int, BattleCellHighlightType>();
            foreach (var position in abilityPositions)
            {
                highlights[position] = BattleCellHighlightType.Ability;
            }

            foreach (var target in abilityTargets)
            {
                if (target == null)
                {
                    throw new ArgumentException("Ability targets cannot contain null.",
                        nameof(abilityTargets));
                }

                highlights[target.Position] = BattleCellHighlightType.ValidAbilityTarget;
            }

            if (selectedTarget != null)
            {
                if (!ContainsTarget(abilityTargets, selectedTarget))
                {
                    throw new ArgumentException(
                        "Selected ability target must be present in ability targets.",
                        nameof(selectedTarget));
                }

                highlights[selectedTarget.Position] = BattleCellHighlightType.SelectedAbilityTarget;
            }

            return highlights;
        }

        private static bool ContainsTarget(IReadOnlyList<CombatUnit> targets, CombatUnit selectedTarget)
        {
            foreach (var target in targets)
            {
                if (ReferenceEquals(target, selectedTarget))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
