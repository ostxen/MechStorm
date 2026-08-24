using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using MechStorm.Battle;
using MechStorm.Battle.Actions;
using MechStorm.Battle.Data;
using MechStorm.Battle.Diagnostics;
using MechStorm.Battle.Units;
using MechStorm.Presentation.Controllers;
using TEngine;
using UnityEngine;

namespace MechStorm.Presentation
{
    /// <summary>
    /// 为临时战斗 HUD 提供只读显示数据和调试操作，不直接实现战斗规则。
    /// </summary>
    public sealed class TempBattleDebugActions
    {
        private readonly BattlePresentationController _presentationController;
        private readonly BattleSession _battleSession;
        private readonly CombatUnit _debugDamageTarget;
        private readonly IReadOnlyDictionary<int, IReadOnlyList<AbilityDefinition>> _abilitiesByUnitId;
        private readonly TempBattleUnitPresenter _unitPresenter;
        private readonly int _debugDamage;

        public TempBattleDebugActions(
            BattleSession battleSession,
            BattlePresentationController presentationController,
            CombatUnit debugDamageTarget,
            IReadOnlyDictionary<int, IReadOnlyList<AbilityDefinition>> abilitiesByUnitId,
            TempBattleUnitPresenter unitPresenter,
            int debugDamage)
        {
            _battleSession = battleSession ?? throw new ArgumentNullException(nameof(battleSession));
            _presentationController = presentationController ??
                throw new ArgumentNullException(nameof(presentationController));
            _debugDamageTarget = debugDamageTarget ??
                throw new ArgumentNullException(nameof(debugDamageTarget));
            _abilitiesByUnitId = abilitiesByUnitId ??
                throw new ArgumentNullException(nameof(abilitiesByUnitId));
            _unitPresenter = unitPresenter ?? throw new ArgumentNullException(nameof(unitPresenter));
            _debugDamage = debugDamage;
        }

        /// <summary>
        /// HUD 使用该值统一禁用会改变战场或回合的按钮。
        /// </summary>
        public bool IsBattleEnded => _battleSession.IsBattleEnded;

        public string BattleOutcomeText => _battleSession.Outcome.WinningFaction switch
        {
            CombatFaction.TeamA => "我方胜利",
            CombatFaction.TeamB => "敌方胜利",
            _ => "对局结束",
        };

        public bool IsCurrentUnitSelected => _presentationController.IsCurrentUnitSelected;

        public bool IsActionInputLocked => _presentationController.IsActionInputLocked;

        public BattleActionSelectionState ActionSelectionState =>
            _presentationController.ActionSelectionState;

        public BattleActionSelectionPath ActionSelectionPath =>
            _presentationController.ActionSelectionPath;

        public int CurrentRoundNumber => _battleSession.CurrentRoundNumber;

        public string CurrentFactionName => _battleSession.CurrentFaction switch
        {
            CombatFaction.TeamA => "我方",
            CombatFaction.TeamB => "敌方",
            _ => "中立",
        };

        public string CurrentUnitName => _battleSession.CurrentCombatUnit.Pilot.Name;

        public int CurrentUnitActionPoint =>
            _battleSession.CurrentCombatUnit.PilotRuntime.CurrentActionPoint;

        public int CurrentUnitMaxActionPoint =>
            _battleSession.CurrentCombatUnit.Pilot.MaxActionPoint;

        public int CurrentUnitDurability =>
            _battleSession.CurrentCombatUnit.MechRuntime.CurrentDurability;

        public int CurrentUnitMaxDurability =>
            _battleSession.CurrentCombatUnit.Mech.MaxDurability;

        public int CurrentUnitId => _battleSession.CurrentCombatUnit.UnitId;

        public int DebugDamage => _debugDamage;

        public string SelectedActionName =>
            GetActionName(_presentationController.SelectedAction);

        public string SelectedTargetName =>
            _presentationController.SelectedTarget?.Unit?.Pilot.Name;

        public TempBattleActionPanelState ActionPanelState =>
            TempBattleActionPanelState.Create(
                IsCurrentUnitSelected,
                IsBattleEnded,
                ActionSelectionState,
                ActionSelectionPath,
                _presentationController.SelectedAction?.Ability?.RequiresBasicWeapon == true,
                _presentationController.CanConfirmSelectedAction,
                SelectedActionName,
                SelectedTargetName);

