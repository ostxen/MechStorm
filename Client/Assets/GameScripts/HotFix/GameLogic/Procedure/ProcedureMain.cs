using System;
using TEngine;

namespace GameLogic
{
    /// <summary>
    /// 主场景阶段：沿用当前 main 场景，管理主界面和业务入口。
    /// </summary>
    public sealed class ProcedureMain : ProcedureBase, IDisposable
    {
        private readonly GameEventMgr _events = new GameEventMgr();
        private bool _active;
        private bool _enterBattle;

        protected override void OnEnter(IFsm<IProcedureModule> fsm)
        {
            _active = true;
            _enterBattle = false;
            _events.AddEvent(IGameProcedure_Event.RequestBattle, OnBattleRequested);
            GameModule.UI.ShowUIAsync<MainUI>();
        }

        protected override void OnUpdate(IFsm<IProcedureModule> fsm, float elapseSeconds, float realElapseSeconds)
        {
            if (_active && _enterBattle)
            {
                ChangeState<ProcedureBattle>(fsm);
            }
        }

        private void OnBattleRequested()
        {
            if (_active && !_enterBattle)
            {
                _enterBattle = true;
                Log.Info("正在进入 Battle 场景...");
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
            _enterBattle = false;
            _events.Clear();
            GameModule.UI.CloseUI<MainUI>();
        }
    }
}
