using System;
using System.Collections.Generic;
using MechStorm.Battle.Actions;
using MechStorm.Battle.Data;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Outcomes;
using MechStorm.Battle.Rules;
using MechStorm.Battle.Snapshots;
using MechStorm.Battle.Spatial;
using MechStorm.Battle.Turns;
using MechStorm.Battle.Units;

namespace MechStorm.Battle
{
    public sealed class BattleSession
    {
        private readonly SquareGrid _squareGrid;
        private readonly MovementResolver _movementResolver;
        private readonly AttackResolver _attackResolver;
        private readonly AbilityResolver _abilityResolver;
        private readonly CombatUnitRegistry _unitRegistry;
        private readonly TurnCoordinator _turnCoordinator;
        private readonly List<BattleActionLog> _actionLogs;
        private readonly IReadOnlyList<BattleActionLog> _readOnlyActionLogs;

        private int _actionSequence;
        private BattleOutcome _outcome;
        private Vector2Int _currentActionStartPosition;

        public BattleSession(int boardWidth, int boardHeight, IEnumerable<CombatUnit> teamAUnits, IEnumerable<CombatUnit> teamBUnits, IEnumerable<CombatUnit> neutralUnits = null)
        {
            _actionLogs = new List<BattleActionLog>();
            _readOnlyActionLogs = _actionLogs.AsReadOnly();
            _squareGrid = new SquareGrid(boardWidth, boardHeight);
            _unitRegistry = new CombatUnitRegistry(teamAUnits, teamBUnits, neutralUnits);
            _movementResolver = new MovementResolver(_squareGrid, IsPositionOccupied);
            _attackResolver = new AttackResolver(_squareGrid);
            _abilityResolver = new AbilityResolver(_squareGrid, _unitRegistry);
            ValidateUnitPositions(_unitRegistry.GetFactionUnits(CombatFaction.TeamA), nameof(teamAUnits));
            ValidateUnitPositions(_unitRegistry.GetFactionUnits(CombatFaction.TeamB), nameof(teamBUnits));
            ValidateUnitPositions(_unitRegistry.GetFactionUnits(CombatFaction.Neutral), nameof(neutralUnits));
            ValidateAliveUnitPositionsDoNotOverlap();
            _turnCoordinator = new TurnCoordinator(_unitRegistry);
            _currentActionStartPosition = CurrentCombatUnit.Position;
            _outcome = BattleOutcome.InProgress;
        }

        public SquareGrid Grid => _squareGrid;

        public IReadOnlyList<CombatUnit> CombatUnits => _unitRegistry.CombatUnits;

        public int CurrentRoundNumber => _turnCoordinator.CurrentRoundNumber;

        public CombatFaction CurrentFaction => _turnCoordinator.CurrentFaction;

        public CombatUnit CurrentCombatUnit => _turnCoordinator.CurrentCombatUnit;

        /// <summary>
        /// 当前机兵本次行动的移动范围锚点；暂定走位不会改变该位置。
        /// </summary>
        public Vector2Int CurrentActionStartPosition => _currentActionStartPosition;

        /// <summary>
        /// 是否已经产生最终胜负。结束后所有改变战场或推进回合的入口都会失败。
        /// </summary>
        public bool IsBattleEnded => _outcome.IsBattleEnded;

        /// <summary>
        /// 当前对局结果，包含胜方、全灭方和结束原因。
        /// </summary>
        public BattleOutcome Outcome => _outcome;

        public IReadOnlyList<BattleActionLog> ActionLogs => _readOnlyActionLogs;

