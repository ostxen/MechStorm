namespace MechStorm.Presentation.Board
{
    /// <summary>
    /// 棋盘格的表现高亮类型。
    /// </summary>
    public enum BattleCellHighlightType
    {
        /// <summary>可移动范围。</summary>
        Move,

        /// <summary>普通攻击覆盖范围。</summary>
        Attack,

        /// <summary>移动与攻击范围重叠。</summary>
        MoveAndAttack,

        /// <summary>合法攻击目标所在格。</summary>
        ValidTarget,

        /// <summary>已选择、等待确认普通攻击的目标所在格。</summary>
        SelectedAttackTarget,

        /// <summary>主动技能覆盖范围。</summary>
        Ability,

        /// <summary>合法技能目标所在格。</summary>
        ValidAbilityTarget,

        /// <summary>已选择、等待确认释放的技能目标所在格。</summary>
        SelectedAbilityTarget
    }
}