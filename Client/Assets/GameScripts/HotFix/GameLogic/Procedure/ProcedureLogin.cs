using System;
using TEngine;

namespace GameLogic
{
    /// <summary>
    /// 本地登录阶段：显示开始界面，暂不进行账号认证。
    /// </summary>
    public sealed class ProcedureLogin : ProcedureBase, IDisposable
    {
        private readonly GameEventMgr _events = new GameEventMgr();
        private bool _active;
        private bool _enterMain;

        protected override void OnEnter(IFsm<IProcedureModule> fsm)
        {
            _active = true;
            _enterMain = false;
            _events.AddEvent(IGameProcedure_Event.RequestStartGame, OnStartRequested);
            GameModule.UI.ShowUIAsync<StartUI>();
        }

        protected override void OnUpdate(IFsm<IProcedureModule> fsm, float elapseSeconds, float realElapseSeconds)
        {
            if (_active && _enterMain)
            {
                ChangeState<ProcedureMain>(fsm);
            }
        }

        private void OnStartRequested()
        {
            if (!_active || _enterMain)
            {
                return;
            }

            _enterMain = true;
            Log.Info("点击开始游戏，正在进入主界面（本地模式，无账号认证）。");
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
            _enterMain = false;
            _events.Clear();
            GameModule.UI.CloseUI<StartUI>();
        }
    }
}
