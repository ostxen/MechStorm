using TEngine;

namespace GameLogic
{
    [EventInterface(EEventGroup.GroupUI)]
    public interface IGameProcedure
    {
        void RequestStartGame();
        void RequestBattle();
        void StartGameFailed();
    }
}
