using System;
using System.Collections.Generic;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Units;

namespace MechStorm.Battle.Actions
{
    /// <summary>
    /// 记录一次战斗操作发生了什么，包括执行结果及该操作涉及的数据变化。
    /// </summary>
    public sealed class BattleActionLog
    {
        public int Sequence { get; }

        public BattleActionType ActionType { get; }

        public bool IsSuccess => FailureReason == BattleActionFailureReason.None;

        public BattleActionFailureReason FailureReason { get; }

        public int ActorUnitId { get; }

        public int? TargetUnitId { get; }

        public int? AbilityId { get; }

        public int? NextUnitId { get; }

        public IReadOnlyList<BattleActionChangeType> ChangeTypes { get; }

        public Vector2Int? PositionBefore { get; }

        public Vector2Int? PositionAfter { get; }

        public int? DurabilityBefore { get; }

        public int? DurabilityAfter { get; }

        public int? ActionPointBefore { get; }

        public int? ActionPointAfter { get; }

        public int? RoundNumberBefore { get; }

        public int? RoundNumberAfter { get; }

        public CombatFaction? FactionBefore { get; }

        public CombatFaction? FactionAfter { get; }

        public BattleActionLog(BattleActionResult result, int actorUnitId, int? targetUnitId = null, int? nextUnitId = null)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            ValidateUnitIds(result, actorUnitId, targetUnitId, nextUnitId);

            Sequence = result.Sequence;
            ActionType = result.ActionType;
            FailureReason = result.FailureReason;
            ActorUnitId = actorUnitId;
            TargetUnitId = targetUnitId;
            NextUnitId = nextUnitId;
            AbilityId = result.AbilityId;
            ChangeTypes = CopyChangeTypes(result.ChangeTypes);
            PositionBefore = result.PositionBefore;
            PositionAfter = result.PositionAfter;
            DurabilityBefore = result.DurabilityBefore;
            DurabilityAfter = result.DurabilityAfter;
            ActionPointBefore = result.ActionPointBefore;
            ActionPointAfter = result.ActionPointAfter;
            RoundNumberBefore = result.RoundNumberBefore;
            RoundNumberAfter = result.RoundNumberAfter;
            FactionBefore = result.FactionBefore;
            FactionAfter = result.FactionAfter;
        }

        private static void ValidateUnitIds(BattleActionResult result, int actorUnitId, int? targetUnitId, int? nextUnitId)
        {
            if (result.Sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(result), result.Sequence, "Action sequence must be greater than zero.");
            }

            if (actorUnitId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actorUnitId), actorUnitId, "Actor unit ID must be greater than zero.");
            }

            if (targetUnitId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetUnitId), targetUnitId, "Target unit ID must be greater than zero.");
            }

            if (nextUnitId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nextUnitId), nextUnitId, "Next unit ID must be greater than zero.");
            }

            if (result.AbilityId.HasValue && result.AbilityId.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(result), result.AbilityId,
                    "Ability ID must be greater than zero.");
            }

            var requiresTargetUnit = result.ActionType == BattleActionType.Attack ||
                                     result.ActionType == BattleActionType.UseAbility;
            if (requiresTargetUnit && targetUnitId == null)
            {
                throw new ArgumentException("Attack and ability logs must provide a target unit ID.",
                    nameof(targetUnitId));
            }

            if (!requiresTargetUnit && targetUnitId != null)
            {
                throw new ArgumentException("Only attack and ability logs can provide a target unit ID.",
                    nameof(targetUnitId));
            }

            if (result.ActionType == BattleActionType.UseAbility && result.AbilityId == null)
            {
                throw new ArgumentException("Ability log must provide an ability ID.", nameof(result));
            }

            if (result.ActionType != BattleActionType.UseAbility && result.AbilityId != null)
            {
                throw new ArgumentException("Only ability log can provide an ability ID.", nameof(result));
            }

            var requiresNextUnit = result.ActionType == BattleActionType.EndUnitAction ||
                                   result.ActionType == BattleActionType.EndFactionTurn;
            if (requiresNextUnit && nextUnitId == null)
            {
                throw new ArgumentException("Turn advancement log must provide the next unit ID.",
                    nameof(nextUnitId));
            }

            if (!requiresNextUnit && nextUnitId != null)
            {
                throw new ArgumentException("Only turn advancement logs can provide the next unit ID.",
                    nameof(nextUnitId));
            }
        }

        private static IReadOnlyList<BattleActionChangeType> CopyChangeTypes(IReadOnlyList<BattleActionChangeType> changeTypes)
        {
            var copy = new BattleActionChangeType[changeTypes.Count];
            for (var i = 0; i < changeTypes.Count; i++)
            {
                copy[i] = changeTypes[i];
            }

            return Array.AsReadOnly(copy);
        }
    }
}