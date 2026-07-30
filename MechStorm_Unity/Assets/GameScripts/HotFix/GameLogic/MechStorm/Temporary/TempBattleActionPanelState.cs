using System;
using MechStorm.Presentation.Controllers;

namespace MechStorm.Presentation
{
    /// <summary>
    /// HUD 可直接渲染的行动项，保留 Task 4.9 行动契约作为唯一选择身份。
    /// </summary>
    public sealed class TempBattleActionOption
    {
        public TempBattleActionOption(BattleActionSelection selection, string title, string detail,
            int actionPointCost, bool isAvailable, bool isSelected)
        {
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
            Title = string.IsNullOrWhiteSpace(title)
                ? throw new ArgumentException("Action title must not be empty.", nameof(title))
                : title;
            Detail = detail ?? string.Empty;
            ActionPointCost = actionPointCost >= 0
                ? actionPointCost
                : throw new ArgumentOutOfRangeException(
                    nameof(actionPointCost),
                    actionPointCost,
                    "Action point cost must be non-negative.");
            IsAvailable = isAvailable;
            IsSelected = isSelected;
        }

        public BattleActionSelection Selection { get; }

        public string Title { get; }

        public string Detail { get; }

        public int ActionPointCost { get; }

        public bool IsAvailable { get; }

        public bool IsSelected { get; }
    }

    /// <summary>
    /// 将 Task 4.9 四状态映射为 HUD 区域显隐，不保存独立的交互状态。
    /// </summary>
    public sealed class TempBattleActionPanelState
    {
        private TempBattleActionPanelState(bool showActionCatalog, bool showSelectedAction,
            bool showTargetActions, bool showBasicWeapon, bool showSelectionControls,
            bool showConfirm, bool canConfirm, string prompt)
        {
            ShowActionCatalog = showActionCatalog;
            ShowSelectedAction = showSelectedAction;
            ShowTargetActions = showTargetActions;
            ShowBasicWeapon = showBasicWeapon;
            ShowSelectionControls = showSelectionControls;
            ShowConfirm = showConfirm;
            CanConfirm = canConfirm;
            Prompt = prompt;
        }

        public bool ShowActionCatalog { get; }

        public bool ShowSelectedAction { get; }

        public bool ShowTargetActions { get; }

        public bool ShowBasicWeapon { get; }

        public bool ShowSelectionControls { get; }

        public bool ShowConfirm { get; }

        public bool CanConfirm { get; }

        public string Prompt { get; }

        public static TempBattleActionPanelState Create(bool hasSelectedUnit, bool isBattleEnded,
            BattleActionSelectionState selectionState, BattleActionSelectionPath selectionPath,
            bool selectedActionRequiresBasicWeapon, bool canConfirm, string selectedActionName,
            string selectedTargetName)
        {
            if (!hasSelectedUnit || isBattleEnded)
            {
                return new TempBattleActionPanelState(
                    false, false, false, false, false, false, false, string.Empty);
            }

            switch (selectionState)
            {
                case BattleActionSelectionState.ActionList:
                    return new TempBattleActionPanelState(
                        true, false, false, true, false, false, false,
                        "选择基础武器或技能，或先点击敌方机兵。");
                case BattleActionSelectionState.ActionSelected:
                    return new TempBattleActionPanelState(
                        false, true, false, selectedActionRequiresBasicWeapon,
                        true, false, false,
                        $"已选择 {selectedActionName ?? "行动"}，请选择范围内目标。");
                case BattleActionSelectionState.TargetSelected:
                    return new TempBattleActionPanelState(
                        false, false, true, false, false, false, false,
                        $"已选择 {selectedTargetName ?? "目标"}，请选择可用行动。");
                case BattleActionSelectionState.AwaitingConfirmation:
                    var targetFirst =
                        selectionPath == BattleActionSelectionPath.TargetFirst;
                    return new TempBattleActionPanelState(
                        false, !targetFirst, targetFirst,
                        !targetFirst && selectedActionRequiresBasicWeapon,
                        true, true, canConfirm,
                        $"{selectedActionName ?? "行动"} → {selectedTargetName ?? "目标"}");
                default:
                    throw new ArgumentOutOfRangeException(nameof(selectionState));
            }
        }
    }
}
