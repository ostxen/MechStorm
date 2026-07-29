using System;

namespace MechStorm.Presentation.Controllers
{
    /// <summary>
    /// 行动与目标的四种有效组合，同时覆盖行动优先和目标优先。
    /// </summary>
    public enum BattleActionSelectionState
    {
        ActionList,
        ActionSelected,
        TargetSelected,
        AwaitingConfirmation,
    }

    /// <summary>
    /// 只保存已选行动和目标，状态由两者组合推导，避免平行布尔值失配。
    /// </summary>
    public sealed class BattleActionSelectionStateMachine
    {
        public BattleActionSelectionState State
        {
            get
            {
                if (SelectedAction != null && SelectedTarget != null)
                {
                    return BattleActionSelectionState.AwaitingConfirmation;
                }

                if (SelectedAction != null)
                {
                    return BattleActionSelectionState.ActionSelected;
                }

                return SelectedTarget != null
                    ? BattleActionSelectionState.TargetSelected
                    : BattleActionSelectionState.ActionList;
            }
        }

        public BattleActionSelection SelectedAction { get; private set; }

        public BattleActionTargetSelection SelectedTarget { get; private set; }

        public bool IsInputLocked => State != BattleActionSelectionState.ActionList;

        public bool CanConfirm => State == BattleActionSelectionState.AwaitingConfirmation;

        public void SelectAction(BattleActionSelection action)
        {
            SelectedAction = action ?? throw new ArgumentNullException(nameof(action));
        }

        public void SelectTarget(BattleActionTargetSelection target)
        {
            SelectedTarget = target ?? throw new ArgumentNullException(nameof(target));
        }

        public void ClearAction()
        {
            SelectedAction = null;
        }

        public void ClearTarget()
        {
            SelectedTarget = null;
        }

        public void Cancel()
        {
            Reset();
        }

        public void Reset()
        {
            SelectedAction = null;
            SelectedTarget = null;
        }
    }
}
