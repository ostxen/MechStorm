using System;
using System.Collections.Generic;
using MechStorm.Battle;
using MechStorm.Battle.Units;
using MechStorm.Presentation.Controllers;
using MechStorm.Presentation.Units;
using TEngine;

namespace GameLogic
{
    /// <summary>
    /// 映射本场行动选择与 HUD 意图；规则和执行继续由现有 Controller / Battle 校验。
    /// </summary>
    public sealed class BattleHUDPresenter : IDisposable
    {
        private BattleSession _session;
        private BattleHUDUI _view;
        private BattleInteractionController _controller;
        private BattleUnitStatusPresenter _unitStatusPresenter;
        private Func<bool> _canAcceptInput;
        private CombatUnit _lastUnit;
        private readonly List<BattleActionSelection> _displayedActions = new();
        private BattleActionSelection _lastAction;
        private BattleActionTargetSelection _lastTarget;
        private int _lastActionPoint;
        private int _lastMaxActionPoint;
        private int _lastTargetActionPoint;
        private bool _lastEnded;
        private bool _lastSelected;
        private bool _lastCanConfirm;

        public BattleHUDPresenter(BattleSession session, BattleInteractionController controller,
            BattleUnitStatusPresenter unitStatusPresenter, BattleHUDUI view, Func<bool> canAcceptInput)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _unitStatusPresenter = unitStatusPresenter ?? throw new ArgumentNullException(nameof(unitStatusPresenter));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _canAcceptInput = canAcceptInput ?? throw new ArgumentNullException(nameof(canAcceptInput));
            view.ActionRequested += OnActionRequested;
            view.ConfirmRequested += OnConfirmRequested;
            view.CancelRequested += OnCancelRequested;
            view.StandbyRequested += OnStandbyRequested;
        }

        public void RefreshIfChanged(bool force = false)
        {
            var unit = _session.CurrentCombatUnit;
            bool unitChanged = !ReferenceEquals(unit, _lastUnit);
            if (unitChanged && !_session.IsBattleEnded && !_controller.IsCurrentUnitSelected)
            {
                _controller.SelectCurrentCombatUnit();
            }
            if (unitChanged || unit.PilotRuntime.CurrentActionPoint != _lastActionPoint)
            {
                _controller.RefreshSelection();
            }
            var target = _controller.SelectedTarget?.Unit;
            if (!force && !HasChanged(unit, target))
            {
                return;
            }
            RefreshStatus(unit, target);
            RefreshActions(unit);
            RememberState(unit, target);
        }

        private bool HasChanged(CombatUnit unit, CombatUnit target)
        {
            return !ReferenceEquals(unit, _lastUnit) ||
                unit.PilotRuntime.CurrentActionPoint != _lastActionPoint ||
                unit.Pilot.MaxActionPoint != _lastMaxActionPoint ||
                !ReferenceEquals(_controller.SelectedAction, _lastAction) ||
                !ReferenceEquals(_controller.SelectedTarget, _lastTarget) ||
                (target?.PilotRuntime.CurrentActionPoint ?? 0) != _lastTargetActionPoint ||
                _session.IsBattleEnded != _lastEnded ||
                _controller.IsCurrentUnitSelected != _lastSelected ||
                _controller.CanConfirmSelectedAction != _lastCanConfirm;
        }

        private void RefreshStatus(CombatUnit unit, CombatUnit target)
        {
            int cost = GetActionCost(_controller.SelectedAction, unit);
            _view.SetPilotName(unit.Pilot.Name);
            _view.SetActionPoint(unit.PilotRuntime.CurrentActionPoint, unit.Pilot.MaxActionPoint);
            _view.SetActionPointPreview(cost);
            _view.SetCombatInfo(!_session.IsBattleEnded && target != null,
                unit.Pilot.Name, unit.PilotRuntime.CurrentActionPoint, unit.Pilot.MaxActionPoint,
                target?.Pilot.Name, target?.PilotRuntime.CurrentActionPoint ?? 0, target?.Pilot.MaxActionPoint ?? 0, cost);
        }

