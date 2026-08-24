using System;
using System.Collections.Generic;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Outcomes;
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

        /// <summary>
        /// 操作完成后的对局结果快照。
        /// </summary>
        public BattleOutcome Outcome { get; }

        /// <summary>
        /// 将一次 Result 复制成只保留稳定 ID 和数值的日志，避免日志依赖后续变化的单位对象。
        /// </summary>
        public BattleActionLog(BattleActionResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            ValidateResult(result);

            Sequence = result.Sequence;
            ActionType = result.ActionType;
            FailureReason = result.FailureReason;
            ActorUnitId = result.ActorUnit.UnitId;
            TargetUnitId = result.TargetUnit?.UnitId;
            NextUnitId = result.NextCombatUnit?.UnitId;
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
            Outcome = result.Outcome;
        }

        private static void ValidateResult(BattleActionResult result)
        {
            if (result.Sequence <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(result), result.Sequence, "Action sequence must be greater than zero.");
            }

            if (result.ActorUnit == null || result.ActorUnit.UnitId <= 0)
            {
                throw new ArgumentException("Result must provide a valid actor unit.", nameof(result));
            }

            if (result.TargetUnit?.UnitId <= 0)
            {
                throw new ArgumentException("Result target unit ID must be greater than zero.", nameof(result));
            }

            if (result.NextCombatUnit?.UnitId <= 0)
            {
                throw new ArgumentException("Result next unit ID must be greater than zero.", nameof(result));
            }

            if (result.AbilityId.HasValue && result.AbilityId.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(result), result.AbilityId,
                    "Ability ID must be greater than zero.");
            }

            var requiresTargetUnit = result.ActionType == BattleActionType.Attack ||
                                     result.ActionType == BattleActionType.UseAbility;
            if (requiresTargetUnit && result.TargetUnit == null)
            {
                throw new ArgumentException("Attack and ability results must provide a target unit.",
                    nameof(result));
            }

            if (!requiresTargetUnit && result.TargetUnit != null)
            {
                throw new ArgumentException("Only attack and ability results can provide a target unit.",
                    nameof(result));
            }

            if (result.ActionType == BattleActionType.UseAbility && result.AbilityId == null)
            {
                throw new ArgumentException("Ability log must provide an ability ID.", nameof(result));
            }

            if (result.ActionType != BattleActionType.UseAbility && result.AbilityId != null)
            {
                throw new ArgumentException("Only ability log can provide an ability ID.", nameof(result));
            }

            // 成功切换行动者或推进回合时才存在下一单位。
            var changesCurrentUnit = result.ActionType == BattleActionType.SelectUnit ||
                                     result.ActionType == BattleActionType.EndUnitAction ||
                                     result.ActionType == BattleActionType.EndFactionTurn;
            var requiresNextUnit = result.IsSuccess && changesCurrentUnit;
            if (requiresNextUnit && result.NextCombatUnit == null)
            {
                throw new ArgumentException("Successful current unit change result must provide the next unit.",
                    nameof(result));
            }

            if ((!changesCurrentUnit || !result.IsSuccess) && result.NextCombatUnit != null)
            {
                throw new ArgumentException("Only successful current unit change results can provide the next unit.",
                    nameof(result));
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