        public IReadOnlyList<TempBattleActionOption> GetActionCatalog()
        {
            var availableActions = _presentationController.GetAvailableActions();
            var selectedAction = _presentationController.SelectedAction;
            var basicAttackSelection = BattleActionSelection.ForBasicAttack();
            var availableBasicAttack =
                FindAction(availableActions, basicAttackSelection);
            var basicAttackAction = availableBasicAttack ?? basicAttackSelection;
            var options = new List<TempBattleActionOption>
            {
                CreateActionOption(
                    basicAttackAction,
                    availableBasicAttack != null,
                    basicAttackAction.Matches(selectedAction)),
            };

            foreach (var ability in GetCurrentCombatUnitAbilities())
            {
                var abilitySelection = BattleActionSelection.ForAbility(ability);
                var availableAction = FindAction(availableActions, abilitySelection);
                var action = availableAction ?? abilitySelection;
                options.Add(CreateActionOption(
                    action,
                    availableAction != null,
                    action.Matches(selectedAction)));
            }

            return options;
        }

        public IReadOnlyList<TempBattleActionOption> GetActionsForSelectedTarget()
        {
            var actions = _presentationController.GetAvailableActionsForSelectedTarget();
            var options = new List<TempBattleActionOption>(actions.Count);
            foreach (var action in actions)
            {
                options.Add(CreateActionOption(
                    action,
                    true,
                    action.Matches(_presentationController.SelectedAction),
                    action.Kind == BattleActionSelectionKind.BasicAttack));
            }

            return options;
        }

        public TempBattleActionOption GetSelectedAction()
        {
            var selectedAction = _presentationController.SelectedAction;
            return selectedAction == null
                ? null
                : CreateActionOption(selectedAction, true, true);
        }

        public void Tick()
        {
            var inputAction = _presentationController.Tick(out var actionUnit);
            var actionResult = _presentationController.LastActionResult;
            switch (inputAction)
            {
                case BattleInputAction.CurrentUnitSelected:
                    Log.Info($"[MechStorm] Selected current unit {actionUnit.Pilot.Name}.");
                    break;
                case BattleInputAction.CurrentUnitChanged:
                    Log.Info(
                        $"[MechStorm] Switched current unit from " +
                        $"{actionResult.ActorUnit.Pilot.Name} to " +
                        $"{actionResult.NextCombatUnit.Pilot.Name}.");
                    break;
                case BattleInputAction.CurrentUnitMoved:
                    Log.Info(
                        $"[MechStorm] {actionResult.ActorUnit.Pilot.Name} moved to " +
                        $"GridPosition: {actionResult.PositionAfter.Value}");
                    break;
                case BattleInputAction.TargetSelected:
                    Log.Info(
                        $"[MechStorm] Action target selected: " +
                        $"{_presentationController.SelectedTarget.Unit.Pilot.Name}.");
                    break;
                case BattleInputAction.ActionRejected:
                    Log.Warning(
                        $"[MechStorm] Battle input rejected: " +
                        $"{_presentationController.LastRejectionReason}");
                    break;
            }
        }

        /// <summary>
        /// 执行临时扣血操作；对局结束后拒绝执行，避免调试按钮继续修改最终结果。
        /// </summary>
        public void ApplyDebugDamageToPlayerA()
        {
            if (_battleSession.IsBattleEnded)
            {
                Log.Warning("[MechStorm] Debug damage rejected: battle has already ended.");
                return;
            }

            if (_debugDamage <= 0)
            {
                Log.Warning("[MechStorm] Debug damage must be greater than zero.");
                return;
            }

            if (_debugDamageTarget.MechRuntime.IsDestroyed)
            {
                Log.Info($"[MechStorm] {_debugDamageTarget.Pilot.Name} is already destroyed.");
                return;
            }

            _debugDamageTarget.MechRuntime.TakeDamage(_debugDamage);
            _unitPresenter.Refresh(_debugDamageTarget);
            Log.Info(
                $"[MechStorm] {_debugDamageTarget.Pilot.Name} took {_debugDamage} damage, " +
                $"HP={_debugDamageTarget.MechRuntime.CurrentDurability}/" +
                $"{_debugDamageTarget.Mech.MaxDurability}");
        }

        public void SelectAction(TempBattleActionOption option)
        {
            if (option == null)
            {
                throw new ArgumentNullException(nameof(option));
            }

            if (!option.IsAvailable)
            {
                return;
            }

            var selected = option.Selection.Kind switch
            {
                BattleActionSelectionKind.BasicAttack =>
                    _presentationController.SelectBasicAttack(),
                BattleActionSelectionKind.ActiveAbility =>
                    _presentationController.SelectAbility(
                        option.Selection.Ability,
                        option.Selection.RangeSource),
                _ => throw new ArgumentOutOfRangeException(),
            };
            if (!selected)
            {
                Log.Warning(
                    $"[MechStorm] 无法选择 {option.Title}：" +
                    $"{_presentationController.LastRejectionReason}");
                return;
            }

            Log.Info(_presentationController.CanConfirmSelectedAction
                ? $"[MechStorm] 已为 {SelectedTargetName} 选择 {option.Title}，请确认。"
                : $"[MechStorm] 已选择 {option.Title}，请选择范围内目标。");
        }