        private void RefreshActions(CombatUnit unit)
        {
            bool enabled = !_session.IsBattleEnded && _controller.IsCurrentUnitSelected;
            bool selecting = _controller.ActionSelectionState != BattleActionSelectionState.ActionList;
            bool hasTarget = _controller.SelectedTarget?.Unit != null;
            bool canSelect = enabled && hasTarget &&
                _controller.ActionSelectionPath == BattleActionSelectionPath.TargetFirst;
            _displayedActions.Clear();
            if (enabled && hasTarget)
            {
                _displayedActions.AddRange(_controller.GetAvailableActionsForSelectedTarget());
            }
            var attack = unit.Mech.BasicAttack;
            // 持有武器不等于选择攻击方式，不能因此锁定移动或进入 ActionFirst。
            _view.SetWeapon(attack.Damage, attack.MinRange, attack.MaxRange, enabled && !selecting, true);
            _view.SetAbilityCount(_displayedActions.Count);
            for (int i = 0; i < _displayedActions.Count; i++)
            {
                var action = _displayedActions[i];
                string title = action.Kind == BattleActionSelectionKind.BasicAttack ? "普通攻击" : action.Ability.Name;
                _view.SetAbility(i, title, GetActionCost(action, unit), canSelect,
                    action.Matches(_controller.SelectedAction));
            }
            bool awaiting = _controller.ActionSelectionState == BattleActionSelectionState.AwaitingConfirmation;
            _view.SetActionLayout(enabled, selecting, hasTarget, awaiting, _controller.CanConfirmSelectedAction);
        }

        private static int GetActionCost(BattleActionSelection action, CombatUnit unit)
        {
            if (action == null)
            {
                return 0;
            }
            return action.Kind == BattleActionSelectionKind.BasicAttack
                ? unit.Mech.BasicAttack.ActionPointCost : action.Ability.ActionPointCost;
        }

        private void RememberState(CombatUnit unit, CombatUnit target)
        {
            _lastUnit = unit;
            _lastActionPoint = unit.PilotRuntime.CurrentActionPoint;
            _lastMaxActionPoint = unit.Pilot.MaxActionPoint;
            _lastTargetActionPoint = target?.PilotRuntime.CurrentActionPoint ?? 0;
            _lastAction = _controller.SelectedAction;
            _lastTarget = _controller.SelectedTarget;
            _lastEnded = _session.IsBattleEnded;
            _lastSelected = _controller.IsCurrentUnitSelected;
            _lastCanConfirm = _controller.CanConfirmSelectedAction;
        }

        private bool CanAcceptInput()
        {
            if (!_view.Visible || _session.IsBattleEnded || !_canAcceptInput())
            {
                return false;
            }
            if (!ReferenceEquals(_lastUnit, _session.CurrentCombatUnit))
            {
                RefreshIfChanged();
                return false;
            }
            return _controller.IsCurrentUnitSelected;
        }

        private void OnActionRequested(int index)
        {
            if (!CanAcceptInput() || _controller.SelectedTarget?.Unit == null)
            {
                return;
            }
            if (!ReferenceEquals(_controller.SelectedTarget, _lastTarget))
            {
                RefreshIfChanged(true);
                return;
            }
            if (index < 0 || index >= _displayedActions.Count)
            {
                return;
            }
            var action = _displayedActions[index];
            bool accepted = action.Kind == BattleActionSelectionKind.BasicAttack
                ? _controller.SelectBasicAttack() : _controller.SelectAbility(action.Ability, action.RangeSource);
            if (!accepted)
            {
                Log.Warning($"行动选择被拒绝：{_controller.LastRejectionReason}");
            }
            RefreshIfChanged(true);
        }

        private void OnConfirmRequested()
        {
            if (!CanAcceptInput() || !_controller.CanConfirmSelectedAction)
            {
                return;
            }
            var result = _controller.ConfirmSelectedAction();
            RefreshAfterAction(result.IsSuccess);
        }

        private void OnCancelRequested()
        {
            if (!CanAcceptInput())
            {
                return;
            }
            _controller.CancelActionSelection();
            RefreshIfChanged(true);
        }

        private void OnStandbyRequested()
        {
            if (!CanAcceptInput() || _controller.IsActionInputLocked)
            {
                return;
            }
            var result = _controller.EndCurrentUnitAction();
            RefreshAfterAction(result.IsSuccess);
        }

        private void RefreshAfterAction(bool success)
        {
            _unitStatusPresenter.RefreshAll();
            // 阵营已推进但提示尚未播放时，不提前选中新阵营单位或刷新成可操作 HUD。
            if (success && !_session.IsBattleEnded && !_canAcceptInput())
            {
                return;
            }
            if (success && !_session.IsBattleEnded)
            {
                _controller.SelectCurrentCombatUnit();
            }
            if (!success)
            {
                Log.Warning($"战斗行动被拒绝：{_controller.LastRejectionReason}");
            }
            RefreshIfChanged(true);
        }

        public void Dispose()
        {
            if (_view != null)
            {
                _view.ActionRequested -= OnActionRequested;
                _view.ConfirmRequested -= OnConfirmRequested;
                _view.CancelRequested -= OnCancelRequested;
                _view.StandbyRequested -= OnStandbyRequested;
            }
            _view = null;
            _session = null;
            _lastUnit = null;
            _controller = null;
            _unitStatusPresenter = null;
            _canAcceptInput = null;
            _displayedActions.Clear();
            _lastAction = null;
            _lastTarget = null;
        }
    }
}
