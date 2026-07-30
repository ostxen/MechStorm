using System;
using System.Collections.Generic;
using MechStorm.Presentation.Controllers;
using UnityEngine;
using UnityEngine.UIElements;

namespace MechStorm.Presentation
{
    /// <summary>
    /// 运行时临时战斗 HUD，负责显示数据和转发按钮点击，不判断战斗规则。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TempBattleDebugPanel : MonoBehaviour
    {
        private const int UnitPanelWidth = 390;
        private const int ActionPanelWidth = 460;
        private const int WeaponPanelWidth = 330;
        private const string RuntimeThemeResourcePath = "MechStorm/TempBattleRuntimeTheme";

        private UIDocument _document;
        private PanelSettings _ownedPanelSettings;
        [SerializeField]
        private TempBattleHudTheme _theme;
        private VisualElement _panel;
        private VisualElement _durabilityFill;
        private VisualElement _actionPointSegmentsRoot;
        private VisualElement _diagnosticsDrawer;
        private VisualElement _unitCard;
        private VisualElement _weaponDock;
        private VisualElement _actionDock;
        private VisualElement _turnControlDock;
        private VisualElement _actionCatalogRoot;
        private VisualElement _basicWeaponRoot;
        private VisualElement _selectedWeaponRoot;
        private VisualElement _skillListRoot;
        private VisualElement _selectedActionRoot;
        private VisualElement _targetActionRoot;
        private VisualElement _targetActionListRoot;
        private VisualElement _selectionControlsRoot;
        private VisualElement _actionTooltip;
        private Label _roundLabel;
        private Label _factionLabel;
        private Label _unitLabel;
        private Label _durabilityLabel;
        private Label _actionPointLabel;
        private Label _tooltipTitleLabel;
        private Label _tooltipDetailLabel;
        private Label _confirmCaptionLabel;
        private Button _endFactionTurnButton;
        private Button _waitButton;
        private Button _confirmButton;
        private Button _cancelSelectionButton;
        private Button _damageButton;
        private Button _logButton;
        private Button _exportButton;
        private Button _diagnosticsButton;
        private readonly List<VisualElement> _actionPointSegments = new List<VisualElement>();
        private TempBattleDebugActions _actions;
        private IVisualElementScheduledItem _refreshSchedule;
        private Font _runtimeFont;
        private bool _ownsRuntimeFont;
        private bool _diagnosticsVisible;
        private string _actionContentSignature;

        private Color PanelBackground =>
            _theme != null ? _theme.PanelBackground : new Color(0.045f, 0.07f, 0.09f, 0.92f);
        private Color PanelBorder =>
            _theme != null ? _theme.PanelBorder : new Color(0.35f, 0.43f, 0.47f, 0.95f);
        private Color PrimaryText => _theme != null ? _theme.PrimaryText : Color.white;
        private Color SecondaryText =>
            _theme != null ? _theme.SecondaryText : new Color(0.72f, 0.78f, 0.82f);
        private Color AccentText =>
            _theme != null ? _theme.AccentText : new Color(0.55f, 0.72f, 0.85f);
        private Color TargetText =>
            _theme != null ? _theme.TargetText : new Color(0.95f, 0.8f, 0.34f);
        private Color AbilityButton =>
            _theme != null ? _theme.AbilityButton : new Color(0.17f, 0.42f, 0.34f);
        private Color AttackButton =>
            _theme != null ? _theme.AttackButton : new Color(0.43f, 0.22f, 0.2f);
        private Color EndActionButton =>
            _theme != null ? _theme.EndActionButton : new Color(0.74f, 0.49f, 0.12f);
        private Color EndTurnButton =>
            _theme != null ? _theme.EndTurnButton : new Color(0.38f, 0.31f, 0.17f);
        private Color NeutralButton =>
            _theme != null ? _theme.NeutralButton : new Color(0.15f, 0.21f, 0.27f);
        private Color ButtonText => _theme != null ? _theme.ButtonText : Color.white;
        private Color DurabilityTrack =>
            _theme != null ? _theme.DurabilityTrack : new Color(0.12f, 0.16f, 0.19f);
        private Color DurabilityFill =>
            _theme != null ? _theme.DurabilityFill : new Color(0.3f, 0.78f, 0.5f);
        private Color ActionPointFilled =>
            _theme != null ? _theme.ActionPointFilled : new Color(0.88f, 0.72f, 0.27f);
        private Color ActionPointEmpty =>
            _theme != null ? _theme.ActionPointEmpty : new Color(0.22f, 0.27f, 0.3f);

        private void Awake()
        {
            _runtimeFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei" },
                16);
            _ownsRuntimeFont = _runtimeFont != null;
            if (_runtimeFont == null)
            {
                _runtimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            EnsureDocument();
        }

        private void OnEnable()
        {
            if (_panel == null)
            {
                BuildVisualTree();
            }

            _refreshSchedule = _panel?.schedule.Execute(Refresh).Every(100);
        }

        private void OnDisable()
        {
            _refreshSchedule?.Pause();
            _refreshSchedule = null;
        }

        private void OnDestroy()
        {
            UnregisterCallbacks();
            if (_ownedPanelSettings != null)
            {
                Destroy(_ownedPanelSettings);
            }

            if (_ownsRuntimeFont && _runtimeFont != null)
            {
                Destroy(_runtimeFont);
            }
        }

        public void Bind(TempBattleDebugActions actions)
        {
            _actions = actions;
            _actionContentSignature = null;
            Refresh();
        }

        public void SetTheme(TempBattleHudTheme theme)
        {
            if (theme == null || ReferenceEquals(_theme, theme))
            {
                return;
            }

            _theme = theme;
            if (_panel == null)
            {
                return;
            }

            _refreshSchedule?.Pause();
            UnregisterCallbacks();
            _document.rootVisualElement.Clear();
            _panel = null;
            _actionPointSegments.Clear();
            _actionContentSignature = null;
            BuildVisualTree();
            Refresh();
            _refreshSchedule = _panel.schedule.Execute(Refresh).Every(100);
        }

        public bool IsPointerOverPanel()
        {
            if (_panel?.panel == null)
            {
                return false;
            }

            var panelPosition = RuntimePanelUtils.ScreenToPanel(
                _panel.panel,
                UnityEngine.Input.mousePosition);
            var pickedElement = _panel.panel.Pick(panelPosition);
            while (pickedElement != null)
            {
                if (ReferenceEquals(pickedElement, _panel))
                {
                    return true;
                }

                pickedElement = pickedElement.parent;
            }

            return false;
        }

        private void EnsureDocument()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null)
            {
                _document = gameObject.AddComponent<UIDocument>();
            }