        public void ConfirmSelectedAction()
        {
            if (!_presentationController.CanConfirmSelectedAction)
            {
                return;
            }

            var result = _presentationController.ConfirmSelectedAction();
            if (!result.IsSuccess)
            {
                Log.Warning(
                    $"[MechStorm] 行动执行失败：{result.FailureReason}");
                return;
            }

            if (result.ActionType == BattleActionType.Attack)
            {
                RefreshAndLogAttack(result);
                return;
            }

            RefreshAndLogAbility(result);
        }

        public void CancelActionSelection()
        {
            if (!_presentationController.IsActionInputLocked)
            {
                return;
            }

            _presentationController.CancelActionSelection();
            Log.Info("[MechStorm] 已取消行动与目标选择。");
        }

        /// <summary>
        /// 请求结束当前单位行动；失败时不读取不存在的下一单位。
        /// </summary>
        public void EndCurrentUnitAction()
        {
            if (IsActionInputLocked)
            {
                Log.Warning("[MechStorm] Confirm or cancel the selected action before waiting.");
                return;
            }

            LogCurrentBattleState("Before End Action");
            var result = _presentationController.EndCurrentUnitAction();
            if (!result.IsSuccess)
            {
                Log.Warning($"[MechStorm] End action rejected: {result.FailureReason}");
                return;
            }

            Log.Info(
                $"[MechStorm] {result.ActorUnit.Pilot.Name} ended action, " +
                $"Next={result.NextCombatUnit.Pilot.Name}, " +
                $"Round={result.RoundNumberBefore}->{result.RoundNumberAfter}, " +
                $"Faction={result.FactionBefore}->{result.FactionAfter}");
            _unitPresenter.RefreshAll();
            LogCurrentBattleState("After End Action");
        }

        /// <summary>
        /// 请求结束当前阵营回合；失败时不读取不存在的下一单位。
        /// </summary>
        public void EndCurrentFactionTurn()
        {
            if (IsActionInputLocked)
            {
                Log.Warning("[MechStorm] Confirm or cancel the selected action before ending the faction turn.");
                return;
            }

            LogCurrentBattleState("Before End Faction Turn");
            var result = _presentationController.EndCurrentFactionTurn();
            if (!result.IsSuccess)
            {
                Log.Warning($"[MechStorm] End faction turn rejected: {result.FailureReason}");
                return;
            }

            Log.Info(
                $"[MechStorm] {result.FactionBefore} turn ended by " +
                $"{result.ActorUnit.Pilot.Name}, " +
                $"Next={result.NextCombatUnit.Pilot.Name}, " +
                $"Round={result.RoundNumberBefore}->{result.RoundNumberAfter}, " +
                $"Faction={result.FactionBefore}->{result.FactionAfter}");
            _unitPresenter.RefreshAll();
            LogCurrentBattleState("After End Faction Turn");
        }

        public void LogCurrentBattleState()
        {
            LogCurrentBattleState("Current Battle State");
        }

        public void ExportBattleDebugJson()
        {
            ExportBattleDebugJsonAsync().Forget();
        }

        private void RefreshAndLogAttack(BattleActionResult result)
        {
            _unitPresenter.Refresh(result.TargetUnit);
            Log.Info(
                $"[MechStorm] {result.ActorUnit.Pilot.Name} attacked " +
                $"{result.TargetUnit.Pilot.Name}, " +
                $"AP={result.ActionPointBefore}->{result.ActionPointAfter}, " +
                $"HP={result.DurabilityBefore}->{result.DurabilityAfter}/" +
                $"{result.TargetUnit.Mech.MaxDurability}");
            LogDestroyedUnit(result.TargetUnit, result.DurabilityAfter.GetValueOrDefault());
        }

        private void RefreshAndLogAbility(BattleActionResult result)
        {
            _unitPresenter.Refresh(result.ActorUnit);
            _unitPresenter.Refresh(result.TargetUnit);
            var ability = FindAbility(result.ActorUnit.UnitId, result.AbilityId);
            var abilityName = ability?.Name ?? $"Ability {result.AbilityId}";
            Log.Info(
                $"[MechStorm] {result.ActorUnit.Pilot.Name} used " +
                $"{abilityName} on {result.TargetUnit.Pilot.Name}, " +
                $"AP={result.ActionPointBefore}->{result.ActionPointAfter}, " +
                $"HP={result.DurabilityBefore}->{result.DurabilityAfter}/" +
                $"{result.TargetUnit.Mech.MaxDurability}");
            LogDestroyedUnit(result.TargetUnit, result.DurabilityAfter.GetValueOrDefault());
        }