        /// <summary>
        /// 复制当前棋盘、单位、回合和胜负结果，供诊断导出使用。
        /// </summary>
        public BattleSnapshot CreateSnapshot()
        {
            var unitSnapshots = new List<BattleUnitSnapshot>(CombatUnits.Count);
            foreach (var unit in CombatUnits)
            {
                unitSnapshots.Add(new BattleUnitSnapshot(unit.UnitId, _unitRegistry.GetFaction(unit), unit.Position,
                    unit.MechRuntime.CurrentDurability, unit.Mech.MaxDurability,
                    unit.PilotRuntime.CurrentActionPoint, unit.Pilot.MaxActionPoint,
                    _turnCoordinator.HasUnitActed(unit)));
            }

            return new BattleSnapshot(Grid.Width, Grid.Height, CurrentRoundNumber, CurrentFaction,
                CurrentCombatUnit.UnitId, unitSnapshots, _outcome, _currentActionStartPosition);
        }

        /// <summary>
        /// 判断指定机兵能否在当前阵营回合内被选为行动者。
        /// </summary>
        public bool CanSelectCurrentFactionCombatUnit(CombatUnit combatUnit)
        {
            return !IsBattleEnded && _turnCoordinator.CanSelectCombatUnit(combatUnit);
        }

        /// <summary>
        /// 切换到当前阵营未行动机兵，并将上一机兵回退到其行动开始位置。
        /// </summary>
        public BattleActionResult TrySelectCurrentFactionCombatUnit(CombatUnit combatUnit)
        {
            if (combatUnit == null)
            {
                throw new ArgumentNullException(nameof(combatUnit));
            }

            if (!_unitRegistry.Contains(combatUnit))
            {
                throw new ArgumentException(
                    "Selected unit does not belong to this battle session.",
                    nameof(combatUnit));
            }

            var actorUnit = CurrentCombatUnit;
            var sequence = ++_actionSequence;
            if (IsBattleEnded)
            {
                return RecordBattleEndedFailure(
                    sequence, BattleActionType.SelectUnit, actorUnit);
            }

            if (!_turnCoordinator.CanSelectCombatUnit(combatUnit))
            {
                var failedResult = BattleActionResult.Failed(sequence,
                    BattleActionType.SelectUnit, actorUnit,
                    BattleActionFailureReason.InvalidUnitSelection);
                return RecordAction(failedResult);
            }

            var positionBefore = actorUnit.Position;
            var actionPointBefore = combatUnit.PilotRuntime.CurrentActionPoint;
            if (!ReferenceEquals(actorUnit, combatUnit))
            {
                actorUnit.MoveTo(_currentActionStartPosition);
                _turnCoordinator.SelectCombatUnit(combatUnit);
                _currentActionStartPosition = combatUnit.Position;
            }

            var result = BattleActionResult.UnitSelectionSucceeded(sequence, actorUnit,
                combatUnit, positionBefore, actorUnit.Position, actionPointBefore,
                combatUnit.PilotRuntime.CurrentActionPoint);
            return RecordAction(result);
        }

        /// <summary>
        /// 尝试移动当前单位；对局结束后只记录失败，不改变位置。
        /// </summary>
        public BattleActionResult TryMoveCurrentCombatUnit(Vector2Int target)
        {
            var actorUnit = CurrentCombatUnit;
            var sequence = ++_actionSequence;
            if (IsBattleEnded)
            {
                return RecordBattleEndedFailure(sequence, BattleActionType.Move, actorUnit);
            }

            var positionBefore = actorUnit.Position;
            var result = !_movementResolver.TryMoveTo(
                    actorUnit, _currentActionStartPosition, target)
                ? BattleActionResult.Failed(sequence, BattleActionType.Move, actorUnit, BattleActionFailureReason.InvalidMoveTarget)
                : BattleActionResult.MoveSucceeded(sequence, actorUnit, positionBefore, actorUnit.Position);
            return RecordAction(result);
        }

