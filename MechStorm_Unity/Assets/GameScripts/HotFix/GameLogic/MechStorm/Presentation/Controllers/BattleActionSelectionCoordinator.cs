using System;
using System.Collections.Generic;
using MechStorm.Battle;
using MechStorm.Battle.Actions;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;

namespace MechStorm.Presentation.Controllers
{
    /// <summary>
    /// 使用 Battle 权威查询校验行动与目标组合，不复制攻击或技能规则。
    /// </summary>
    public sealed class BattleActionSelectionCoordinator
    {
        private readonly BattleSession _battleSession;
        private readonly IReadOnlyDictionary<int, IReadOnlyList<AbilityDefinition>> _abilitiesByUnitId;
        private readonly BattleActionSelectionStateMachine _stateMachine;

        public BattleActionSelectionCoordinator(BattleSession battleSession,
            IReadOnlyDictionary<int, IReadOnlyList<AbilityDefinition>> abilitiesByUnitId = null)
        {
            _battleSession = battleSession ?? throw new ArgumentNullException(nameof(battleSession));
            _abilitiesByUnitId = abilitiesByUnitId ??
                new Dictionary<int, IReadOnlyList<AbilityDefinition>>();
            _stateMachine = new BattleActionSelectionStateMachine();
        }

        public BattleActionSelectionState State => _stateMachine.State;

        public BattleActionSelection SelectedAction => _stateMachine.SelectedAction;

        public BattleActionTargetSelection SelectedTarget => _stateMachine.SelectedTarget;

        public bool IsInputLocked => _stateMachine.IsInputLocked;

        public bool CanConfirm =>
            !_battleSession.IsBattleEnded &&
            _stateMachine.CanConfirm &&
            IsValidTarget(SelectedAction, SelectedTarget);

        /// <summary>
        /// 返回行动优先入口可选择的行动；行动本身可用不要求范围内已经存在目标。
        /// </summary>
        public IReadOnlyList<BattleActionSelection> GetAvailableActions()
        {
            var actions = new List<BattleActionSelection>();
            var basicAttack = BattleActionSelection.ForBasicAttack();
            if (IsActionAvailable(basicAttack))
            {
                actions.Add(basicAttack);
            }

            foreach (var ability in GetCurrentCombatUnitAbilities())
            {
                var abilityAction = BattleActionSelection.ForAbility(ability);
                if (IsActionAvailable(abilityAction))
                {
                    actions.Add(abilityAction);
                }
            }

            return actions;
        }

        /// <summary>
        /// 返回目标优先入口中与已选目标兼容的行动。
        /// </summary>
        public IReadOnlyList<BattleActionSelection> GetAvailableActionsForSelectedTarget()
        {
            return SelectedTarget == null
                ? Array.Empty<BattleActionSelection>()
                : GetAvailableActionsForTarget(SelectedTarget);
        }

        /// <summary>
        /// 选择行动并保留目标优先路径中已经验证兼容的目标。
        /// </summary>
        public bool TrySelectAction(BattleActionSelection action, out string rejectionReason)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (_battleSession.IsBattleEnded)
            {
                rejectionReason = "Battle has already ended.";
                return false;
            }

            if (SelectedAction != null)
            {
                rejectionReason =
                    "Confirm or cancel the selected action before choosing another action.";
                return false;
            }

            if (!IsActionAvailable(action))
            {
                rejectionReason = "The selected action is not currently available.";
                return false;
            }

            if (SelectedTarget != null && !IsValidTarget(action, SelectedTarget))
            {
                rejectionReason =
                    "The selected action cannot use the currently selected target.";
                return false;
            }

            _stateMachine.SelectAction(action);
            rejectionReason = null;
            return true;
        }

