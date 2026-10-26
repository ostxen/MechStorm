using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MechStorm.Battle.Statistics;
using MechStorm.Battle.Units;
using MechStorm.Runtime;
using TEngine;
using UnityEngine.SceneManagement;

namespace GameLogic
{
    /// <summary>
    /// 正式战斗流程：管理准备、目标/阵营提示、HUD 与本场 Runtime 的生命周期。
    /// </summary>
    public sealed partial class ProcedureBattle : ProcedureBase, IDisposable
    {
        private const string SceneLocation = "Battle";
        private CancellationTokenSource _lifetime;
        private BattleRuntime _runtime;
        private LoadingUI _loading;
        private BattleHUDUI _hud;
        private BattleHUDPresenter _hudPresenter;
        private BattleStatisticsReport _statisticsReport;
        private int _announcedRound;
        private CombatFaction _announcedFaction;
        private Type _nextProcedureType;

        protected override void OnEnter(IFsm<IProcedureModule> fsm)
        {
            _nextProcedureType = null;
            _announcedRound = 0;
            _lifetime = new CancellationTokenSource();
            InitializeBattleAsync(_lifetime, _lifetime.Token).Forget();
        }

        protected override void OnUpdate(IFsm<IProcedureModule> fsm, float elapseSeconds, float realElapseSeconds)
        {
            if (_nextProcedureType != null)
            {
                ChangeState(fsm, _nextProcedureType);
                return;
            }

            UpdateBattleFlow();
        }

        private bool IsCurrent(CancellationTokenSource lifetime)
        {
            return ReferenceEquals(_lifetime, lifetime);
        }

