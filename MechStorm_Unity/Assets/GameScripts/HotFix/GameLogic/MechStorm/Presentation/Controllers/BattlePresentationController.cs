using System;
using System.Collections.Generic;
using MechStorm.Battle;
using MechStorm.Battle.Actions;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using MechStorm.Presentation.Board;
using MechStorm.Presentation.Input;
using MechStorm.Presentation.Units;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Controllers
{
    public enum BattleInputAction
    {
        None,
        CurrentUnitSelected,
        CurrentUnitChanged,
        CurrentUnitMoved,
        TargetSelected,
        ActionRejected,
    }

    public sealed class BattlePresentationController
    {
        private readonly BattleSession _battleSession;
        // 战斗单位到对应表现对象的映射。
        private readonly IReadOnlyDictionary<CombatUnit, CombatUnitVisual> _combatUnitVisuals;
        private readonly BattleBoardInputter _battleBoardInputter;
        private readonly BattleRangeHighlightMapper _highlightMapper;
        private readonly BattleRangeHighlighter _rangeHighlighter;
        private readonly BattleActionSelectionCoordinator _actionSelection;

        private CombatUnit _selectedCombatUnit;

        public string LastRejectionReason { get; private set; }

        public BattleActionResult LastActionResult { get; private set; }

        public BattleActionSelectionState ActionSelectionState => _actionSelection.State;

        public BattleActionSelection SelectedAction => _actionSelection.SelectedAction;

        public BattleActionTargetSelection SelectedTarget => _actionSelection.SelectedTarget;

        public BattleActionSelectionPath ActionSelectionPath => _actionSelection.Path;

        public bool IsActionInputLocked => _actionSelection.IsInputLocked;

        public bool CanConfirmSelectedAction => _actionSelection.CanConfirm;

        public bool IsCurrentUnitSelected =>
            ReferenceEquals(_selectedCombatUnit, _battleSession.CurrentCombatUnit);

        public IReadOnlyList<BattleActionSelection> GetAvailableActions()
        {
            return _actionSelection.GetAvailableActions();
        }

        public IReadOnlyList<BattleActionSelection> GetAvailableActionsForSelectedTarget()
        {
            return _actionSelection.GetAvailableActionsForSelectedTarget();
        }

        public BattlePresentationController(BattleSession battleSession, IReadOnlyDictionary<CombatUnit, CombatUnitVisual> combatUnitVisuals,
            BattleBoardInputter battleBoardInputter, BattleRangeHighlighter rangeHighlighter,
            IReadOnlyDictionary<int, IReadOnlyList<AbilityDefinition>> abilitiesByUnitId = null)
        {
            _battleSession = battleSession ?? throw new ArgumentNullException(nameof(battleSession));
            _combatUnitVisuals = combatUnitVisuals ?? throw new ArgumentNullException(nameof(combatUnitVisuals));
            _battleBoardInputter = battleBoardInputter ?? throw new ArgumentNullException(nameof(battleBoardInputter));
            _rangeHighlighter = rangeHighlighter ?? throw new ArgumentNullException(nameof(rangeHighlighter));
            _highlightMapper = new BattleRangeHighlightMapper();
            _actionSelection = new BattleActionSelectionCoordinator(
                battleSession,
                abilitiesByUnitId);
        }

        /// <summary>
        /// 读取一次棋盘点击并转换成表现层动作；对局结束后统一拒绝输入并清除旧高亮。
        /// </summary>
        public BattleInputAction Tick(out CombatUnit actionUnit)
        {
            actionUnit = null;
            LastRejectionReason = null;
            LastActionResult = null;

            if (!_battleBoardInputter.Tick(out var clickedCombatUnit, out var targetGridPosition))
            {
                return BattleInputAction.None;
            }

            if (_battleSession.IsBattleEnded)
            {
                // 仍返回被点击单位，方便调试日志说明这次被拒绝的输入来自哪里。
                actionUnit = clickedCombatUnit ?? _battleSession.CurrentCombatUnit;
                LastRejectionReason = GetFailureReason(
                    BattleActionFailureReason.BattleAlreadyEnded,
                    BattleActionType.Move);
                ClearSelection();
                return BattleInputAction.ActionRejected;
            }

            var currentCombatUnit = _battleSession.CurrentCombatUnit;
            if (!ReferenceEquals(_selectedCombatUnit, currentCombatUnit))
            {
                ClearSelection();
            }

            if (clickedCombatUnit != null)
            {
                actionUnit = clickedCombatUnit;
                return HandleCombatUnitClick(currentCombatUnit, clickedCombatUnit);
            }

            actionUnit = currentCombatUnit;
            return HandleGridClick(currentCombatUnit, targetGridPosition);
        }
        /// <summary>
        /// 保持当前单位选中，并按最新战斗数据刷新范围；对局结束时直接清空选择。
        /// </summary>
        public void RefreshSelection()
        {
            if (_battleSession.IsBattleEnded)
            {
                ClearSelection();
                return;
            }

            if (_selectedCombatUnit == null)
            {
                return;
            }

            if (!ReferenceEquals(_selectedCombatUnit, _battleSession.CurrentCombatUnit))
            {
                ClearSelection();
                return;
            }

            _actionSelection.Refresh();
            RefreshRangeHighlights();
        }

        /// <summary>
        /// 清除当前选择并恢复棋盘格默认颜色。
        /// </summary>
        public void ClearSelection()
        {
            _selectedCombatUnit = null;
            _actionSelection.Reset();
            _rangeHighlighter.Clear();
        }

        /// <summary>
        /// 选择基础攻击；目标优先时保留合法目标并直接进入待确认。
        /// </summary>
        public bool SelectBasicAttack()
        {
            return TrySelectAction(BattleActionSelection.ForBasicAttack());
        }

        /// <summary>
        /// 选择主动技能；目标优先时保留合法目标并直接进入待确认。
        /// </summary>
        public bool SelectAbility(AbilityDefinition ability,
            AbilityTargetRangeSource rangeSource = AbilityTargetRangeSource.Ability)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            return TrySelectAction(BattleActionSelection.ForAbility(ability, rangeSource));
        }

        /// <summary>
        /// 取消当前行动选择，保留行动者站位并恢复移动与普通攻击范围。
        /// </summary>
        public void CancelActionSelection()
        {
            _actionSelection.Cancel();
            RefreshSelection();
        }

        /// <summary>
        /// 提交已选择的通用行动与目标。
        /// </summary>
        public BattleActionResult ConfirmSelectedAction()
        {
            LastRejectionReason = null;
            LastActionResult = _actionSelection.Confirm();

            if (!LastActionResult.IsSuccess)
            {
                LastRejectionReason = GetFailureReason(
                    LastActionResult.FailureReason,
                    LastActionResult.ActionType);
                if (_battleSession.IsBattleEnded)
                {
                    ClearSelection();
                }
                else
                {
                    RefreshSelection();
                }

                return LastActionResult;
            }

            ClearSelection();
            return LastActionResult;
        }

        /// <summary>
        /// 结束当前单位行动并清除选择；对局已结束时保留明确的拒绝原因。
        /// </summary>
        public BattleActionResult EndCurrentUnitAction()
        {
            ThrowIfActionInputLocked();
            LastRejectionReason = null;
            LastActionResult = _battleSession.EndCurrentUnitAction();
            if (!LastActionResult.IsSuccess)
            {
                LastRejectionReason = GetFailureReason(
                    LastActionResult.FailureReason,
                    LastActionResult.ActionType);
            }

            ClearSelection();
            return LastActionResult;
        }

        /// <summary>
        /// 结束当前阵营回合并清除选择；对局已结束时不会推进阵营。
        /// </summary>
        public BattleActionResult EndCurrentFactionTurn()
        {
            ThrowIfActionInputLocked();
            LastRejectionReason = null;
            LastActionResult = _battleSession.EndCurrentFactionTurn();
            if (!LastActionResult.IsSuccess)
            {
                LastRejectionReason = GetFailureReason(
                    LastActionResult.FailureReason,
                    LastActionResult.ActionType);
            }

            RefreshActorPosition(LastActionResult);
            ClearSelection();
            return LastActionResult;
        }

        private BattleInputAction HandleCombatUnitClick(CombatUnit currentCombatUnit, CombatUnit clickedCombatUnit)
        {
            switch (_actionSelection.State)
            {
                case BattleActionSelectionState.ActionSelected:
                case BattleActionSelectionState.AwaitingConfirmation:
                    return SelectActionTarget(clickedCombatUnit);
                case BattleActionSelectionState.TargetSelected:
                    LastRejectionReason =
                        "Select an available action or cancel the selected target.";
                    return BattleInputAction.ActionRejected;
            }

            if (ReferenceEquals(currentCombatUnit, clickedCombatUnit))
            {
                _selectedCombatUnit = currentCombatUnit;
                RefreshSelection();
                return BattleInputAction.CurrentUnitSelected;
            }

            if (_battleSession.GetCombatUnitFaction(clickedCombatUnit) ==
                _battleSession.CurrentFaction)
            {
                LastActionResult =
                    _battleSession.TrySelectCurrentFactionCombatUnit(clickedCombatUnit);
                if (!LastActionResult.IsSuccess)
                {
                    LastRejectionReason = GetFailureReason(
                        LastActionResult.FailureReason,
                        LastActionResult.ActionType);
                    return BattleInputAction.ActionRejected;
                }

                RefreshActorPosition(LastActionResult);
                _selectedCombatUnit = clickedCombatUnit;
                _actionSelection.Reset();
                RefreshSelection();
                return BattleInputAction.CurrentUnitChanged;
            }

            return SelectActionTarget(clickedCombatUnit);
        }

        private BattleInputAction HandleGridClick(CombatUnit currentCombatUnit, Vector2Int targetGridPosition)
        {
            if (_selectedCombatUnit == null)
            {
                LastRejectionReason = "Select the current combat unit before moving.";
                return BattleInputAction.ActionRejected;
            }

            if (_actionSelection.IsInputLocked)
            {
                LastRejectionReason =
                    "Confirm or cancel the selected action before moving.";
                return BattleInputAction.ActionRejected;
            }

            if (!_combatUnitVisuals.TryGetValue(currentCombatUnit, out var currentUnitVisual))
            {
                throw new InvalidOperationException("Current combat unit does not have a presentation visual.");
            }

            var moveResult = _battleSession.TryMoveCurrentCombatUnit(targetGridPosition);
            LastActionResult = moveResult;
            if (!moveResult.IsSuccess)
            {
                LastRejectionReason = GetFailureReason(moveResult.FailureReason, moveResult.ActionType);
                return BattleInputAction.ActionRejected;
            }

            currentUnitVisual.RefreshPosition(moveResult.PositionAfter.Value);
            RefreshSelection();
            return BattleInputAction.CurrentUnitMoved;
        }

        private BattleInputAction SelectActionTarget(CombatUnit clickedCombatUnit)
        {
            if (!_actionSelection.TrySelectUnitTarget(
                    clickedCombatUnit,
                    out var rejectionReason))
            {
                LastRejectionReason = rejectionReason;
                return BattleInputAction.ActionRejected;
            }

            _selectedCombatUnit = _battleSession.CurrentCombatUnit;
            RefreshRangeHighlights();
            return BattleInputAction.TargetSelected;
        }

        private bool TrySelectAction(BattleActionSelection action)
        {
            LastRejectionReason = null;
            LastActionResult = null;
            if (_battleSession.IsBattleEnded)
            {
                LastRejectionReason = GetFailureReason(
                    BattleActionFailureReason.BattleAlreadyEnded,
                    GetActionType(action));
                ClearSelection();
                return false;
            }

            if (!ReferenceEquals(_selectedCombatUnit, _battleSession.CurrentCombatUnit))
            {
                LastRejectionReason =
                    "Select the current combat unit before selecting an action.";
                return false;
            }

            if (!_actionSelection.TrySelectAction(action, out var rejectionReason))
            {
                LastRejectionReason = rejectionReason;
                return false;
            }

            RefreshRangeHighlights();
            return true;
        }

        private void RefreshActorPosition(BattleActionResult result)
        {
            if (result?.PositionAfter == null)
            {
                return;
            }

            if (!_combatUnitVisuals.TryGetValue(result.ActorUnit, out var actorVisual))
            {
                throw new InvalidOperationException(
                    "Battle action actor does not have a presentation visual.");
            }

            actorVisual.RefreshPosition(result.PositionAfter.Value);
        }

        /// <summary>
        /// 查询 Battle 权威范围并刷新棋盘高亮。
        /// </summary>
        private void RefreshRangeHighlights()
        {
            var selectedAction = _actionSelection.SelectedAction;
            if (selectedAction?.Kind == BattleActionSelectionKind.ActiveAbility)
            {
                var abilityHighlights = _highlightMapper.BuildAbility(
                    _battleSession.GetCurrentCombatUnitAbilityPositions(
                        selectedAction.Ability,
                        selectedAction.RangeSource),
                    _battleSession.GetCurrentCombatUnitAbilityTargets(
                        selectedAction.Ability,
                        selectedAction.RangeSource),
                    _actionSelection.SelectedTarget?.Unit);
                _rangeHighlighter.Show(abilityHighlights);
                return;
            }

            if (_actionSelection.State == BattleActionSelectionState.TargetSelected)
            {
                var targetHighlights = _highlightMapper.BuildSelectedActionTarget(
                    _actionSelection.SelectedTarget.Unit);
                _rangeHighlighter.Show(targetHighlights);
                return;
            }

            var highlights = _highlightMapper.Build(
                _actionSelection.IsInputLocked
                    ? Array.Empty<Vector2Int>()
                    : _battleSession.GetCurrentCombatUnitReachablePositions(),
                _battleSession.GetCurrentCombatUnitBasicAttackPositions(),
                _battleSession.GetCurrentCombatUnitBasicAttackTargets(),
                _actionSelection.SelectedTarget?.Unit);
            _rangeHighlighter.Show(highlights);
        }

        private static BattleActionType GetActionType(BattleActionSelection action)
        {
            return action.Kind == BattleActionSelectionKind.ActiveAbility
                ? BattleActionType.UseAbility
                : BattleActionType.Attack;
        }

        private void ThrowIfActionInputLocked()
        {
            if (!_actionSelection.IsInputLocked)
            {
                return;
            }

            LastRejectionReason =
                "Confirm or cancel the selected action before ending the action or faction turn.";
            throw new InvalidOperationException(LastRejectionReason);
        }

        private static string GetFailureReason(
            BattleActionFailureReason failureReason,
            BattleActionType actionType)
        {
            return failureReason switch
            {
                BattleActionFailureReason.InvalidMoveTarget => "Current combat unit cannot move to the selected grid position.",
                BattleActionFailureReason.InvalidUnitSelection => "Select an alive, unacted combat unit from the current faction.",
                BattleActionFailureReason.TargetOutOfRange => actionType == BattleActionType.UseAbility
                    ? "Target combat unit is outside ability range."
                    : "Target combat unit is outside attack range.",
                BattleActionFailureReason.SameFactionTarget => actionType == BattleActionType.UseAbility
                    ? "Cannot use this ability on a combat unit from the same faction."
                    : "Cannot attack a combat unit from the same faction.",
                BattleActionFailureReason.TargetAlreadyDead => actionType == BattleActionType.UseAbility
                    ? "Cannot use this ability on a dead combat unit."
                    : "Cannot attack a dead combat unit.",
                BattleActionFailureReason.InsufficientActionPoint => "Current combat unit does not have enough AP.",
                BattleActionFailureReason.ActorCannotAct => "Current combat unit cannot act.",
                BattleActionFailureReason.BattleAlreadyEnded => "Battle has already ended.",
                _ => "Battle action was rejected.",
            };
        }
    }
}