        private IReadOnlyList<AbilityDefinition> GetCurrentCombatUnitAbilities()
        {
            return _abilitiesByUnitId.TryGetValue(
                _battleSession.CurrentCombatUnit.UnitId,
                out var abilities)
                ? abilities
                : Array.Empty<AbilityDefinition>();
        }

        private TempBattleActionOption CreateActionOption(
            BattleActionSelection action,
            bool isAvailable,
            bool isSelected,
            bool showAsWeaponSkill = false)
        {
            if (action.Kind == BattleActionSelectionKind.BasicAttack)
            {
                var basicAttack = _battleSession.CurrentCombatUnit.Mech.BasicAttack;
                return new TempBattleActionOption(
                    action,
                    showAsWeaponSkill ? "基础武器技能" : "默认基础武器",
                    $"射程 {basicAttack.MinRange}-{basicAttack.MaxRange} · 伤害 {basicAttack.Damage}",
                    basicAttack.ActionPointCost,
                    isAvailable,
                    isSelected);
            }

            var ability = action.Ability;
            var weaponData = _battleSession.CurrentCombatUnit.Mech.BasicAttack;
            var useWeaponRange =
                action.RangeSource == AbilityTargetRangeSource.BasicWeapon;
            var minRange = useWeaponRange
                ? weaponData.MinRange
                : ability.MinRange;
            var maxRange = useWeaponRange
                ? weaponData.MaxRange
                : ability.MaxRange;
            return new TempBattleActionOption(
                action,
                ability.Name,
                $"AP {ability.ActionPointCost} · 射程 {minRange}-{maxRange} · " +
                $"伤害 {ability.Damage}",
                ability.ActionPointCost,
                isAvailable,
                isSelected);
        }

        private static string GetActionName(BattleActionSelection action)
        {
            if (action == null)
            {
                return null;
            }

            return action.Kind == BattleActionSelectionKind.BasicAttack
                ? "基础武器技能"
                : action.Ability.Name;
        }

        private static BattleActionSelection FindAction(
            IReadOnlyList<BattleActionSelection> actions,
            BattleActionSelection selection)
        {
            foreach (var action in actions)
            {
                if (action.Matches(selection))
                {
                    return action;
                }
            }

            return null;
        }

        private AbilityDefinition FindAbility(int unitId, int? abilityId)
        {
            if (!_abilitiesByUnitId.TryGetValue(unitId, out var abilities))
            {
                return null;
            }

            foreach (var ability in abilities)
            {
                if (!abilityId.HasValue || ability.AbilityId == abilityId.Value)
                {
                    return ability;
                }
            }

            return null;
        }

        private void LogDestroyedUnit(CombatUnit unit, int durability)
        {
            if (durability <= 0)
            {
                Log.Info($"[MechStorm] {unit.Pilot.Name} was destroyed.");
            }
        }

        private void LogCurrentBattleState(string label)
        {
            var currentUnit = _battleSession.CurrentCombatUnit;
            Log.Info(
                $"[MechStorm] {label}: " +
                $"Round={_battleSession.CurrentRoundNumber}, " +
                $"Faction={_battleSession.CurrentFaction}, " +
                $"Unit={currentUnit.Pilot.Name}/{currentUnit.Mech.Name}");
        }

        private async UniTask ExportBattleDebugJsonAsync()
        {
            try
            {
                var snapshot = _battleSession.CreateSnapshot();
                var json = BattleDebugJsonSerializer.Serialize(snapshot, _battleSession.ActionLogs);
                var directoryPath = GetBattleDebugDirectoryPath();
                Directory.CreateDirectory(directoryPath);

                var fileName = $"battle_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json";
                var filePath = Path.Combine(directoryPath, fileName);
                await File.WriteAllTextAsync(filePath, json);

                Log.Info($"[MechStorm] Battle debug JSON exported to: {filePath}");
            }
            catch (Exception exception)
            {
                Log.Error($"[MechStorm] Failed to export battle debug JSON: {exception}");
            }
        }

        private static string GetBattleDebugDirectoryPath()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "BattleDebug"));
#else
            return Path.Combine(Application.persistentDataPath, "BattleDebug");
#endif
        }
    }
}
