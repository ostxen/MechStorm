namespace MechStorm.Battle.Actions
{
    public enum BattleActionFailureReason
    {
        None,
        InvalidMoveTarget,
        TargetOutOfRange,
        SameFactionTarget,
        TargetAlreadyDead,
        ActorCannotAct,
        InsufficientActionPoint,
    }
}