using System;
using System.Collections.Generic;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Outcomes;
using MechStorm.Battle.Units;

namespace MechStorm.Battle.Actions
{
    /// <summary>
    /// 一次战斗操作的执行结果。
    /// </summary>
    public sealed class BattleActionResult
    {
        /// <summary>
        /// 失败操作使用的空变化类型集合。
        /// </summary>
        private static readonly IReadOnlyList<BattleActionChangeType> _emptyChangeTypes = Array.Empty<BattleActionChangeType>();

        /// <summary>
        /// 本次战斗会话中的操作序号。
        /// </summary>
        public int Sequence { get; }

        /// <summary>
        /// 操作是否执行成功。
        /// </summary>
        public bool IsSuccess => FailureReason == BattleActionFailureReason.None;

        /// <summary>
        /// 本次尝试执行的操作类型。
        /// </summary>
        public BattleActionType ActionType { get; }

        /// <summary>
        /// 操作失败原因，成功时为 None。
        /// </summary>
        public BattleActionFailureReason FailureReason { get; }

        /// <summary>
        /// 执行操作的单位。
        /// </summary>
        public CombatUnit ActorUnit { get; }

        /// <summary>
        /// 操作目标单位，无目标时为 null。
        /// </summary>
        public CombatUnit TargetUnit { get; }

        /// <summary>
        /// 本次尝试使用的技能 ID，非技能操作为 null。
        /// </summary>
        public int? AbilityId { get; }

        /// <summary>
        /// 行动结束后的下一个单位，其他操作为 null。
        /// </summary>
        public CombatUnit NextCombatUnit { get; }

        /// <summary>
        /// 本次操作产生的有序战斗变化类型。
        /// </summary>
        public IReadOnlyList<BattleActionChangeType> ChangeTypes { get; }

        /// <summary>
        /// 移动前的位置，非移动操作为 null。
        /// </summary>
        public Vector2Int? PositionBefore { get; }

        /// <summary>
        /// 移动后的位置，非移动操作为 null。
        /// </summary>
        public Vector2Int? PositionAfter { get; }

        /// <summary>
        /// 受到攻击或技能伤害前的耐久值，其他操作为 null。
        /// </summary>
        public int? DurabilityBefore { get; }

        /// <summary>
        /// 受到攻击或技能伤害后的耐久值，其他操作为 null。
        /// </summary>
        public int? DurabilityAfter { get; }

        /// <summary>
        /// 技能 Actor 消耗前或 NextCombatUnit 恢复前的 AP，其他操作为 null。
        /// </summary>
        public int? ActionPointBefore { get; }

        /// <summary>
        /// 技能 Actor 消耗后或 NextCombatUnit 恢复后的 AP，其他操作为 null。
        /// </summary>
        public int? ActionPointAfter { get; }

        /// <summary>
        /// 行动结束前的回合数，其他操作为 null。
        /// </summary>
        public int? RoundNumberBefore { get; }

        /// <summary>
        /// 行动结束后的回合数，其他操作为 null。
        /// </summary>
        public int? RoundNumberAfter { get; }

        /// <summary>
        /// 行动结束前的当前阵营，其他操作为 null。
        /// </summary>
        public CombatFaction? FactionBefore { get; }

        /// <summary>
        /// 行动结束后的当前阵营，其他操作为 null。
        /// </summary>
        public CombatFaction? FactionAfter { get; }

        /// <summary>
        /// 本次操作完成后的对局结果；普通结果为进行中，最后一击或结束后的失败结果为已结束。
        /// </summary>
        public BattleOutcome Outcome { get; }

