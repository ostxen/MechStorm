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
        CurrentUnitMoved,
        AttackTargetSelected,
        AbilityTargetSelected,
        TargetAbilityUsed,
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

        private CombatUnit _selectedCombatUnit;
        private CombatUnit _selectedAttackTarget;
        private AbilityDefinition _targetingAbility;
        private CombatUnit _selectedAbilityTarget;

        public string LastRejectionReason { get; private set; }

        public BattleActionResult LastActionResult { get; private set; }

        public bool IsAbilityTargeting => _targetingAbility != null;

        public string TargetingAbilityName => _targetingAbility?.Name;

        public CombatUnit SelectedAbilityTarget => _selectedAbilityTarget;

        public CombatUnit SelectedAttackTarget => _selectedAttackTarget;

        public bool IsCurrentUnitSelected =>
            ReferenceEquals(_selectedCombatUnit, _battleSession.CurrentCombatUnit);

        public bool CanConfirmBasicAttack => _selectedAttackTarget != null;

        public bool CanConfirmAbilityTargeting =>
            _targetingAbility != null && _selectedAbilityTarget != null;

        public BattlePresentationController(BattleSession battleSession, IReadOnlyDictionary<CombatUnit, CombatUnitVisual> combatUnitVisuals,
            BattleBoardInputter battleBoardInputter, BattleRangeHighlighter rangeHighlighter)
        {
            _battleSession = battleSession ?? throw new ArgumentNullException(nameof(battleSession));
            _combatUnitVisuals = combatUnitVisuals ?? throw new ArgumentNullException(nameof(combatUnitVisuals));
            _battleBoardInputter = battleBoardInputter ?? throw new ArgumentNullException(nameof(battleBoardInputter));
            _rangeHighlighter = rangeHighlighter ?? throw new ArgumentNullException(nameof(rangeHighlighter));
            _highlightMapper = new BattleRangeHighlightMapper();
        }

        public BattleInputAction Tick(out CombatUnit actionUnit)
        {
            actionUnit = null;
            LastRejectionReason = null;
            LastActionResult = null;

            if (!_battleBoardInputter.Tick(out var clickedCombatUnit, out var targetGridPosition))
            {
                return BattleInputAction.None;
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
        /// 保持当前单位选中，并按最新战斗状态刷新范围。
        /// </summary>
        public void RefreshSelection()
        {
            if (_selectedCombatUnit == null)
            {
                return;
            }

            if (!ReferenceEquals(_selectedCombatUnit, _battleSession.CurrentCombatUnit))
            {
                ClearSelection();
                return;
            }

            if (_selectedAttackTarget != null &&
                !ContainsTarget(
                    _battleSession.GetCurrentCombatUnitBasicAttackTargets(),
                    _selectedAttackTarget))
            {
                _selectedAttackTarget = null;
            }

            RefreshRangeHighlights();
        }

        /// <summary>
        /// 清除当前选择并恢复棋盘格默认颜色。
        /// </summary>
        public void ClearSelection()
        {
            _selectedCombatUnit = null;
            _selectedAttackTarget = null;
            _targetingAbility = null;
            _selectedAbilityTarget = null;
            _rangeHighlighter.Clear();
        }

        /// <summary>
        /// 选中当前单位并进入指定技能的目标选择模式。
        /// </summary>
        public void BeginAbilityTargeting(AbilityDefinition ability)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            LastRejectionReason = null;
            LastActionResult = null;
            _selectedCombatUnit = _battleSession.CurrentCombatUnit;
            _selectedAttackTarget = null;
            _targetingAbility = ability;
            _selectedAbilityTarget = null;
            RefreshRangeHighlights();
        }

        /// <summary>
        /// 退出技能目标选择，并恢复普通移动与攻击高亮。
        /// </summary>
        public void CancelAbilityTargeting()
        {
            _targetingAbility = null;
            _selectedAbilityTarget = null;
            RefreshSelection();
        }

        public BattleActionResult ConfirmAbilityTargeting()
        {
            if (_targetingAbility == null || _selectedAbilityTarget == null)
            {
                throw new InvalidOperationException(
                    "Ability targeting requires a selected target before confirmation.");
            }

            LastRejectionReason = null;
            LastActionResult = _battleSession.TryUseAbility(
                _targetingAbility,
                _selectedAbilityTarget);
            if (!LastActionResult.IsSuccess)
            {
                LastRejectionReason = GetFailureReason(
                    LastActionResult.FailureReason,
                    LastActionResult.ActionType);
                RefreshRangeHighlights();
                return LastActionResult;
            }

            _targetingAbility = null;
            _selectedAbilityTarget = null;
            RefreshSelection();
            return LastActionResult;
        }

        public BattleActionResult ConfirmBasicAttack()
        {
            if (_selectedAttackTarget == null)
            {
                throw new InvalidOperationException(
                    "Basic attack requires a selected target before confirmation.");
            }

            LastRejectionReason = null;
            LastActionResult = _battleSession.AttackTargetCombatUnit(_selectedAttackTarget);
            if (!LastActionResult.IsSuccess)
            {
                LastRejectionReason = GetFailureReason(
                    LastActionResult.FailureReason,
                    LastActionResult.ActionType);
            }

            RefreshSelection();
            return LastActionResult;
        }

        /// <summary>
        /// 结束当前单位行动并清除选择。
        /// </summary>
        public BattleActionResult EndCurrentUnitAction()
        {
            LastRejectionReason = null;
            LastActionResult = _battleSession.EndCurrentUnitAction();
            ClearSelection();
            return LastActionResult;
        }

        public BattleActionResult EndCurrentFactionTurn()
        {
            LastRejectionReason = null;
            LastActionResult = _battleSession.EndCurrentFactionTurn();
            ClearSelection();
            return LastActionResult;
        }

        private BattleInputAction HandleCombatUnitClick(CombatUnit currentCombatUnit, CombatUnit clickedCombatUnit)
        {
            if (_targetingAbility != null)
            {
                var validTargets = _battleSession.GetCurrentCombatUnitAbilityTargets(
                    _targetingAbility);
                var isValidTarget = false;
                foreach (var validTarget in validTargets)
                {
                    if (ReferenceEquals(validTarget, clickedCombatUnit))
                    {
                        isValidTarget = true;
                        break;
                    }
                }

                if (!isValidTarget)
                {
                    LastRejectionReason = "Select a highlighted valid ability target.";
                    return BattleInputAction.ActionRejected;
                }

                _selectedAbilityTarget = clickedCombatUnit;
                RefreshRangeHighlights();
                return BattleInputAction.AbilityTargetSelected;
            }

            if (ReferenceEquals(currentCombatUnit, clickedCombatUnit))
            {
                _selectedCombatUnit = currentCombatUnit;
                RefreshSelection();
                return BattleInputAction.CurrentUnitSelected;
            }

            if (!ContainsTarget(
                    _battleSession.GetCurrentCombatUnitBasicAttackTargets(),
                    clickedCombatUnit))
            {
                LastRejectionReason = "Select a highlighted valid attack target.";
                return BattleInputAction.ActionRejected;
            }

            _selectedCombatUnit = currentCombatUnit;
            _selectedAttackTarget = clickedCombatUnit;
            RefreshRangeHighlights();
            return BattleInputAction.AttackTargetSelected;
        }

        private BattleInputAction HandleGridClick(CombatUnit currentCombatUnit, Vector2Int targetGridPosition)
        {
            if (_selectedCombatUnit == null)
            {
                LastRejectionReason = "Select the current combat unit before moving.";
                return BattleInputAction.ActionRejected;
            }

            if (_targetingAbility != null)
            {
                LastRejectionReason = "Select a combat unit as the ability target.";
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

        /// <summary>
        /// 查询 Battle 权威范围并刷新棋盘高亮。
        /// </summary>
        private void RefreshRangeHighlights()
        {
            if (_targetingAbility != null)
            {
                var abilityHighlights = _highlightMapper.BuildAbility(
                    _battleSession.GetCurrentCombatUnitAbilityPositions(_targetingAbility),
                    _battleSession.GetCurrentCombatUnitAbilityTargets(_targetingAbility),
                    _selectedAbilityTarget);
                _rangeHighlighter.Show(abilityHighlights);
                return;
            }

            var highlights = _highlightMapper.Build(
                _battleSession.GetCurrentCombatUnitReachablePositions(),
                _battleSession.GetCurrentCombatUnitBasicAttackPositions(),
                _battleSession.GetCurrentCombatUnitBasicAttackTargets(),
                _selectedAttackTarget);
            _rangeHighlighter.Show(highlights);
        }

        private static bool ContainsTarget(IReadOnlyList<CombatUnit> targets, CombatUnit target)
        {
            foreach (var candidate in targets)
            {
                if (ReferenceEquals(candidate, target))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetFailureReason(
            BattleActionFailureReason failureReason,
            BattleActionType actionType)
        {
            return failureReason switch
            {
                BattleActionFailureReason.InvalidMoveTarget => "Current combat unit cannot move to the selected grid position.",
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
                _ => "Battle action was rejected.",
            };
        }
    }
}
