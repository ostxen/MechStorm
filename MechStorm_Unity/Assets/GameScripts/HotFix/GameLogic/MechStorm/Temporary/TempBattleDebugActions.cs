using System;
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
    public sealed class TempBattleDebugActions
    {
        private readonly BattlePresentationController _presentationController;
        private readonly BattleSession _battleSession;
        private readonly CombatUnit _playerAUnit;
        private readonly AbilityDefinition _debugAbility;
        private readonly TempBattleUnitPresenter _unitPresenter;
        private readonly int _debugDamage;

        public TempBattleDebugActions(
            BattleSession battleSession,
            BattlePresentationController presentationController,
            CombatUnit playerAUnit,
            AbilityDefinition debugAbility,
            TempBattleUnitPresenter unitPresenter,
            int debugDamage)
        {
            _battleSession = battleSession ?? throw new ArgumentNullException(nameof(battleSession));
            _presentationController = presentationController ??
                throw new ArgumentNullException(nameof(presentationController));
            _playerAUnit = playerAUnit ?? throw new ArgumentNullException(nameof(playerAUnit));
            _debugAbility = debugAbility ?? throw new ArgumentNullException(nameof(debugAbility));
            _unitPresenter = unitPresenter ?? throw new ArgumentNullException(nameof(unitPresenter));
            _debugDamage = debugDamage;
        }

        public bool IsBattleReady => _battleSession != null;

        public bool IsBattleEnded => _battleSession.IsBattleEnded;

        public bool IsCurrentUnitSelected => _presentationController.IsCurrentUnitSelected;

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

        public string AbilityName => _debugAbility.Name;

        public int AbilityActionPointCost => _debugAbility.ActionPointCost;

        public int AbilityDamage => _debugAbility.Damage;

        public int DebugDamage => _debugDamage;

        public bool IsAbilityTargeting =>
            _presentationController?.IsAbilityTargeting ?? false;

        public bool CanConfirmAbilityTargeting =>
            _presentationController?.CanConfirmAbilityTargeting ?? false;

        public string SelectedAbilityTargetName =>
            _presentationController?.SelectedAbilityTarget?.Pilot.Name;

        public bool CanConfirmBasicAttack => _presentationController.CanConfirmBasicAttack;

        public string SelectedAttackTargetName =>
            _presentationController.SelectedAttackTarget?.Pilot.Name;

        public string AbilityUnavailableReason
        {
            get
            {
                if (_presentationController == null)
                {
                    return "战斗尚未就绪";
                }

                if (_battleSession.IsBattleEnded)
                {
                    return "对局已经结束";
                }

                var currentUnit = _battleSession.CurrentCombatUnit;
                if (currentUnit.IsDead())
                {
                    return "当前单位无法行动";
                }

                var currentActionPoint = currentUnit.PilotRuntime.CurrentActionPoint;
                if (currentActionPoint < _debugAbility.ActionPointCost)
                {
                    return $"AP 不足：需要 {_debugAbility.ActionPointCost}，当前 {currentActionPoint}";
                }

                return _battleSession.GetCurrentCombatUnitAbilityTargets(_debugAbility).Count == 0
                    ? "范围内没有可用目标"
                    : null;
            }
        }

        public string AbilitySummary =>
            $"{_debugAbility.Name}（AP {_debugAbility.ActionPointCost}，" +
            $"射程 {_debugAbility.MinRange}-{_debugAbility.MaxRange}，" +
            $"伤害 {_debugAbility.Damage}）";

        public void Tick()
        {
            if (_presentationController == null)
            {
                return;
            }

            var inputAction = _presentationController.Tick(out var actionUnit);
            var actionResult = _presentationController.LastActionResult;
            switch (inputAction)
            {
                case BattleInputAction.CurrentUnitSelected:
                    Log.Info($"[MechStorm] Selected current unit {actionUnit.Pilot.Name}.");
                    break;
                case BattleInputAction.CurrentUnitMoved:
                    Log.Info(
                        $"[MechStorm] {actionResult.ActorUnit.Pilot.Name} moved to " +
                        $"GridPosition: {actionResult.PositionAfter.Value}");
                    break;
                case BattleInputAction.AttackTargetSelected:
                    Log.Info(
                        $"[MechStorm] Attack target selected: " +
                        $"{_presentationController.SelectedAttackTarget.Pilot.Name}.");
                    break;
                case BattleInputAction.AbilityTargetSelected:
                    Log.Info(
                        $"[MechStorm] Ability target selected: " +
                        $"{_presentationController.SelectedAbilityTarget.Pilot.Name}.");
                    break;
                case BattleInputAction.TargetAbilityUsed:
                    RefreshAndLogAbility(actionResult);
                    break;
                case BattleInputAction.ActionRejected:
                    Log.Warning(
                        $"[MechStorm] Battle input rejected: " +
                        $"{_presentationController.LastRejectionReason}");
                    break;
            }
        }

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

            if (_playerAUnit.MechRuntime.IsDestroyed)
            {
                Log.Info("[MechStorm] PlayerA is already destroyed.");
                return;
            }

            _playerAUnit.MechRuntime.TakeDamage(_debugDamage);
            _unitPresenter.Refresh(_playerAUnit);
            Log.Info(
                $"[MechStorm] PlayerA took {_debugDamage} damage, " +
                $"HP={_playerAUnit.MechRuntime.CurrentDurability}/{_playerAUnit.Mech.MaxDurability}");
        }

        public void ConfirmBasicAttack()
        {
            if (!_presentationController.CanConfirmBasicAttack)
            {
                return;
            }

            var result = _presentationController.ConfirmBasicAttack();
            if (!result.IsSuccess)
            {
                Log.Warning($"[MechStorm] Basic attack rejected: {result.FailureReason}");
                return;
            }

            RefreshAndLogAttack(result);
        }

        public void BeginAbilityTargeting()
        {
            var unavailableReason = AbilityUnavailableReason;
            if (!string.IsNullOrEmpty(unavailableReason))
            {
                Log.Warning($"[MechStorm] 无法使用 {_debugAbility.Name}：{unavailableReason}。");
                return;
            }

            if (_presentationController.IsAbilityTargeting)
            {
                return;
            }

            _presentationController.BeginAbilityTargeting(_debugAbility);
            Log.Info($"[MechStorm] 已选择 {AbilitySummary}，请选择范围内目标。");
        }

        public void CancelAbilityTargeting()
        {
            if (_presentationController?.IsAbilityTargeting != true)
            {
                return;
            }

            _presentationController.CancelAbilityTargeting();
            Log.Info("[MechStorm] 已取消技能目标选择。");
        }

        public void ConfirmAbilityTargeting()
        {
            if (_presentationController?.CanConfirmAbilityTargeting != true)
            {
                return;
            }

            var result = _presentationController.ConfirmAbilityTargeting();
            if (!result.IsSuccess)
            {
                Log.Warning(
                    $"[MechStorm] 技能释放失败：" +
                    $"{result.FailureReason}");
                return;
            }

            RefreshAndLogAbility(result);
        }

        public void EndCurrentUnitAction()
        {
            LogCurrentBattleState("Before End Action");
            var result = _presentationController != null
                ? _presentationController.EndCurrentUnitAction()
                : _battleSession.EndCurrentUnitAction();
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

        public void EndCurrentFactionTurn()
        {
            LogCurrentBattleState("Before End Faction Turn");
            var result = _presentationController != null
                ? _presentationController.EndCurrentFactionTurn()
                : _battleSession.EndCurrentFactionTurn();
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
                $"HP={result.DurabilityBefore}->{result.DurabilityAfter}/" +
                $"{result.TargetUnit.Mech.MaxDurability}");
            LogDestroyedUnit(result.TargetUnit, result.DurabilityAfter.GetValueOrDefault());
        }

        private void RefreshAndLogAbility(BattleActionResult result)
        {
            _unitPresenter.Refresh(result.ActorUnit);
            _unitPresenter.Refresh(result.TargetUnit);
            Log.Info(
                $"[MechStorm] {result.ActorUnit.Pilot.Name} used " +
                $"{_debugAbility.Name} on {result.TargetUnit.Pilot.Name}, " +
                $"AP={result.ActionPointBefore}->{result.ActionPointAfter}, " +
                $"HP={result.DurabilityBefore}->{result.DurabilityAfter}/" +
                $"{result.TargetUnit.Mech.MaxDurability}");
            LogDestroyedUnit(result.TargetUnit, result.DurabilityAfter.GetValueOrDefault());
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
