namespace MechStorm.Battle.Actions
{
    /// <summary>
    /// 战斗操作造成的变化类型。
    /// </summary>
    public enum BattleActionChangeType
    {
        UnitMoved,
        DamageApplied,
        UnitDied,
        UnitActionEnded,
        RoundStarted,
        FactionTurnStarted,
        FactionTurnEnded,
        CurrentUnitChanged,
        ActionPointSpent,
        ActionPointRestored,
        FactionEliminated,
        BattleEnded,
    }
}