        /// <summary>
        /// 尝试执行普通攻击，并在伤害结算后检查目标阵营是否全灭。
        /// </summary>
        public BattleActionResult AttackTargetCombatUnit(CombatUnit targetUnit)
        {
            if (targetUnit == null)
            {
                throw new ArgumentNullException(nameof(targetUnit));
            }

            if (!_unitRegistry.Contains(targetUnit))
            {
                throw new ArgumentException("Target unit does not belong to this battle session.", nameof(targetUnit));
            }

            var attackerUnit = CurrentCombatUnit;
            var sequence = ++_actionSequence;
            if (IsBattleEnded)
            {
                var endedResult = BattleActionResult.AttackFailed(
                    sequence, attackerUnit, targetUnit,
                    BattleActionFailureReason.BattleAlreadyEnded, _outcome);
                return RecordAction(endedResult);
            }

            var failureReason = GetBasicAttackFailureReason(attackerUnit, targetUnit);
            if (failureReason != BattleActionFailureReason.None)
            {
                var result = BattleActionResult.AttackFailed(
                    sequence, attackerUnit, targetUnit, failureReason);
                return RecordAction(result);
            }

            var actionPointBefore = attackerUnit.PilotRuntime.CurrentActionPoint;
            var durabilityBefore = targetUnit.MechRuntime.CurrentDurability;
            if (!_attackResolver.TryAttack(attackerUnit, targetUnit))
            {
                throw new InvalidOperationException("Validated basic attack could not be executed.");
            }

            // 伤害提交后立即判定全灭，使最后一击结果同时携带死亡与对局结束事实。
            UpdateOutcomeAfterDamage(targetUnit);
            var successResult = BattleActionResult.AttackSucceeded(sequence, attackerUnit, targetUnit,
                actionPointBefore, attackerUnit.PilotRuntime.CurrentActionPoint,
                durabilityBefore, targetUnit.MechRuntime.CurrentDurability, _outcome);
            RecordAction(successResult);
            if (!IsBattleEnded)
            {
                EndCurrentUnitAction();
            }

            return successResult;
        }

        /// <summary>
        /// 尝试释放单体技能，并在 AP 与伤害结算后检查目标阵营是否全灭。
        /// </summary>
        public BattleActionResult TryUseAbility(AbilityDefinition ability, CombatUnit targetUnit,
            AbilityTargetRangeSource rangeSource = AbilityTargetRangeSource.Ability)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            if (targetUnit == null)
            {
                throw new ArgumentNullException(nameof(targetUnit));
            }

            if (!_unitRegistry.Contains(targetUnit))
            {
                throw new ArgumentException("Target unit does not belong to this battle session.",
                    nameof(targetUnit));
            }

            var actorUnit = CurrentCombatUnit;
            var sequence = ++_actionSequence;
            if (IsBattleEnded)
            {
                var endedResult = BattleActionResult.AbilityFailed(
                    sequence, actorUnit, targetUnit, ability.AbilityId,
                    BattleActionFailureReason.BattleAlreadyEnded, _outcome);
                return RecordAction(endedResult);
            }

            var failureReason = GetAbilityFailureReason(
                ability,
                actorUnit,
                targetUnit,
                rangeSource);
            if (failureReason != BattleActionFailureReason.None)
            {
                var failedResult = BattleActionResult.AbilityFailed(sequence, actorUnit, targetUnit,
                    ability.AbilityId, failureReason);
                return RecordAction(failedResult);
            }

            var actionPointBefore = actorUnit.PilotRuntime.CurrentActionPoint;
            var durabilityBefore = targetUnit.MechRuntime.CurrentDurability;
            if (!_abilityResolver.TryResolveAbility(
                    ability,
                    actorUnit,
                    targetUnit,
                    rangeSource))
            {
                throw new InvalidOperationException("Validated ability could not be executed.");
            }