        private BattleActionResult(int sequence, BattleActionType actionType, BattleActionFailureReason failureReason, CombatUnit actorUnit, CombatUnit targetUnit, CombatUnit nextCombatUnit,
            IReadOnlyList<BattleActionChangeType> changeTypes, Vector2Int? positionBefore = null, Vector2Int? positionAfter = null, int? durabilityBefore = null, int? durabilityAfter = null,
            int? abilityId = null, int? actionPointBefore = null, int? actionPointAfter = null, int? roundNumberBefore = null, int? roundNumberAfter = null,
            CombatFaction? factionBefore = null, CombatFaction? factionAfter = null, BattleOutcome outcome = null)
        {
            Sequence = sequence;
            ActionType = actionType;
            FailureReason = failureReason;
            ActorUnit = actorUnit;
            TargetUnit = targetUnit;
            NextCombatUnit = nextCombatUnit;
            ChangeTypes = changeTypes;
            PositionBefore = positionBefore;
            PositionAfter = positionAfter;
            DurabilityBefore = durabilityBefore;
            DurabilityAfter = durabilityAfter;
            AbilityId = abilityId;
            ActionPointBefore = actionPointBefore;
            ActionPointAfter = actionPointAfter;
            RoundNumberBefore = roundNumberBefore;
            RoundNumberAfter = roundNumberAfter;
            FactionBefore = factionBefore;
            FactionAfter = factionAfter;
            Outcome = outcome ?? BattleOutcome.InProgress;
            ValidateOutcome(actionType, failureReason, durabilityAfter, Outcome);
        }

        /// <summary>
        /// 创建移动成功结果。
        /// </summary>
        /// <param name="sequence">操作序号。</param>
        /// <param name="actorUnit">执行移动的单位。</param>
        /// <param name="positionBefore">移动前的位置。</param>
        /// <param name="positionAfter">移动后的位置。</param>
        public static BattleActionResult MoveSucceeded(int sequence, CombatUnit actorUnit, Vector2Int positionBefore, Vector2Int positionAfter)
        {
            if (actorUnit == null)
            {
                throw new ArgumentNullException(nameof(actorUnit));
            }

            return new BattleActionResult(sequence, BattleActionType.Move, BattleActionFailureReason.None, actorUnit,
                null, null, new[] { BattleActionChangeType.UnitMoved }, positionBefore, positionAfter);
        }

        /// <summary>
        /// 创建阵营内行动单位切换成功结果，并携带上一单位可能发生的位置回退。
        /// </summary>
        public static BattleActionResult UnitSelectionSucceeded(int sequence, CombatUnit actorUnit,
            CombatUnit selectedCombatUnit, Vector2Int positionBefore, Vector2Int positionAfter,
            int actionPointBefore, int actionPointAfter)
        {
            if (actorUnit == null)
            {
                throw new ArgumentNullException(nameof(actorUnit));
            }

            if (selectedCombatUnit == null)
            {
                throw new ArgumentNullException(nameof(selectedCombatUnit));
            }

            if (actionPointBefore < 0 || actionPointAfter < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionPointBefore),
                    "Action points must be non-negative.");
            }

            var changeTypes = new List<BattleActionChangeType>();
            if (positionBefore != positionAfter)
            {
                changeTypes.Add(BattleActionChangeType.UnitPositionReset);
            }

            if (!ReferenceEquals(actorUnit, selectedCombatUnit))
            {
                changeTypes.Add(BattleActionChangeType.CurrentUnitChanged);
            }

            if (actionPointBefore != actionPointAfter)
            {
                changeTypes.Add(BattleActionChangeType.ActionPointRestored);
            }

