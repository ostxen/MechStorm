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

    public enum BattleActionSelectionPath
    {
        None,
        ActionFirst,
        TargetFirst,
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

        public BattleActionSelectionPath Path { get; private set; }

        public bool IsInputLocked => State != BattleActionSelectionState.ActionList;

        public bool CanConfirm => State == BattleActionSelectionState.AwaitingConfirmation;

        public void SelectAction(BattleActionSelection action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (Path == BattleActionSelectionPath.None)
            {
                Path = SelectedTarget == null
                    ? BattleActionSelectionPath.ActionFirst
                    : BattleActionSelectionPath.TargetFirst;
            }

            SelectedAction = action;
        }

        public void SelectTarget(BattleActionTargetSelection target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (Path == BattleActionSelectionPath.None)
            {
                Path = SelectedAction == null
                    ? BattleActionSelectionPath.TargetFirst
                    : BattleActionSelectionPath.ActionFirst;
            }

            SelectedTarget = target;
        }

        public void ClearAction()
        {
            SelectedAction = null;
            Path = SelectedTarget == null
                ? BattleActionSelectionPath.None
                : BattleActionSelectionPath.TargetFirst;
        }

        public void ClearTarget()
        {
            SelectedTarget = null;
            Path = SelectedAction == null
                ? BattleActionSelectionPath.None
                : BattleActionSelectionPath.ActionFirst;
        }

        public void Cancel()
        {
            Reset();
        }

        public void Reset()
        {
            SelectedAction = null;
            SelectedTarget = null;
            Path = BattleActionSelectionPath.None;
        }
    }
}
