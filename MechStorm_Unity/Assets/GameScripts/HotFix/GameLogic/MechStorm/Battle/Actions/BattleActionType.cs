namespace MechStorm.Battle.Actions
{
    /// <summary>
    /// 战斗操作类型。
    /// </summary>
    public enum BattleActionType
    {
        /// <summary>
        /// 在当前阵营内切换到另一台尚未行动的机兵。
        /// </summary>
        SelectUnit,

        Move,
        Attack,
        EndUnitAction,
        EndFactionTurn,
        UseAbility,
    }
}