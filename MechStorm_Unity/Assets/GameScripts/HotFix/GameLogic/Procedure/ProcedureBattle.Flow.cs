using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace GameLogic
{
    /// <summary>
    /// 客户端战斗内部阶段，不包含 Procedure 初始化时的资源加载。
    /// </summary>
    public enum BattleFlowPhase
    {
        Preparation,
        Starting,
        Playing,
        TurnTransition,
        Result,
    }

    public sealed partial class ProcedureBattle
    {
        /// <summary>
        /// 初始化资源期间及退出后为 null，资源就绪后从 Preparation 开始。
        /// </summary>
        public BattleFlowPhase? CurrentPhase { get; private set; }

        private void ChangeBattlePhase(BattleFlowPhase next)
        {
            if (!CanChangeBattlePhase(CurrentPhase, next))
            {
                throw new InvalidOperationException($"非法战斗阶段转换：{CurrentPhase} -> {next}。");
            }

            var previous = CurrentPhase;
            StopBattleFlow();
            CurrentPhase = next;
            Log.Info($"战斗阶段：{previous?.ToString() ?? "初始化"} -> {next}");
            RunBattlePhaseAsync(next, _lifetime.Token).Forget();
        }

        private static bool CanChangeBattlePhase(BattleFlowPhase? current, BattleFlowPhase next)
        {
            switch (current)
            {
                case null:
                    return next == BattleFlowPhase.Preparation;
                case BattleFlowPhase.Preparation:
                    return next == BattleFlowPhase.Starting;
                case BattleFlowPhase.Starting:
                    return next == BattleFlowPhase.Playing;
                case BattleFlowPhase.Playing:
                    return next == BattleFlowPhase.TurnTransition || next == BattleFlowPhase.Result;
                case BattleFlowPhase.TurnTransition:
                    return next == BattleFlowPhase.Playing;
                default:
                    return false;
            }
        }

        private async UniTask RunBattlePhaseAsync(BattleFlowPhase phase, CancellationToken cancellationToken)
        {
            // 阶段工作完成后才推进，离场统一使用本场 token 取消。
            var next = await EnterBattlePhaseAsync(phase, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (next.HasValue)
            {
                ChangeBattlePhase(next.Value);
            }
        }

        private async UniTask<BattleFlowPhase?> EnterBattlePhaseAsync(BattleFlowPhase phase, CancellationToken cancellationToken)
        {
            switch (phase)
            {
                case BattleFlowPhase.Preparation:
                    await WaitForBattleStartAsync(cancellationToken);
                    return BattleFlowPhase.Starting;
                case BattleFlowPhase.Starting:
                    await ShowObjectiveAsync(cancellationToken);
                    await ShowRoundAsync(cancellationToken);
                    await InitializeHUDAsync(cancellationToken);
                    return BattleFlowPhase.Playing;
                case BattleFlowPhase.Playing:
                    ShowBattleHUD();
                    return null;
                case BattleFlowPhase.TurnTransition:
                    _runtime.InteractionController.ClearSelection();
                    await ShowRoundAsync(cancellationToken);
                    return BattleFlowPhase.Playing;
                case BattleFlowPhase.Result:
                    _runtime.InteractionController.ClearSelection();
                    await ShowResultAsync(cancellationToken);
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }

        private void UpdateBattleFlow()
        {
            if (CurrentPhase != BattleFlowPhase.Playing)
            {
                return;
            }
            if (TryLeavePlaying())
            {
                return;
            }
            UpdateCameraInput();
            _runtime.Tick(CanAcceptPointerInput());
            if (!TryLeavePlaying())
            {
                _hudPresenter.RefreshIfChanged();
            }
        }

        private void UpdateCameraInput()
        {
            if (!CanAcceptCameraInput()) return;
            bool left = Input.GetKeyDown(KeyCode.LeftArrow);
            bool right = Input.GetKeyDown(KeyCode.RightArrow);
            if (left != right) OnCameraRotationRequested(left ? -1 : 1);
            var movement = new Vector2(
                (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));
            if (movement != Vector2.zero) _runtime.MoveCamera(movement, Time.unscaledDeltaTime);
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f) OnCameraZoomRequested(scroll);
        }

        private bool CanAcceptPointerInput()
        {
            return CanAcceptBattleInput() && _hud.Visible && HasInputFocus();
        }

        private static bool HasInputFocus()
        {
            if (!Application.isFocused) return false;
            var selected = EventSystem.current?.currentSelectedGameObject;
            return selected == null || (selected.GetComponent<InputField>() == null &&
                selected.GetComponent<TMP_InputField>() == null);
        }

        private bool CanAcceptCameraInput()
        {
            // 菜单只阻止战斗操作和拖拽；设置仍阻止镜头新输入，不依赖底层HUD是否被遮挡。
            return IsPlayingInputAvailable() && !GameModule.UI.HasWindow<SettingsUI>() && HasInputFocus();
        }

        private void OnCameraRotationRequested(int direction)
        {
            if (CanAcceptCameraInput()) _runtime.RotateCamera(direction);
        }

        private void OnCameraZoomRequested(float scrollDelta)
        {
            if (!CanAcceptCameraInput()) return;
            var passthrough = GameModule.UI.HasWindow<BattleMenuUI>() && _battleMenu != null
                ? _battleMenu.CameraScrollPassthrough : null;
            _runtime.ZoomCamera(scrollDelta, passthrough);
        }

        private bool TryLeavePlaying()
        {
            // 胜负优先于阵营交接，致胜一击不能再播放新阵营提示。
            if (_runtime.Session.IsBattleEnded)
            {
                ChangeBattlePhase(BattleFlowPhase.Result);
                return true;
            }
            if (NeedsRoundAnnouncement())
            {
                ChangeBattlePhase(BattleFlowPhase.TurnTransition);
                return true;
            }
            return false;
        }

        private bool NeedsRoundAnnouncement()
        {
            var session = _runtime.Session;
            return session.CurrentRoundNumber != _announcedRound || session.CurrentFaction != _announcedFaction;
        }

        private bool CanAcceptBattleInput()
        {
            return IsPlayingInputAvailable() && !IsBattleModalOpen();
        }

        private bool IsPlayingInputAvailable()
        {
            // Session 变更后立即拒绝输入，不必等待下一帧才切阶段。
            return CurrentPhase == BattleFlowPhase.Playing &&
                !_runtime.Session.IsBattleEnded && !NeedsRoundAnnouncement();
        }

        private static bool IsBattleModalOpen()
        {
            // TE先入栈再异步加载，因此窗口尚未显示时也已阻止战斗输入。
            return GameModule.UI.HasWindow<BattleMenuUI>() || GameModule.UI.HasWindow<SettingsUI>();
        }

        private void StopBattleFlow()
        {
            var previous = CurrentPhase;
            CurrentPhase = null;
            ExitBattlePhase(previous);
        }

        private void ExitBattlePhase(BattleFlowPhase? phase)
        {
            if (phase == BattleFlowPhase.Playing)
            {
                _runtime.CancelPointerGesture();
                _hud.SetDisplayEnabled(false);
            }
        }
    }
}