            return new BattleActionResult(sequence, BattleActionType.SelectUnit,
                BattleActionFailureReason.None, actorUnit, null, selectedCombatUnit,
                changeTypes.ToArray(), positionBefore, positionAfter,
                actionPointBefore: actionPointBefore, actionPointAfter: actionPointAfter);
        }

        /// <summary>
        /// 创建攻击成功结果。
        /// </summary>
        /// <param name="sequence">操作序号。</param>
        /// <param name="actorUnit">执行攻击的单位。</param>
        /// <param name="targetUnit">受到攻击的目标单位。</param>
        /// <param name="durabilityBefore">目标受击前的耐久值。</param>
        /// <param name="durabilityAfter">目标受击后的耐久值。</param>
        /// <param name="outcome">攻击完成后的对局结果。</param>
        public static BattleActionResult AttackSucceeded(int sequence, CombatUnit actorUnit,
            CombatUnit targetUnit, int durabilityBefore, int durabilityAfter,
            BattleOutcome outcome = null)
        {
            if (actorUnit == null)
            {
                throw new ArgumentNullException(nameof(actorUnit));
            }

            if (targetUnit == null)
            {
                throw new ArgumentNullException(nameof(targetUnit));
            }

            var changeTypes = new List<BattleActionChangeType>
            {
                BattleActionChangeType.DamageApplied
            };
            if (durabilityAfter <= 0)
            {
                changeTypes.Add(BattleActionChangeType.UnitDied);
            }

            AppendBattleOutcomeChanges(changeTypes, outcome);

            return new BattleActionResult(sequence, BattleActionType.Attack, BattleActionFailureReason.None, actorUnit, targetUnit,
                null, changeTypes.ToArray(), durabilityBefore: durabilityBefore,
                durabilityAfter: durabilityAfter, outcome: outcome);
        }

        /// <summary>
        /// 创建技能成功结果，并按 AP、伤害、死亡和对局结束的顺序记录变化。
        /// </summary>
        public static BattleActionResult AbilitySucceeded(int sequence, CombatUnit actorUnit,
            CombatUnit targetUnit, int abilityId, int actionPointBefore, int actionPointAfter,
            int durabilityBefore, int durabilityAfter, BattleOutcome outcome = null)
        {
            if (actorUnit == null)
            {
                throw new ArgumentNullException(nameof(actorUnit));
            }

            if (targetUnit == null)
            {
                throw new ArgumentNullException(nameof(targetUnit));
            }

            if (abilityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(abilityId), abilityId,
                    "Ability ID must be greater than zero.");
            }

            if (actionPointBefore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionPointBefore), actionPointBefore,
                    "Action point before ability use must be non-negative.");
            }

            if (actionPointAfter < 0 || actionPointAfter > actionPointBefore)
            {
                throw new ArgumentOutOfRangeException(nameof(actionPointAfter), actionPointAfter,
                    "Action point after ability use must be between zero and the previous value.");
            }

            var changeTypes = new List<BattleActionChangeType>();
            if (actionPointBefore != actionPointAfter)
            {
                changeTypes.Add(BattleActionChangeType.ActionPointSpent);
            }

            if (durabilityBefore != durabilityAfter)
            {
                changeTypes.Add(BattleActionChangeType.DamageApplied);
                if (durabilityAfter <= 0)
                {
                    changeTypes.Add(BattleActionChangeType.UnitDied);
                }
            }

            AppendBattleOutcomeChanges(changeTypes, outcome);

            return new BattleActionResult(sequence, BattleActionType.UseAbility,
                BattleActionFailureReason.None, actorUnit, targetUnit, null, changeTypes.ToArray(),
                durabilityBefore: durabilityBefore, durabilityAfter: durabilityAfter, abilityId: abilityId,
                actionPointBefore: actionPointBefore, actionPointAfter: actionPointAfter,
                outcome: outcome);
        }

        /// <summary>
        /// 创建单位行动结束结果。
        /// </summary>
        /// <param name="sequence">操作序号。</param>
        /// <param name="actorUnit">结束行动的单位。</param>
        /// <param name="nextCombatUnit">下一个行动单位。</param>
        /// <param name="roundNumberBefore">行动结束前的回合数。</param>
        /// <param name="roundNumberAfter">行动结束后的回合数。</param>
        /// <param name="factionBefore">行动结束前的当前阵营。</param>
        /// <param name="factionAfter">行动结束后的当前阵营。</param>
        /// <param name="actionPointBefore">下一行动单位恢复前的 AP。</param>
        /// <param name="actionPointAfter">下一行动单位恢复后的 AP。</param>
        public static BattleActionResult ActionEnded(int sequence, CombatUnit actorUnit, CombatUnit nextCombatUnit, int roundNumberBefore,
            int roundNumberAfter, CombatFaction factionBefore, CombatFaction factionAfter, int actionPointBefore,
            int actionPointAfter)
        {
            if (actorUnit == null)
            {
                throw new ArgumentNullException(nameof(actorUnit));
            }

            if (nextCombatUnit == null)
            {
                throw new ArgumentNullException(nameof(nextCombatUnit));
            }

            if (actionPointBefore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionPointBefore), actionPointBefore,
                    "Action point before restore must be non-negative.");
            }

            if (actionPointAfter < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionPointAfter), actionPointAfter,
                    "Action point after restore must be non-negative.");
            }

            var changeTypes = new List<BattleActionChangeType>
            {
                BattleActionChangeType.UnitActionEnded
            };

            if (factionBefore != factionAfter)
            {
                changeTypes.Add(BattleActionChangeType.FactionTurnEnded);
            }

            if (roundNumberBefore != roundNumberAfter)
            {
                changeTypes.Add(BattleActionChangeType.RoundStarted);
            }

            if (factionBefore != factionAfter)
            {
                changeTypes.Add(BattleActionChangeType.FactionTurnStarted);
            }

            changeTypes.Add(BattleActionChangeType.CurrentUnitChanged);
            if (actionPointBefore != actionPointAfter)
            {
                changeTypes.Add(BattleActionChangeType.ActionPointRestored);
            }

            return new BattleActionResult(sequence, BattleActionType.EndUnitAction, BattleActionFailureReason.None, actorUnit, null,
                nextCombatUnit, changeTypes.ToArray(), roundNumberBefore: roundNumberBefore, roundNumberAfter: roundNumberAfter,
                factionBefore: factionBefore, factionAfter: factionAfter, actionPointBefore: actionPointBefore,
                actionPointAfter: actionPointAfter);
        }

        /// <summary>
        /// 创建当前阵营回合结束结果。
        /// </summary>
        public static BattleActionResult FactionTurnEnded(
            int sequence,
            CombatUnit actorUnit,
            CombatUnit nextCombatUnit,
            int roundNumberBefore,
            int roundNumberAfter,
            CombatFaction factionBefore,
            CombatFaction factionAfter,
            int actionPointBefore,
            int actionPointAfter,
            Vector2Int? positionBefore = null,
            Vector2Int? positionAfter = null)
        {
            if (actorUnit == null)
            {
                throw new ArgumentNullException(nameof(actorUnit));
            }

            if (nextCombatUnit == null)
            {
                throw new ArgumentNullException(nameof(nextCombatUnit));
            }

            if (actionPointBefore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionPointBefore), actionPointBefore,
                    "Action point before restore must be non-negative.");
            }

            if (actionPointAfter < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionPointAfter), actionPointAfter,
                    "Action point after restore must be non-negative.");
            }

            var changeTypes = new List<BattleActionChangeType>
            {
                BattleActionChangeType.UnitActionEnded,
                BattleActionChangeType.FactionTurnEnded
            };

            if (positionBefore != positionAfter)
            {
                changeTypes.Insert(0, BattleActionChangeType.UnitPositionReset);
            }

            if (roundNumberBefore != roundNumberAfter)
            {
                changeTypes.Add(BattleActionChangeType.RoundStarted);
            }

            changeTypes.Add(BattleActionChangeType.FactionTurnStarted);
            changeTypes.Add(BattleActionChangeType.CurrentUnitChanged);
            if (actionPointBefore != actionPointAfter)
            {
                changeTypes.Add(BattleActionChangeType.ActionPointRestored);
            }

            return new BattleActionResult(sequence, BattleActionType.EndFactionTurn,
                BattleActionFailureReason.None, actorUnit, null, nextCombatUnit, changeTypes.ToArray(),
                positionBefore: positionBefore, positionAfter: positionAfter,
                roundNumberBefore: roundNumberBefore, roundNumberAfter: roundNumberAfter,
                factionBefore: factionBefore, factionAfter: factionAfter,
                actionPointBefore: actionPointBefore, actionPointAfter: actionPointAfter);
        }

        /// <summary>
        /// 创建基础攻击失败结果，并保留请求中的目标身份和失败后的对局结果。
        /// </summary>
        public static BattleActionResult AttackFailed(int sequence, CombatUnit actorUnit,
            CombatUnit targetUnit, BattleActionFailureReason failureReason,
            BattleOutcome outcome = null)
        {
            if (actorUnit == null)
            {
                throw new ArgumentNullException(nameof(actorUnit));
            }

            if (targetUnit == null)
            {
                throw new ArgumentNullException(nameof(targetUnit));
            }

            if (failureReason == BattleActionFailureReason.None)
            {
                throw new ArgumentException("A failed attack must provide a failure reason.",
                    nameof(failureReason));
            }

            return new BattleActionResult(sequence, BattleActionType.Attack, failureReason, actorUnit,
                targetUnit, null, _emptyChangeTypes, outcome: outcome);
        }

        /// <summary>
        /// 创建技能失败结果，并保留请求中的技能、目标身份和失败后的对局结果。
        /// </summary>
        public static BattleActionResult AbilityFailed(int sequence, CombatUnit actorUnit,
            CombatUnit targetUnit, int abilityId, BattleActionFailureReason failureReason,
            BattleOutcome outcome = null)
        {
            if (actorUnit == null)
            {
                throw new ArgumentNullException(nameof(actorUnit));
            }

            if (targetUnit == null)
            {
                throw new ArgumentNullException(nameof(targetUnit));
            }

            if (abilityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(abilityId), abilityId,
                    "Ability ID must be greater than zero.");
            }

            if (failureReason == BattleActionFailureReason.None)
            {
                throw new ArgumentException("A failed ability must provide a failure reason.",
                    nameof(failureReason));
            }

            return new BattleActionResult(sequence, BattleActionType.UseAbility, failureReason, actorUnit,
                targetUnit, null, _emptyChangeTypes, abilityId: abilityId, outcome: outcome);
        }

        /// <summary>
        /// 创建操作失败结果。
        /// </summary>
        /// <param name="sequence">操作序号。</param>
        /// <param name="actionType">尝试执行的操作类型。</param>
        /// <param name="actorUnit">尝试执行操作的单位。</param>
        /// <param name="failureReason">操作失败原因。</param>
        /// <param name="outcome">失败发生时的对局结果。</param>
        public static BattleActionResult Failed(int sequence, BattleActionType actionType,
            CombatUnit actorUnit, BattleActionFailureReason failureReason,
            BattleOutcome outcome = null)
        {
            if (actorUnit == null)
            {
                throw new ArgumentNullException(nameof(actorUnit));
            }

            if (failureReason == BattleActionFailureReason.None)
            {
                throw new ArgumentException("A failed action must provide a failure reason.", nameof(failureReason));
            }

            if (actionType == BattleActionType.Attack || actionType == BattleActionType.UseAbility)
            {
                throw new ArgumentException(
                    "Targeted actions must use their dedicated failure result factory.",
                    nameof(actionType));
            }

            return new BattleActionResult(sequence, actionType, failureReason, actorUnit, null, null,
                _emptyChangeTypes, outcome: outcome);
        }

        /// <summary>
        /// 约束 Result 与 Outcome 的组合：只有致死伤害能成功结束对局；
        /// 已结束后的失败操作必须返回 BattleAlreadyEnded。
        /// </summary>
        private static void ValidateOutcome(
            BattleActionType actionType, BattleActionFailureReason failureReason,
            int? durabilityAfter, BattleOutcome outcome)
        {
            var isSuccess = failureReason == BattleActionFailureReason.None;
            if (failureReason == BattleActionFailureReason.BattleAlreadyEnded &&
                !outcome.IsBattleEnded)
            {
                throw new ArgumentException(
                    "BattleAlreadyEnded failure must provide a completed battle outcome.",
                    nameof(outcome));
            }

            if (!outcome.IsBattleEnded)
            {
                return;
            }

            if (!isSuccess)
            {
                if (failureReason != BattleActionFailureReason.BattleAlreadyEnded)
                {
                    throw new ArgumentException(
                        "A failed action can carry a completed outcome only when the battle already ended.",
                        nameof(outcome));
                }

                return;
            }

            var isDamageAction = actionType == BattleActionType.Attack ||
                                 actionType == BattleActionType.UseAbility;
            if (!isDamageAction || durabilityAfter != 0)
            {
                throw new ArgumentException(
                    "A successful action can complete the battle only through lethal damage.",
                    nameof(outcome));
            }
        }

        /// <summary>
        /// 对局结束变化必须排在具体伤害和死亡变化之后，便于表现层按结算时序消费。
        /// </summary>
        private static void AppendBattleOutcomeChanges(
            ICollection<BattleActionChangeType> changeTypes, BattleOutcome outcome)
        {
            if (outcome?.IsBattleEnded != true)
            {
                return;
            }

            changeTypes.Add(BattleActionChangeType.FactionEliminated);
            changeTypes.Add(BattleActionChangeType.BattleEnded);
        }
    }
}