        /// <summary>
        /// 根据当前状态将单位解释为行动目标，失败时不修改原有选择。
        /// </summary>
        public bool TrySelectUnitTarget(CombatUnit targetUnit, out string rejectionReason)
        {
            if (targetUnit == null)
            {
                throw new ArgumentNullException(nameof(targetUnit));
            }

            var target = BattleActionTargetSelection.ForUnit(targetUnit);
            switch (State)
            {
                case BattleActionSelectionState.ActionList:
                    if (!CanUseAnyActionOnTarget(target))
                    {
                        rejectionReason =
                            "No available action can target the selected combat unit.";
                        return false;
                    }

                    break;
                case BattleActionSelectionState.ActionSelected:
                    if (!IsValidTarget(SelectedAction, target))
                    {
                        rejectionReason = "Select a highlighted valid action target.";
                        return false;
                    }

                    break;
                case BattleActionSelectionState.TargetSelected:
                    rejectionReason =
                        "Select an available action or cancel the selected target.";
                    return false;
                case BattleActionSelectionState.AwaitingConfirmation:
                    rejectionReason =
                        "Confirm or cancel the selected action before changing input.";
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _stateMachine.SelectTarget(target);
            rejectionReason = null;
            return true;
        }

        /// <summary>
        /// 提交已验证的行动与目标；成功后复位，失败时保留仍然有效的部分选择。
        /// </summary>
        public BattleActionResult Confirm()
        {
            if (!CanConfirm)
            {
                throw new InvalidOperationException(
                    "An action and a compatible target are required before confirmation.");
            }

            var result = SelectedAction.Kind switch
            {
                BattleActionSelectionKind.BasicAttack =>
                    _battleSession.AttackTargetCombatUnit(SelectedTarget.Unit),
                BattleActionSelectionKind.ActiveAbility =>
                    _battleSession.TryUseAbility(
                        SelectedAction.Ability,
                        SelectedTarget.Unit),
                _ => throw new ArgumentOutOfRangeException(),
            };

            if (result.IsSuccess)
            {
                Reset();
            }
            else
            {
                Refresh();
            }

            return result;
        }

        /// <summary>
        /// 战斗数据变化后移除失效目标，行动选择仍保留给玩家取消或重新选目标。
        /// </summary>
        public void Refresh()
        {
            if (SelectedTarget != null && !CanUseAnyActionOnTarget(SelectedTarget))
            {
                _stateMachine.ClearTarget();
            }

            if (_stateMachine.CanConfirm &&
                !IsValidTarget(SelectedAction, SelectedTarget))
            {
                _stateMachine.ClearTarget();
            }
        }

        public void Cancel()
        {
            _stateMachine.Cancel();
        }

        public void Reset()
        {
            _stateMachine.Reset();
        }

        private IReadOnlyList<BattleActionSelection> GetAvailableActionsForTarget(
            BattleActionTargetSelection target)
        {
            var actions = new List<BattleActionSelection>();
            var basicAttack = BattleActionSelection.ForBasicAttack();
            if (IsValidTarget(basicAttack, target))
            {
                actions.Add(basicAttack);
            }

            foreach (var ability in GetCurrentCombatUnitAbilities())
            {
                var abilityAction = BattleActionSelection.ForAbility(ability);
                if (IsValidTarget(abilityAction, target))
                {
                    actions.Add(abilityAction);
                }
            }

            return actions;
        }

        private IReadOnlyList<AbilityDefinition> GetCurrentCombatUnitAbilities()
        {
            return _abilitiesByUnitId.TryGetValue(
                _battleSession.CurrentCombatUnit.UnitId,
                out var abilities)
                ? abilities
                : Array.Empty<AbilityDefinition>();
        }

        private bool CanUseAnyActionOnTarget(BattleActionTargetSelection target)
        {
            return GetAvailableActionsForTarget(target).Count > 0;
        }

        private bool IsActionAvailable(BattleActionSelection action)
        {
            var currentUnit = _battleSession.CurrentCombatUnit;
            if (_battleSession.IsBattleEnded || currentUnit.IsDead())
            {
                return false;
            }

            switch (action.Kind)
            {
                case BattleActionSelectionKind.BasicAttack:
                    return true;
                case BattleActionSelectionKind.ActiveAbility:
                    foreach (var ability in GetCurrentCombatUnitAbilities())
                    {
                        if (ReferenceEquals(ability, action.Ability))
                        {
                            return currentUnit.PilotRuntime.HasEnoughActionPoint(
                                ability.ActionPointCost);
                        }
                    }

                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool IsValidTarget(
            BattleActionSelection action,
            BattleActionTargetSelection target)
        {
            if (action == null ||
                target?.Kind != BattleActionTargetKind.Unit ||
                target.Unit == null)
            {
                return false;
            }

            return action.Kind switch
            {
                BattleActionSelectionKind.BasicAttack =>
                    ContainsTarget(
                        _battleSession.GetCurrentCombatUnitBasicAttackTargets(),
                        target.Unit),
                BattleActionSelectionKind.ActiveAbility =>
                    ContainsTarget(
                        _battleSession.GetCurrentCombatUnitAbilityTargets(action.Ability),
                        target.Unit),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        private static bool ContainsTarget(
            IReadOnlyList<CombatUnit> targets,
            CombatUnit target)
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
    }
}