        private async UniTask InitializeBattleAsync(CancellationTokenSource lifetime, CancellationToken cancellationToken)
        {
            var loading = await ShowWindowAsync<LoadingUI>("LoadingUI", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _loading = loading;
            if (!GameModule.Resource.CheckLocationValid(SceneLocation))
            {
                throw new GameFrameworkException("Battle 场景地址无效，请检查 YooAsset 收集配置。");
            }

            // TE 场景 API 不支持取消，等待真实操作结束；退出后不再更新 UI 或继续业务。
            var scene = await GameModule.Scene.LoadSceneAsync(SceneLocation, LoadSceneMode.Single,
                progressCallBack: progress =>
                {
                    if (IsCurrent(lifetime))
                    {
                        _loading.SetProgress(progress * 0.7f);
                    }
                });
            cancellationToken.ThrowIfCancellationRequested();
            if (!scene.IsValid())
            {
                throw new GameFrameworkException("Battle 场景加载失败。");
            }

            CreateRuntime(scene);
            _loading.SetProgress(0.85f);
            ChangeBattlePhase(BattleFlowPhase.Preparation);
        }

        private async UniTask ShowObjectiveAsync(CancellationToken cancellationToken)
        {
            var objective = await ShowWindowAsync<BattleObjectiveUI>("BattleObjectiveUI", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await objective.PlayAsync("全灭敌方单位", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            GameModule.UI.CloseUI<BattleObjectiveUI>();
        }

        private async UniTask ShowRoundAsync(CancellationToken cancellationToken)
        {
            int roundNumber = _runtime.Session.CurrentRoundNumber;
            var faction = _runtime.Session.CurrentFaction;
            var round = await ShowWindowAsync<BattleRoundIntroUI>("BattleRoundIntroUI", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await round.PlayAsync(roundNumber, faction == CombatFaction.TeamA, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            GameModule.UI.CloseUI<BattleRoundIntroUI>();
            _announcedRound = roundNumber;
            _announcedFaction = faction;
        }

        private async UniTask WaitForBattleStartAsync(CancellationToken cancellationToken)
        {
            var preparation = await ShowWindowAsync<BattlePreparationUI>("BattlePreparationUI", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var enterBattle = new UniTaskCompletionSource();
            preparation.EnterBattleRequested += () => enterBattle.TrySetResult();
            preparation.MenuRequested += OnBattleMenuRequested;
            await preparation.DisplayAsync(_runtime.Session.GetFactionCombatUnits(CombatFaction.TeamA), cancellationToken);
            _loading.SetProgress(1f);
            await UniTask.Yield(cancellationToken);
            CloseLoading();
            Log.Info("战前准备已就绪，等待点击进入战斗。");
            await enterBattle.Task.AttachExternalCancellation(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            GameModule.UI.CloseUI<BattlePreparationUI>();
        }

        private void OnBattleMenuRequested()
        {
            ShowBattleMenuAsync(_lifetime.Token).Forget();
        }

        private async UniTask ShowBattleMenuAsync(CancellationToken cancellationToken)
        {
            var menu = await ShowWindowAsync<BattleMenuUI>("BattleMenuUI", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            menu.ExitRequested += ReturnToMain;
            menu.RestartRequested += RestartBattle;
        }

        private async UniTask InitializeHUDAsync(CancellationToken cancellationToken)
        {
            var hud = await ShowWindowAsync<BattleHUDUI>("BattleHUDUI", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _hud = hud;
            int maxAbilityCount = 0;
            foreach (var abilities in _runtime.Scenario.AbilitiesByUnitId.Values)
            {
                maxAbilityCount = Math.Max(maxAbilityCount, abilities.Count);
            }
            await _hud.InitializeActionsAsync(maxAbilityCount, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _hudPresenter = new BattleHUDPresenter(_runtime.Session, _runtime.InteractionController,
                _runtime.UnitStatusPresenter, _hud, CanAcceptBattleInput);
        }

        private void ShowBattleHUD()
        {
            _runtime.InteractionController.SelectCurrentCombatUnit();
            _hudPresenter.RefreshIfChanged(true);
            _hud.SetDisplayEnabled(true);
        }

        private async UniTask ShowResultAsync(CancellationToken cancellationToken)
        {
            _statisticsReport = BattleStatisticsBuilder.Build(_runtime.Session);
            var result = await ShowWindowAsync<BattleResultUI>("BattleResultUI", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            result.ShowOutcome(_runtime.Session.Outcome.WinningFaction == CombatFaction.TeamA);
            result.StatisticsRequested += OnStatisticsRequested;
            result.ExitRequested += ReturnToMain;
            result.RestartRequested += RestartBattle;
        }

        private void OnStatisticsRequested()
        {
            if (GameModule.UI.HasWindow<BattleStatisticsUI>())
            {
                return;
            }
            ShowStatisticsAsync(_lifetime.Token).Forget();
        }

        private async UniTask ShowStatisticsAsync(CancellationToken cancellationToken)
        {
            var statistics = await ShowWindowAsync<BattleStatisticsUI>("BattleStatisticsUI", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            statistics.BackRequested += CloseStatistics;
            statistics.HomeRequested += ReturnToMain;
            await statistics.DisplayAsync(_statisticsReport);
        }

        private void CloseStatistics()
        {
            GameModule.UI.CloseUI<BattleStatisticsUI>();
        }

        private void ReturnToMain()
        {
            Dispose();
            // 下一次流程更新切回主界面，避免按钮或异步回调直接重入 FSM。
            _nextProcedureType = typeof(ProcedureMain);
        }

        private void RestartBattle()
        {
            Dispose();
            _nextProcedureType = typeof(ProcedureBattle);
        }

        private void CreateRuntime(Scene scene)
        {
            var bindings = GetSceneBindings(scene);
            _runtime = new BattleRuntime(bindings.ScenarioConfig, bindings.Plane, bindings.BattleCamera,
                bindings.UnitParent, bindings.CellSize, bindings.BoardOrigin, bindings.BoardColor);
            _runtime.InitializePresentation(bindings);
        }

        private static BattleSceneBindings GetSceneBindings(Scene scene)
        {
            BattleSceneBindings bindings = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var candidate in root.GetComponentsInChildren<BattleSceneBindings>(true))
                {
                    if (bindings != null)
                    {
                        throw new GameFrameworkException("Battle 场景只能有一个 BattleSceneBindings。");
                    }
                    bindings = candidate;
                }
            }
            if (bindings == null || !bindings.isActiveAndEnabled)
            {
                throw new GameFrameworkException("Battle 场景缺少启用的 BattleSceneBindings，请检查场景引用配置。");
            }
            bindings.Validate();
            return bindings;
        }

        private async UniTask<T> ShowWindowAsync<T>(string location, CancellationToken cancellationToken) where T : UIWindow, new()
        {
            if (!GameModule.Resource.CheckLocationValid(location))
            {
                throw new GameFrameworkException($"{location} 资源地址无效，请检查 YooAsset 收集配置。");
            }
            var window = await GameModule.UI.ShowUIAsyncAwait<T>().AttachExternalCancellation(cancellationToken);
            // TE 超时会返回未加载完成的窗口，此处显式报错，不自动退出战斗。
            // IsLoadDone 只表示资源加载完成，不保证后续绑定和 OnCreate 成功。
            if (!window.IsLoadDone)
            {
                throw new GameFrameworkException($"{location} 加载未完成，请检查 YooAsset 收集配置或加载耗时。");
            }
            return window;
        }

        protected override void OnLeave(IFsm<IProcedureModule> fsm, bool isShutdown)
        {
            Dispose();
        }

        protected override void OnDestroy(IFsm<IProcedureModule> fsm)
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_lifetime == null)
            {
                return;
            }

            var lifetime = _lifetime;
            _lifetime = null;
            lifetime.Cancel();
            lifetime.Dispose();
            ReleaseBattle();
            CloseLoading();
        }

        private void ReleaseBattle()
        {
            StopBattleFlow();
            GameModule.UI.CloseUI<SettingsUI>();
            GameModule.UI.CloseUI<BattleMenuUI>();
            GameModule.UI.CloseUI<BattlePreparationUI>();
            GameModule.UI.CloseUI<BattleObjectiveUI>();
            GameModule.UI.CloseUI<BattleRoundIntroUI>();
            GameModule.UI.CloseUI<BattleStatisticsUI>();
            GameModule.UI.CloseUI<BattleResultUI>();
            GameModule.UI.CloseUI<BattleHUDUI>();
            _statisticsReport = null;
            _hudPresenter?.Dispose();
            _hudPresenter = null;
            _hud = null;
            _runtime?.Dispose();
            _runtime = null;
        }

        private void CloseLoading()
        {
            GameModule.UI.CloseUI<LoadingUI>();
            _loading = null;
        }
    }
}