            // 与普通攻击保持同一时序：先提交伤害，再把可能产生的胜负写入技能 Result。
            UpdateOutcomeAfterDamage(targetUnit);
            var successResult = BattleActionResult.AbilitySucceeded(sequence, actorUnit, targetUnit,
                ability.AbilityId, actionPointBefore, actorUnit.PilotRuntime.CurrentActionPoint,
                durabilityBefore, targetUnit.MechRuntime.CurrentDurability, _outcome);
            RecordAction(successResult);
            if (!IsBattleEnded)
            {
                EndCurrentUnitAction();
            }

            return successResult;
        }

        /// <summary>
        /// 结束当前单位行动并切换到下一单位；对局结束后不再推进回合。
        /// </summary>
        public BattleActionResult EndCurrentUnitAction()
        {
            var actorUnit = CurrentCombatUnit;
            var sequence = ++_actionSequence;
            if (IsBattleEnded)
            {
                return RecordBattleEndedFailure(
                    sequence, BattleActionType.EndUnitAction, actorUnit);
            }

            var roundNumberBefore = CurrentRoundNumber;
            var factionBefore = CurrentFaction;
            var actionPointsBefore = CaptureActionPoints();

            _turnCoordinator.EndCurrentUnitAction();

            var nextCombatUnit = CurrentCombatUnit;
            _currentActionStartPosition = nextCombatUnit.Position;
            var result = BattleActionResult.ActionEnded(sequence, actorUnit, nextCombatUnit, roundNumberBefore,
                CurrentRoundNumber, factionBefore, CurrentFaction, actionPointsBefore[nextCombatUnit.UnitId],
                nextCombatUnit.PilotRuntime.CurrentActionPoint);
            return RecordAction(result);
        }

        /// <summary>
        /// 结束当前阵营回合；对局结束后不再切换阵营。
        /// </summary>
        public BattleActionResult EndCurrentFactionTurn()
        {
            var actorUnit = CurrentCombatUnit;
            var sequence = ++_actionSequence;
            if (IsBattleEnded)
            {
                return RecordBattleEndedFailure(
                    sequence, BattleActionType.EndFactionTurn, actorUnit);
            }

            var roundNumberBefore = CurrentRoundNumber;
            var factionBefore = CurrentFaction;
            var actionPointsBefore = CaptureActionPoints();
            var positionBefore = actorUnit.Position;
            actorUnit.MoveTo(_currentActionStartPosition);

            _turnCoordinator.EndCurrentFactionTurn();

            var nextCombatUnit = CurrentCombatUnit;
            _currentActionStartPosition = nextCombatUnit.Position;
            var result = BattleActionResult.FactionTurnEnded(
                sequence, actorUnit, nextCombatUnit, roundNumberBefore, CurrentRoundNumber,
                factionBefore, CurrentFaction, actionPointsBefore[nextCombatUnit.UnitId],
                nextCombatUnit.PilotRuntime.CurrentActionPoint, positionBefore, actorUnit.Position);
            return RecordAction(result);
        }

        public IReadOnlyList<CombatUnit> GetCurrentFactionCombatUnits()
        {
            return GetFactionCombatUnits(CurrentFaction);
        }

        public IReadOnlyList<CombatUnit> GetFactionCombatUnits(CombatFaction faction)
        {
            return _unitRegistry.GetFactionUnits(faction);
        }

        public CombatFaction GetCombatUnitFaction(CombatUnit combatUnit)
        {
            return _unitRegistry.GetFaction(combatUnit);
        }

        public IReadOnlyList<CombatUnit> GetAllAliveCombatUnits()
        {
            return _unitRegistry.GetAliveUnits();
        }

        public IReadOnlyList<CombatUnit> GetAllDeadCombatUnits()
        {
            return _unitRegistry.GetDeadUnits();
        }

        /// <summary>
        /// 查询当前单位可移动格；对局结束后返回空集合以移除移动高亮。
        /// </summary>
        public IReadOnlyList<Vector2Int> GetCurrentCombatUnitReachablePositions()
        {
            if (IsBattleEnded)
            {
                return Array.Empty<Vector2Int>();
            }

            return _movementResolver.GetReachablePositions(
                CurrentCombatUnit, _currentActionStartPosition);
        }

