using System.Collections.Generic;
using System.Reflection;
using GameLogic;
#if ENABLE_OBFUZ
using Obfuz;
#endif
using TEngine;
#pragma warning disable CS0436


/// <summary>
/// 游戏App。
/// </summary>
#if ENABLE_OBFUZ
[ObfuzIgnore(ObfuzScope.TypeName | ObfuzScope.MethodName)]
#endif
public partial class GameApp
{
    private static List<Assembly> _hotfixAssembly;

    /// <summary>
    /// 热更域App主入口。
    /// </summary>
    /// <param name="objects"></param>
    public static void Entrance(object[] objects)
    {
        GameEventHelper.Init();
        _hotfixAssembly = (List<Assembly>)objects[0];
        Log.Warning("======= 看到此条日志代表你成功运行了热更新代码 =======");
        Log.Warning("======= Entrance GameApp =======");
        Utility.Unity.AddDestroyListener(Release);
        Log.Warning("======= StartGameLogic =======");
        StartGameLogic();
    }
    
    private static void StartGameLogic()
    {
        if (!GameModule.Procedure.RestartProcedure(new ProcedureLogin(), new ProcedureMain(), new ProcedureBattle()))
        {
            throw new GameFrameworkException("主包流程未能交接到游戏业务流程。");
        }
    }
    
    private static void Release()
    {
        // 停止业务事件和窗口，再释放 UI 等单例；流程销毁时可重复清理。
        if (GameModule.Fsm.HasFsm<IProcedureModule>() && GameModule.Procedure.CurrentProcedure is System.IDisposable procedure)
        {
            procedure.Dispose();
        }

        SingletonSystem.Release();
        Log.Warning("======= Release GameApp =======");
    }
}