            if (_document.panelSettings != null)
            {
                return;
            }

            _ownedPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _ownedPanelSettings.name = "TempBattleDebugPanelSettings";
            _ownedPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _ownedPanelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _ownedPanelSettings.sortingOrder = 100;
            _ownedPanelSettings.themeStyleSheet =
                Resources.Load<ThemeStyleSheet>(RuntimeThemeResourcePath);
            if (_ownedPanelSettings.themeStyleSheet == null)
            {
                Debug.LogError(
                    $"[MechStorm] Runtime UI theme not found at " +
                    $"Resources/{RuntimeThemeResourcePath}.tss.");
            }

            _document.panelSettings = _ownedPanelSettings;
        }

        private void BuildVisualTree()
        {
            var documentRoot = _document.rootVisualElement;
            documentRoot.style.width = new Length(100, LengthUnit.Percent);
            documentRoot.style.height = new Length(100, LengthUnit.Percent);
            _panel = new VisualElement
            {
                name = "TempBattleHud",
                pickingMode = PickingMode.Ignore,
            };
            _panel.style.position = Position.Absolute;
            _panel.style.left = 0;
            _panel.style.top = 0;
            _panel.style.width = new Length(100, LengthUnit.Percent);
            _panel.style.height = new Length(100, LengthUnit.Percent);

            var turnBanner = CreateHudCard(320);
            turnBanner.style.position = Position.Absolute;
            turnBanner.style.top = 18;
            turnBanner.style.left = new Length(50, LengthUnit.Percent);
            turnBanner.style.marginLeft = -160;
            turnBanner.style.flexDirection = FlexDirection.Row;
            turnBanner.style.justifyContent = Justify.Center;
            _roundLabel = CreateLabel("第 -- 回合", 23, PrimaryText, FontStyle.Bold);
            _factionLabel = CreateLabel("阵营 --", 20, SecondaryText);
            _factionLabel.style.marginLeft = 18;
            turnBanner.Add(_roundLabel);
            turnBanner.Add(_factionLabel);
            _panel.Add(turnBanner);

            _unitCard = CreateHudCard(UnitPanelWidth);
            _unitCard.style.position = Position.Absolute;
            _unitCard.style.left = 22;
            _unitCard.style.top = 22;
            _unitCard.style.display = DisplayStyle.None;
            var unitEyebrow = CreateLabel("当前行动机兵", 18, AccentText,
                FontStyle.Bold);
            _unitLabel = CreateLabel("等待机兵...", 30, PrimaryText, FontStyle.Bold);
            _unitLabel.style.marginTop = 2;
            _unitLabel.style.marginBottom = 10;
            _unitCard.Add(unitEyebrow);
            _unitCard.Add(_unitLabel);

            var durabilityHeader = CreateInfoHeader("耐久", out _durabilityLabel);
            _unitCard.Add(durabilityHeader);
            var durabilityTrack = new VisualElement();
            durabilityTrack.style.height = 10;
            durabilityTrack.style.marginTop = 4;
            durabilityTrack.style.marginBottom = 10;
            durabilityTrack.style.backgroundColor = DurabilityTrack;
            _durabilityFill = new VisualElement();
            _durabilityFill.style.height = 10;
            _durabilityFill.style.width = new Length(0, LengthUnit.Percent);
            _durabilityFill.style.backgroundColor = DurabilityFill;
            durabilityTrack.Add(_durabilityFill);
            _unitCard.Add(durabilityTrack);

            var actionPointHeader = CreateInfoHeader("行动点", out _actionPointLabel);
            _unitCard.Add(actionPointHeader);
            _actionPointSegmentsRoot = new VisualElement();
            _actionPointSegmentsRoot.style.flexDirection = FlexDirection.Row;
            _actionPointSegmentsRoot.style.marginTop = 4;
            _unitCard.Add(_actionPointSegmentsRoot);
            _panel.Add(_unitCard);

            _weaponDock = new VisualElement();
            _weaponDock.style.width = WeaponPanelWidth;
            _weaponDock.style.position = Position.Absolute;
            _weaponDock.style.right = 22;
            _weaponDock.style.top = 92;
            _weaponDock.style.display = DisplayStyle.None;
            _basicWeaponRoot = new VisualElement();
            _weaponDock.Add(_basicWeaponRoot);
            _selectedWeaponRoot = new VisualElement();
            _selectedWeaponRoot.style.display = DisplayStyle.None;
            _weaponDock.Add(_selectedWeaponRoot);
            _targetActionRoot = new VisualElement();
            _targetActionRoot.style.display = DisplayStyle.None;
            _targetActionListRoot = new VisualElement();
            _targetActionRoot.Add(_targetActionListRoot);
            _weaponDock.Add(_targetActionRoot);
            _panel.Add(_weaponDock);

            _actionDock = new VisualElement();
            _actionDock.style.width = ActionPanelWidth;
            _actionDock.style.position = Position.Absolute;
            _actionDock.style.right = 22;
            _actionDock.style.bottom = 22;
            _actionDock.style.display = DisplayStyle.None;

            _actionCatalogRoot = new VisualElement();
            var commandRow = new VisualElement();
            commandRow.style.flexDirection = FlexDirection.Row;
            commandRow.style.alignItems = Align.FlexEnd;
            _skillListRoot = new VisualElement();
            _skillListRoot.style.flexDirection = FlexDirection.Row;
            _skillListRoot.style.flexGrow = 1;
            commandRow.Add(_skillListRoot);
            _waitButton = CreateLargeCommandButton(
                "待机",
                "▼",
                EndActionButton,
                220,
                out _);
            _waitButton.style.width = 220;
            _waitButton.style.marginLeft = 10;
            _waitButton.style.marginBottom = 0;
            commandRow.Add(_waitButton);
            _actionCatalogRoot.Add(commandRow);
            _actionDock.Add(_actionCatalogRoot);

            _selectedActionRoot = new VisualElement();
            _selectedActionRoot.style.display = DisplayStyle.None;
            _actionDock.Add(_selectedActionRoot);

            _selectionControlsRoot = new VisualElement();
            _selectionControlsRoot.style.display = DisplayStyle.None;
            var selectionButtonRow = new VisualElement();
            selectionButtonRow.style.flexDirection = FlexDirection.Row;
            _cancelSelectionButton = CreateButton("取消", NeutralButton, 70);
            _cancelSelectionButton.style.width = 132;
            _confirmButton = CreateLargeCommandButton(
                "确认",
                "✓",
                AttackButton,
                140,
                out _confirmCaptionLabel);
            _confirmButton.style.width = 140;
            _confirmButton.style.marginLeft = 10;
            selectionButtonRow.Add(_cancelSelectionButton);
            selectionButtonRow.Add(_confirmButton);
            _selectionControlsRoot.Add(selectionButtonRow);
            _actionDock.Add(_selectionControlsRoot);
            _panel.Add(_actionDock);

            _actionTooltip = CreateHudCard(360);
            _actionTooltip.style.position = Position.Absolute;
            _actionTooltip.style.right = 154;
            _actionTooltip.style.bottom = 250;
            _actionTooltip.style.display = DisplayStyle.None;
            _tooltipTitleLabel = CreateLabel("行动说明", 20, PrimaryText, FontStyle.Bold);
            _tooltipDetailLabel = CreateLabel(string.Empty, 18, SecondaryText);
            _tooltipDetailLabel.style.marginTop = 6;
            _tooltipDetailLabel.style.whiteSpace = WhiteSpace.Normal;
            _actionTooltip.Add(_tooltipTitleLabel);
            _actionTooltip.Add(_tooltipDetailLabel);
            _panel.Add(_actionTooltip);

            _turnControlDock = CreateHudCard(UnitPanelWidth);
            _turnControlDock.style.position = Position.Absolute;
            _turnControlDock.style.left = 22;
            _turnControlDock.style.bottom = 22;
            _turnControlDock.style.display = DisplayStyle.None;
            var turnControlTitle = CreateLabel("回合控制", 18, AccentText, FontStyle.Bold);
            var turnControlHint = CreateLabel("全局控制，不结束单台机兵行动", 19, SecondaryText);
            turnControlHint.style.marginTop = 5;
            turnControlHint.style.marginBottom = 14;
            _endFactionTurnButton = CreateButton("结束当前阵营回合", EndTurnButton, 64);
            _turnControlDock.Add(turnControlTitle);
            _turnControlDock.Add(turnControlHint);
            _turnControlDock.Add(_endFactionTurnButton);
            _panel.Add(_turnControlDock);

            _diagnosticsButton = CreateButton("调试", NeutralButton, 52);
            _diagnosticsButton.style.position = Position.Absolute;
            _diagnosticsButton.style.right = 22;
            _diagnosticsButton.style.top = 22;
            _diagnosticsButton.style.width = 78;
            _panel.Add(_diagnosticsButton);

            _diagnosticsDrawer = CreateHudCard(320);
            _diagnosticsDrawer.style.position = Position.Absolute;
            _diagnosticsDrawer.style.right = ActionPanelWidth + 38;
            _diagnosticsDrawer.style.top = 22;
            _diagnosticsDrawer.style.display = DisplayStyle.None;
            var diagnosticsTitle = CreateLabel("调试工具", 21, PrimaryText, FontStyle.Bold);
            diagnosticsTitle.style.marginBottom = 10;
            _diagnosticsDrawer.Add(diagnosticsTitle);
            _damageButton = CreateButton("对我方机兵造成 10 点伤害",
                NeutralButton, 56);
            _logButton = CreateButton("记录当前战斗状态",
                NeutralButton, 56);
            _exportButton = CreateButton("导出战斗调试 JSON",
                NeutralButton, 56);
            _diagnosticsDrawer.Add(_damageButton);
            _diagnosticsDrawer.Add(_logButton);
            _diagnosticsDrawer.Add(_exportButton);
            _panel.Add(_diagnosticsDrawer);

            RegisterCallbacks();
            documentRoot.Add(_panel);
            _panel.schedule.Execute(LogResolvedLayout).ExecuteLater(100);
        }

        private void LogResolvedLayout()
        {
            Debug.Log(
                $"[MechStorm] Runtime HUD layout: " +
                $"Root={_panel.resolvedStyle.width}x{_panel.resolvedStyle.height}, " +
                $"ActionDock={_actionDock.worldBound}, " +
                $"TurnControlDock={_turnControlDock.worldBound}, " +
                $"Theme={_ownedPanelSettings?.themeStyleSheet?.name ?? "None"}.");
        }

        private void RegisterCallbacks()
        {
            _endFactionTurnButton.clicked += OnEndFactionTurnClicked;
            _waitButton.clicked += OnWaitClicked;
            _confirmButton.clicked += OnConfirmClicked;
            _cancelSelectionButton.clicked += OnCancelSelectionClicked;
            _damageButton.clicked += OnDamageClicked;
            _logButton.clicked += OnLogClicked;
            _exportButton.clicked += OnExportClicked;
            _diagnosticsButton.clicked += OnDiagnosticsClicked;
        }

        private void UnregisterCallbacks()
        {
            if (_endFactionTurnButton == null)
            {
                return;
            }

            _endFactionTurnButton.clicked -= OnEndFactionTurnClicked;
            _waitButton.clicked -= OnWaitClicked;
            _confirmButton.clicked -= OnConfirmClicked;
            _cancelSelectionButton.clicked -= OnCancelSelectionClicked;
            _damageButton.clicked -= OnDamageClicked;
            _logButton.clicked -= OnLogClicked;
            _exportButton.clicked -= OnExportClicked;
            _diagnosticsButton.clicked -= OnDiagnosticsClicked;
        }

        private void Refresh()
        {
            var ready = _actions != null;
            var hasSelectedUnit = ready && _actions.IsCurrentUnitSelected;
            var actionPanelState = ready ? _actions.ActionPanelState : null;
            _unitCard.style.display = hasSelectedUnit ? DisplayStyle.Flex : DisplayStyle.None;
            _turnControlDock.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
            _roundLabel.text = ready ? $"第 {_actions.CurrentRoundNumber} 回合" : "第 -- 回合";
            _factionLabel.text = ready ? _actions.CurrentFactionName : "阵营 --";
            _unitLabel.text = ready ? _actions.CurrentUnitName : "等待机兵...";
            _durabilityLabel.text = ready
                ? $"{_actions.CurrentUnitDurability} / {_actions.CurrentUnitMaxDurability}"
                : "-- / --";
            _actionPointLabel.text = ready
                ? $"{_actions.CurrentUnitActionPoint} / {_actions.CurrentUnitMaxActionPoint}"
                : "-- / --";
            var durabilityRatio = ready && _actions.CurrentUnitMaxDurability > 0
                ? (float)_actions.CurrentUnitDurability / _actions.CurrentUnitMaxDurability
                : 0f;
            _durabilityFill.style.width = new Length(
                Mathf.Clamp01(durabilityRatio) * 100f,
                LengthUnit.Percent);
            RefreshActionPointSegments(
                ready ? _actions.CurrentUnitActionPoint : 0,
                ready ? _actions.CurrentUnitMaxActionPoint : 0);

            var selectedAction = ready ? _actions.GetSelectedAction() : null;
            var showSelectedWeapon = actionPanelState?.ShowSelectedAction == true &&
                selectedAction?.Selection.Kind == BattleActionSelectionKind.BasicAttack;
            var showSelectedSkill = actionPanelState?.ShowSelectedAction == true &&
                selectedAction?.Selection.Kind == BattleActionSelectionKind.ActiveAbility;
            var showActionDock = actionPanelState != null &&
                (actionPanelState.ShowActionCatalog ||
                 showSelectedSkill ||
                 actionPanelState.ShowSelectionControls);
            _weaponDock.style.display =
                actionPanelState?.ShowBasicWeapon == true ||
                actionPanelState?.ShowTargetActions == true ||
                showSelectedWeapon
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _actionDock.style.display = showActionDock
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _actionCatalogRoot.style.display =
                actionPanelState?.ShowActionCatalog == true
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _basicWeaponRoot.style.display =
                actionPanelState?.ShowBasicWeapon == true
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _selectedWeaponRoot.style.display = showSelectedWeapon
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _selectedActionRoot.style.display =
                showSelectedSkill
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _targetActionRoot.style.display =
                actionPanelState?.ShowTargetActions == true
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _selectionControlsRoot.style.display =
                actionPanelState?.ShowSelectionControls == true
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _confirmButton.style.display =
                actionPanelState?.ShowConfirm == true
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            RefreshActionButtons(actionPanelState);
            var canUseUnlockedBattleInput =
                ready && !_actions.IsBattleEnded && !_actions.IsActionInputLocked;
            _waitButton.SetEnabled(canUseUnlockedBattleInput);
            _endFactionTurnButton.SetEnabled(canUseUnlockedBattleInput);
            _damageButton.SetEnabled(canUseUnlockedBattleInput);
            _confirmButton.SetEnabled(actionPanelState?.CanConfirm == true);
            _cancelSelectionButton.SetEnabled(
                actionPanelState?.ShowSelectionControls == true);
            _logButton.SetEnabled(ready);
            _exportButton.SetEnabled(ready);
            if (ready)
            {
                _damageButton.text = $"对我方机兵造成 {_actions.DebugDamage} 点伤害";
                _confirmCaptionLabel.text =
                    _actions.ActionSelectionState ==
                    BattleActionSelectionState.AwaitingConfirmation
                        ? $"确认 · {_actions.SelectedActionName}"
                        : "确认";
            }
        }

        private void RefreshActionButtons(TempBattleActionPanelState panelState)
        {
            if (_actions == null || panelState == null)
            {
                return;
            }

            var signature =
                $"{_actions.CurrentUnitId}:{_actions.CurrentUnitActionPoint}:" +
                $"{_actions.ActionSelectionState}:{_actions.ActionSelectionPath}:" +
                $"{_actions.SelectedActionName}:" +
                $"{_actions.SelectedTargetName}:{_actions.IsBattleEnded}";
            if (string.Equals(_actionContentSignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            _actionContentSignature = signature;
            RebuildActionCatalog(_actions.GetActionCatalog());
            RebuildSelectedAction(_actions.GetSelectedAction());
            if (panelState.ShowTargetActions)
            {
                RebuildTargetActions(_actions.GetActionsForSelectedTarget());
            }
            else
            {
                _targetActionListRoot.Clear();
            }
            HideActionTooltip();
        }

        private void RebuildActionCatalog(IReadOnlyList<TempBattleActionOption> actions)
        {
            _basicWeaponRoot.Clear();
            _skillListRoot.Clear();
            foreach (var action in actions)
            {
                if (action.Selection.Kind == BattleActionSelectionKind.BasicAttack)
                {
                    var weaponButton = CreateActionButton(
                        action,
                        compact: false,
                        renderBasicAsSkill: false,
                        selectsAction: false);
                    ApplySelectedActionBorder(weaponButton);
                    _basicWeaponRoot.Add(weaponButton);
                }
                else
                {
                    _skillListRoot.Add(CreateActionButton(action, true));
                }
            }

            if (_skillListRoot.childCount == 0)
            {
                _skillListRoot.Add(CreateLabel(
                    "当前机兵没有主动技能",
                    18,
                    SecondaryText));
            }
        }

        private void RebuildTargetActions(IReadOnlyList<TempBattleActionOption> actions)
        {
            _targetActionListRoot.Clear();
            foreach (var action in actions)
            {
                _targetActionListRoot.Add(CreateActionButton(
                    action,
                    false,
                    action.Selection.Kind == BattleActionSelectionKind.BasicAttack));
            }
        }

        private void RebuildSelectedAction(TempBattleActionOption action)
        {
            _selectedWeaponRoot.Clear();
            _selectedActionRoot.Clear();

            if (action?.Selection.Kind == BattleActionSelectionKind.BasicAttack)
            {
                _selectedWeaponRoot.Add(CreateActionButton(action, false));
            }
            else if (action != null)
            {
                _selectedActionRoot.Add(CreateActionButton(action, true));
            }
        }

        private Button CreateActionButton(TempBattleActionOption option, bool compact,
            bool renderBasicAsSkill = false, bool selectsAction = true)
        {
            var isBasicAttack =
                option.Selection.Kind == BattleActionSelectionKind.BasicAttack;
            var useSkillLayout = !isBasicAttack || renderBasicAsSkill;
            var background = isBasicAttack
                ? AttackButton
                : AbilityButton;
            var button = CreateButton(
                string.Empty,
                background,
                compact ? 104 : useSkillLayout ? 78 : 108);
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.SpaceBetween;
            button.SetEnabled(option.IsAvailable);

            if (compact)
            {
                button.style.width = 88;
                button.style.flexDirection = FlexDirection.Column;
                button.style.paddingLeft = 5;
                button.style.paddingRight = 5;
                button.style.paddingTop = 5;
                button.style.paddingBottom = 4;
                button.style.marginRight = 8;
                button.style.marginBottom = 0;
                button.Add(CreateActionIcon(option, 64));
                var costLabel = CreateLabel(
                    $"AP {option.ActionPointCost}",
                    15,
                    TargetText,
                    FontStyle.Bold);
                costLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                costLabel.style.marginTop = 3;
                button.Add(costLabel);
            }
            else
            {
                button.style.flexDirection = FlexDirection.Row;
                button.style.paddingLeft = 12;
                button.style.paddingRight = 12;
                if (useSkillLayout)
                {
                    var actionPointBadge = new VisualElement();
                    actionPointBadge.style.width = 44;
                    actionPointBadge.style.height = 54;
                    actionPointBadge.style.justifyContent = Justify.Center;
                    actionPointBadge.style.backgroundColor = NeutralButton;
                    var actionPointLabel = CreateLabel(
                        $"{option.ActionPointCost}\nAP",
                        15,
                        TargetText,
                        FontStyle.Bold);
                    actionPointLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    actionPointBadge.Add(actionPointLabel);
                    button.Add(actionPointBadge);
                }

                var textColumn = new VisualElement();
                textColumn.style.flexGrow = 1;
                textColumn.style.marginLeft = useSkillLayout ? 10 : 2;
                textColumn.style.marginRight = 10;
                textColumn.Add(CreateLabel(
                    option.Title,
                    useSkillLayout ? 18 : 21,
                    PrimaryText,
                    FontStyle.Bold));
                textColumn.Add(CreateLabel(
                    option.Detail,
                    useSkillLayout ? 14 : 19,
                    SecondaryText,
                    useSkillLayout ? FontStyle.Normal : FontStyle.Bold));
                button.Add(textColumn);
                button.Add(CreateActionIcon(option, useSkillLayout ? 54 : 76));
            }

            if (option.IsSelected)
            {
                ApplySelectedActionBorder(button);
            }

            if (!option.IsAvailable)
            {
                button.style.opacity = 0.45f;
            }

            button.RegisterCallback<PointerEnterEvent>(_ => ShowActionTooltip(option));
            button.RegisterCallback<PointerLeaveEvent>(_ => HideActionTooltip());
            button.clicked += () =>
            {
                if (selectsAction)
                {
                    _actions?.SelectAction(option);
                    _actionContentSignature = null;
                }

                HideActionTooltip();
                Refresh();
            };
            return button;
        }

        private void ApplySelectedActionBorder(VisualElement element)
        {
            element.style.borderTopWidth = 3;
            element.style.borderRightWidth = 3;
            element.style.borderBottomWidth = 3;
            element.style.borderLeftWidth = 3;
            element.style.borderTopColor = TargetText;
            element.style.borderRightColor = TargetText;
            element.style.borderBottomColor = TargetText;
            element.style.borderLeftColor = TargetText;
        }

        private VisualElement CreateActionIcon(TempBattleActionOption option, float size)
        {
            var iconFrame = new VisualElement();
            iconFrame.style.width = size;
            iconFrame.style.height = size;
            iconFrame.style.justifyContent = Justify.Center;
            iconFrame.style.alignItems = Align.Center;
            iconFrame.style.backgroundColor =
                option.Selection.Kind == BattleActionSelectionKind.BasicAttack
                    ? new Color(0.62f, 0.25f, 0.19f)
                    : new Color(0.32f, 0.19f, 0.48f);
            iconFrame.style.borderTopWidth = 2;
            iconFrame.style.borderRightWidth = 2;
            iconFrame.style.borderBottomWidth = 2;
            iconFrame.style.borderLeftWidth = 2;
            iconFrame.style.borderTopColor = PrimaryText;
            iconFrame.style.borderRightColor = PrimaryText;
            iconFrame.style.borderBottomColor = PrimaryText;
            iconFrame.style.borderLeftColor = PrimaryText;
            var glyph = option.Selection.Kind == BattleActionSelectionKind.BasicAttack
                ? "武"
                : option.Title.Substring(0, 1);
            var glyphLabel = CreateLabel(glyph, size >= 64 ? 30 : 25, PrimaryText, FontStyle.Bold);
            glyphLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            iconFrame.Add(glyphLabel);
            return iconFrame;
        }

        private void ShowActionTooltip(TempBattleActionOption option)
        {
            _tooltipTitleLabel.text = option.Title;
            _tooltipDetailLabel.text = option.Detail;
            _actionTooltip.style.display = DisplayStyle.Flex;
        }

        private void HideActionTooltip()
        {
            _actionTooltip.style.display = DisplayStyle.None;
        }

        private void OnWaitClicked()
        {
            _actions?.EndCurrentUnitAction();
            _actionContentSignature = null;
            Refresh();
        }

        private void OnEndFactionTurnClicked()
        {
            _actions?.EndCurrentFactionTurn();
            _actionContentSignature = null;
            Refresh();
        }

        private void OnConfirmClicked()
        {
            _actions?.ConfirmSelectedAction();
            _actionContentSignature = null;
            Refresh();
        }

        private void OnCancelSelectionClicked()
        {
            _actions?.CancelActionSelection();
            _actionContentSignature = null;
            Refresh();
        }

        private void OnDamageClicked()
        {
            _actions?.ApplyDebugDamageToPlayerA();
            Refresh();
        }

        private void OnLogClicked()
        {
            _actions?.LogCurrentBattleState();
            Refresh();
        }

        private void OnExportClicked()
        {
            _actions?.ExportBattleDebugJson();
            Refresh();
        }

        private void OnDiagnosticsClicked()
        {
            _diagnosticsVisible = !_diagnosticsVisible;
            _diagnosticsDrawer.style.display = _diagnosticsVisible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void RefreshActionPointSegments(int currentActionPoint, int maxActionPoint)
        {
            if (_actionPointSegments.Count != maxActionPoint)
            {
                _actionPointSegments.Clear();
                _actionPointSegmentsRoot.Clear();
                for (var i = 0; i < maxActionPoint; i++)
                {
                    var segment = new VisualElement();
                    segment.style.flexGrow = 1;
                    segment.style.height = 9;
                    segment.style.marginRight = i < maxActionPoint - 1 ? 3 : 0;
                    _actionPointSegments.Add(segment);
                    _actionPointSegmentsRoot.Add(segment);
                }
            }

            for (var i = 0; i < _actionPointSegments.Count; i++)
            {
                _actionPointSegments[i].style.backgroundColor = i < currentActionPoint
                    ? ActionPointFilled
                    : ActionPointEmpty;
            }
        }

        private VisualElement CreateHudCard(float width)
        {
            var card = new VisualElement();
            card.style.width = width;
            card.style.paddingLeft = 16;
            card.style.paddingRight = 16;
            card.style.paddingTop = 14;
            card.style.paddingBottom = 14;
            card.style.backgroundColor = PanelBackground;
            card.style.borderTopWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderTopColor = PanelBorder;
            card.style.borderRightColor = card.style.borderTopColor;
            card.style.borderBottomColor = card.style.borderTopColor;
            card.style.borderLeftColor = card.style.borderTopColor;
            card.style.borderTopLeftRadius = 3;
            card.style.borderTopRightRadius = 3;
            card.style.borderBottomLeftRadius = 3;
            card.style.borderBottomRightRadius = 3;
            return card;
        }

        private VisualElement CreateInfoHeader(string title, out Label valueLabel)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.Add(CreateLabel(title, 17, SecondaryText, FontStyle.Bold));
            valueLabel = CreateLabel("-- / --", 17, PrimaryText, FontStyle.Bold);
            row.Add(valueLabel);
            return row;
        }

        private Label CreateLabel(string text, int fontSize, Color color, FontStyle fontStyle = FontStyle.Normal)
        {
            var label = new Label(text);
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityFont = _runtimeFont;
            label.style.unityFontStyleAndWeight = fontStyle;
            return label;
        }

        private Button CreateButton(string text, Color backgroundColor, float height)
        {
            var button = new Button
            {
                text = text,
            };
            button.style.height = height;
            button.style.marginBottom = 8;
            button.style.color = ButtonText;
            button.style.backgroundColor = backgroundColor;
            button.style.unityFont = _runtimeFont;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = 20;
            button.style.paddingLeft = 16;
            button.style.paddingRight = 16;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.borderTopLeftRadius = 2;
            button.style.borderTopRightRadius = 2;
            button.style.borderBottomLeftRadius = 2;
            button.style.borderBottomRightRadius = 2;
            return button;
        }

        private Button CreateLargeCommandButton(string caption, string glyph,
            Color backgroundColor, float height, out Label captionLabel)
        {
            var size = Mathf.Max(height, 96f);
            var button = CreateButton(string.Empty, backgroundColor, size);
            button.style.width = size;
            button.style.minWidth = size;
            button.style.maxWidth = size;
            button.style.minHeight = size;
            button.style.maxHeight = size;
            button.style.flexGrow = 0;
            button.style.flexShrink = 0;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.overflow = Overflow.Visible;
            button.style.backgroundColor = Color.clear;
            button.style.borderTopWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;

            var diamond = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
            };
            diamond.style.position = Position.Absolute;
            diamond.style.left = 0;
            diamond.style.top = 0;
            diamond.style.width = new Length(100, LengthUnit.Percent);
            diamond.style.height = new Length(100, LengthUnit.Percent);
            diamond.generateVisualContent += context =>
                DrawDiamond(context, diamond.contentRect, backgroundColor);
            button.Add(diamond);

            var glyphLabel = CreateLabel(glyph, size >= 160f ? 42 : 34, PrimaryText, FontStyle.Bold);
            glyphLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            glyphLabel.style.width = size * 0.72f;
            glyphLabel.style.flexShrink = 0;
            glyphLabel.pickingMode = PickingMode.Ignore;
            captionLabel = CreateLabel(caption, size >= 160f ? 25 : 22, ButtonText, FontStyle.Bold);
            captionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            captionLabel.style.width = size * 0.72f;
            captionLabel.style.flexShrink = 0;
            captionLabel.pickingMode = PickingMode.Ignore;
            button.Add(glyphLabel);
            button.Add(captionLabel);
            return button;
        }

        private static void DrawDiamond(
            MeshGenerationContext context,
            Rect rect,
            Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var size = Mathf.Min(rect.width, rect.height);
            var center = rect.center;
            var halfSize = size * 0.5f;
            var mesh = context.Allocate(4, 6);
            var tint = (Color32)color;
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(center.x, center.y - halfSize, Vertex.nearZ),
                tint = tint,
            });
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(center.x + halfSize, center.y, Vertex.nearZ),
                tint = tint,
            });
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(center.x, center.y + halfSize, Vertex.nearZ),
                tint = tint,
            });
            mesh.SetNextVertex(new Vertex
            {
                position = new Vector3(center.x - halfSize, center.y, Vertex.nearZ),
                tint = tint,
            });
            mesh.SetNextIndex(0);
            mesh.SetNextIndex(1);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(3);
            mesh.SetNextIndex(0);
        }
    }
}