        /// <summary>
        /// 查询当前单位合法普通攻击目标；对局结束后返回空集合。
        /// </summary>
        public IReadOnlyList<CombatUnit> GetCurrentCombatUnitBasicAttackTargets()
        {
            if (IsBattleEnded)
            {
                return Array.Empty<CombatUnit>();
            }

            var attackTargets = new List<CombatUnit>();
            var attackerUnit = CurrentCombatUnit;
            foreach (var targetUnit in CombatUnits)
            {
                if (GetBasicAttackFailureReason(attackerUnit, targetUnit) == BattleActionFailureReason.None)
                {
                    attackTargets.Add(targetUnit);
                }
            }

            return attackTargets;
        }

        /// <summary>
        /// 查询当前单位普通攻击覆盖格；对局结束后返回空集合以移除攻击高亮。
        /// </summary>
        public IReadOnlyList<Vector2Int> GetCurrentCombatUnitBasicAttackPositions()
        {
            if (IsBattleEnded)
            {
                return Array.Empty<Vector2Int>();
            }

            return _attackResolver.GetPositionsInRange(CurrentCombatUnit);
        }

        /// <summary>
        /// 查询技能覆盖格；对局结束后返回空集合以移除技能高亮。
        /// </summary>
        public IReadOnlyList<Vector2Int> GetCurrentCombatUnitAbilityPositions(
            AbilityDefinition ability,
            AbilityTargetRangeSource rangeSource = AbilityTargetRangeSource.Ability)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            if (IsBattleEnded)
            {
                return Array.Empty<Vector2Int>();
            }

