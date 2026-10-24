using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TEngine;

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
            RunBattlePhaseAsync(_lifetime, next, _lifetime.Token).Forget();
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

        private async UniTask RunBattlePhaseAsync(CancellationTokenSource lifetime, BattleFlowPhase phase,
            CancellationToken cancellationToken)
        {
            // 阶段工作完成后才推进，离场统一使用本场 token 取消。
            try
            {
                var next = await EnterBattlePhaseAsync(phase, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (next.HasValue)
                {
                    ChangeBattlePhase(next.Value);
                }
            }
            catch
            {
                HandleBattleFailure(lifetime);
                throw;
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
            _runtime.Tick(CanAcceptBattleInput() && _hud.Visible);
            if (!TryLeavePlaying())
            {
                _hudPresenter.RefreshIfChanged();
            }
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
            // Session 变更后立即拒绝输入，不必等待下一帧才切阶段。
            return CurrentPhase == BattleFlowPhase.Playing &&
                !_runtime.Session.IsBattleEnded && !NeedsRoundAnnouncement();
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
                _hud.SetDisplayEnabled(false);
            }
        }
    }
}
