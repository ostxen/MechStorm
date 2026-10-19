using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MechStorm.Presentation;
using TEngine;
using UnityEngine.SceneManagement;

namespace GameLogic
{
    /// <summary>
    /// 正式战斗流程：装配本场 Runtime 与 HUD，并按相反顺序释放。
    /// </summary>
    public sealed class ProcedureBattle : ProcedureBase, IDisposable
    {
        private const string SceneLocation = "Battle";
        private CancellationTokenSource _lifetime;
        private LoadingUI _loading;
        private BattleRuntime _runtime;
        private BattleHUDUI _hud;
        private BattleHUDPresenter _hudPresenter;
        private bool _active;
        private bool _loadFailed;
        private bool _ready;
        private bool _loadingRequested;
        private bool _hudRequested;

        protected override void OnEnter(IFsm<IProcedureModule> fsm)
        {
            _active = true;
            _loadFailed = false;
            _lifetime = new CancellationTokenSource();
            EnterBattleAsync(_lifetime, _lifetime.Token).Forget();
        }

        protected override void OnUpdate(IFsm<IProcedureModule> fsm, float elapseSeconds, float realElapseSeconds)
        {
            if (_active && _loadFailed)
            {
                ChangeState<ProcedureMain>(fsm);
                return;
            }
            if (!_active || !_ready)
            {
                return;
            }

            _runtime.Tick(_hud != null && _hud.IsReady && _hud.Visible);
            _hudPresenter?.RefreshIfChanged();
        }

        private bool IsCurrent(CancellationTokenSource lifetime)
        {
            return _active && ReferenceEquals(_lifetime, lifetime);
        }

        private async UniTask EnterBattleAsync(CancellationTokenSource lifetime, CancellationToken cancellationToken)
        {
            try
            {
                await OpenLoadingAsync(lifetime, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
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
                            _loading?.SetProgress(progress * 0.7f);
                        }
                    });
                cancellationToken.ThrowIfCancellationRequested();
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    throw new GameFrameworkException("Battle 场景加载失败。");
                }

                CreateRuntime(scene);
                _loading.SetProgress(0.85f);
                await OpenHUDAsync(lifetime, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                int maxAbilityCount = 0;
                foreach (var abilities in _runtime.Scenario.AbilitiesByUnitId.Values)
                {
                    maxAbilityCount = System.Math.Max(maxAbilityCount, abilities.Count);
                }
                await _hud.InitializeActionsAsync(maxAbilityCount, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _hudPresenter = new BattleHUDPresenter(_runtime.Session, _runtime.PresentationController,
                    _runtime.UnitPresenter, _hud);
                _hudPresenter.RefreshIfChanged();
                _loading.SetProgress(1f);
                await UniTask.Yield(cancellationToken);
                _ready = true;
                Log.Info("Battle Runtime 与 HUD 已就绪。");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (IsCurrent(lifetime) && !cancellationToken.IsCancellationRequested)
                {
                    ReleaseBattle();
                    Log.Error($"战斗初始化失败，将恢复主界面：{exception.Message}");
                    _loadFailed = true;
                }
            }
            finally
            {
                if (IsCurrent(lifetime))
                {
                    CloseLoading();
                }
            }
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

        private async UniTask OpenLoadingAsync(CancellationTokenSource lifetime, CancellationToken cancellationToken)
        {
            _loadingRequested = true;
            var loading = await GameModule.UI.ShowUIAsyncAwait<LoadingUI>().AttachExternalCancellation(cancellationToken);
            if (loading == null)
            {
                throw new GameFrameworkException("LoadingUI 创建失败。");
            }

            // TE 等待 API 可能返回未完成绑定的窗口，需检查 OnCreate 是否完成。
            await UniTask.WaitUntil(() => loading.IsDestroyed || loading.IsReady, cancellationToken: cancellationToken);
            if (loading.IsDestroyed)
            {
                throw new GameFrameworkException("LoadingUI 在加载前已被关闭。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (IsCurrent(lifetime))
            {
                _loading = loading;
            }
        }

        private async UniTask OpenHUDAsync(CancellationTokenSource lifetime, CancellationToken cancellationToken)
        {
            if (!GameModule.Resource.CheckLocationValid("BattleHUDUI"))
            {
                throw new GameFrameworkException("BattleHUDUI 资源地址无效，请检查 YooAsset 收集配置。");
            }
            _hudRequested = true;
            var hud = await GameModule.UI.ShowUIAsyncAwait<BattleHUDUI>().AttachExternalCancellation(cancellationToken);
            if (hud == null)
            {
                throw new GameFrameworkException("BattleHUDUI 创建失败。");
            }
            await UniTask.WaitUntil(() => hud.IsDestroyed || hud.IsReady, cancellationToken: cancellationToken);
            if (hud.IsDestroyed)
            {
                throw new GameFrameworkException("BattleHUDUI 在初始化完成前已被关闭。");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (IsCurrent(lifetime))
            {
                _hud = hud;
            }
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
            if (!_active)
            {
                return;
            }

            _active = false;
            _ready = false;
            var lifetime = _lifetime;
            _lifetime = null;
            lifetime?.Cancel();
            lifetime?.Dispose();
            ReleaseBattle();
            CloseLoading();
        }

        private void ReleaseBattle()
        {
            _ready = false;
            _hudPresenter?.Dispose();
            _hudPresenter = null;
            if (_hudRequested)
            {
                GameModule.UI.CloseUI<BattleHUDUI>();
                _hudRequested = false;
            }
            _hud = null;
            _runtime?.Dispose();
            _runtime = null;
        }

        private void CloseLoading()
        {
            if (_loadingRequested)
            {
                GameModule.UI.CloseUI<LoadingUI>();
                _loadingRequested = false;
            }
            _loading = null;
        }
    }
}