            return _abilityResolver.GetPositionsInRange(
                ability,
                CurrentCombatUnit,
                rangeSource);
        }

        /// <summary>
        /// 查询技能合法目标；对局结束后返回空集合。
        /// </summary>
        public IReadOnlyList<CombatUnit> GetCurrentCombatUnitAbilityTargets(
            AbilityDefinition ability,
            AbilityTargetRangeSource rangeSource = AbilityTargetRangeSource.Ability)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            if (IsBattleEnded)
            {
                return Array.Empty<CombatUnit>();
            }

            var targets = new List<CombatUnit>();
            var sourceUnit = CurrentCombatUnit;
            foreach (var targetUnit in CombatUnits)
            {
                if (_abilityResolver.CanUseAbility(
                        ability,
                        sourceUnit,
                        targetUnit,
                        rangeSource))
                {
                    targets.Add(targetUnit);
                }
            }

            return targets;
        }

        public bool TryGetAliveCombatUnitAt(Vector2Int position, out CombatUnit combatUnit)
        {
            ValidateGridPosition(position, nameof(position));

            foreach (var unit in CombatUnits)
            {
                if (unit.IsAlive() && unit.Position == position)
                {
                    combatUnit = unit;
                    return true;
                }
            }

            combatUnit = null;
            return false;
        }

        public bool IsPositionOccupied(Vector2Int position)
        {
            return TryGetAliveCombatUnitAt(position, out _);
        }

        public bool AreFactionUnitsDead(CombatFaction faction)
        {
            return _unitRegistry.AreFactionUnitsDead(faction);
        }

        /// <summary>
        /// 只有 TeamA 或 TeamB 的最后一名机兵死亡才会完成对局，Neutral 不参与胜负。
        /// </summary>
        private void UpdateOutcomeAfterDamage(CombatUnit targetUnit)
        {
            if (targetUnit.IsAlive())
            {
                return;
            }

            var eliminatedFaction = _unitRegistry.GetFaction(targetUnit);
            if (eliminatedFaction == CombatFaction.Neutral ||
                !_unitRegistry.AreFactionUnitsDead(eliminatedFaction))
            {
                return;
            }

            var winningFaction = eliminatedFaction == CombatFaction.TeamA
                ? CombatFaction.TeamB
                : CombatFaction.TeamA;
            _outcome = BattleOutcome.FactionEliminated(winningFaction, eliminatedFaction);
        }

        /// <summary>
        /// 统一创建并记录“对局已结束”失败，确保失败操作仍占用连续日志序号。
        /// </summary>
        private BattleActionResult RecordBattleEndedFailure(
            int sequence, BattleActionType actionType, CombatUnit actorUnit)
        {
            var result = BattleActionResult.Failed(sequence, actionType, actorUnit,
                BattleActionFailureReason.BattleAlreadyEnded, _outcome);
            return RecordAction(result);
        }

        private Dictionary<int, int> CaptureActionPoints()
        {
            var actionPoints = new Dictionary<int, int>(CombatUnits.Count);
            foreach (var combatUnit in CombatUnits)
            {
                actionPoints.Add(
                    combatUnit.UnitId,
                    combatUnit.PilotRuntime.CurrentActionPoint);
            }

            return actionPoints;
        }

        private BattleActionResult RecordAction(BattleActionResult result)
        {
            _actionLogs.Add(new BattleActionLog(result));
            return result;
        }

        private BattleActionFailureReason GetBasicAttackFailureReason(CombatUnit attackerUnit, CombatUnit targetUnit)
        {
            if (_unitRegistry.GetFaction(attackerUnit) == _unitRegistry.GetFaction(targetUnit))
            {
                return BattleActionFailureReason.SameFactionTarget;
            }

            if (targetUnit.IsDead())
            {
                return BattleActionFailureReason.TargetAlreadyDead;
            }

            if (!_attackResolver.IsPositionInRange(attackerUnit, targetUnit.Position))
            {
                return BattleActionFailureReason.TargetOutOfRange;
            }

            return attackerUnit.PilotRuntime.HasEnoughActionPoint(
                attackerUnit.Mech.BasicAttack.ActionPointCost)
                ? BattleActionFailureReason.None
                : BattleActionFailureReason.InsufficientActionPoint;
        }

        private BattleActionFailureReason GetAbilityFailureReason(AbilityDefinition ability,
            CombatUnit sourceUnit, CombatUnit targetUnit,
            AbilityTargetRangeSource rangeSource)
        {
            if (targetUnit.IsDead())
            {
                return BattleActionFailureReason.TargetAlreadyDead;
            }

            if (_unitRegistry.GetFaction(sourceUnit) == _unitRegistry.GetFaction(targetUnit))
            {
                return BattleActionFailureReason.SameFactionTarget;
            }

            if (!_abilityResolver.IsPositionInRange(
                    ability,
                    sourceUnit,
                    targetUnit.Position,
                    rangeSource))
            {
                return BattleActionFailureReason.TargetOutOfRange;
            }

            return sourceUnit.PilotRuntime.HasEnoughActionPoint(ability.ActionPointCost)
                ? BattleActionFailureReason.None
                : BattleActionFailureReason.InsufficientActionPoint;
        }

        private void ValidateUnitPositions(IReadOnlyList<CombatUnit> units, string parameterName)
        {
            foreach (var unit in units)
            {
                ValidateGridPosition(unit.Position, parameterName);
            }
        }

        private void ValidateAliveUnitPositionsDoNotOverlap()
        {
            var occupiedPositions = new HashSet<Vector2Int>();
            foreach (var unit in CombatUnits)
            {
                if (unit.IsAlive() && !occupiedPositions.Add(unit.Position))
                {
                    throw new ArgumentException("Alive combat units cannot share the same grid position.");
                }
            }
        }

        private void ValidateGridPosition(Vector2Int position, string parameterName)
        {
            if (!_squareGrid.IsInside(position))
            {
                throw new ArgumentOutOfRangeException(parameterName, position,
                    "Grid position must be inside the battle grid.");
            }
        }
    }
}
