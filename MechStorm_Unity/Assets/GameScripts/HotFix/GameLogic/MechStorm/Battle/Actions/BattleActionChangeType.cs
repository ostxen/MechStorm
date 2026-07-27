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

        /// <summary>
        /// TeamA 或 TeamB 的最后一名存活机兵死亡。
        /// </summary>
        FactionEliminated,

        /// <summary>
        /// 本次操作产生最终胜负，后续战斗操作应被拒绝。
        /// </summary>
        BattleEnded,
    }
}