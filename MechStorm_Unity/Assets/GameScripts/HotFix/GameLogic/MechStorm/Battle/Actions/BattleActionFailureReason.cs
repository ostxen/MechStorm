namespace MechStorm.Battle.Actions
{
    /// <summary>
    /// 正常玩法操作被 Battle 权威层拒绝时返回的原因。
    /// </summary>
    public enum BattleActionFailureReason
    {
        None,
        InvalidMoveTarget,
        TargetOutOfRange,
        SameFactionTarget,
        TargetAlreadyDead,
        ActorCannotAct,
        InsufficientActionPoint,

        /// <summary>
        /// 对局已经产生最终胜负，不能再改变战场或推进回合。
        /// </summary>
        BattleAlreadyEnded,
    }
}