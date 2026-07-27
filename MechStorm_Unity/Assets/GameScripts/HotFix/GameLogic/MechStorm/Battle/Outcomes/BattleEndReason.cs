namespace MechStorm.Battle.Outcomes
{
    /// <summary>
    /// 最小对局结束原因。
    /// </summary>
    public enum BattleEndReason
    {
        /// <summary>
        /// 对局仍在进行，没有结束原因。
        /// </summary>
        None,

        /// <summary>
        /// TeamA 或 TeamB 的全部机兵死亡。
        /// </summary>
        FactionEliminated,
    }
}
