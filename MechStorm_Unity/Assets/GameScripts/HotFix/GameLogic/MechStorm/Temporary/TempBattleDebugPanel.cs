using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MechStorm.Presentation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class TempBattleDebugPanel : MonoBehaviour
    {
        private const int UnitPanelWidth = 390;
        private const int ActionPanelWidth = 460;
        private const string RuntimeThemeResourcePath = "MechStorm/TempBattleRuntimeTheme";

        private UIDocument _document;
        private PanelSettings _ownedPanelSettings;
        [SerializeField]
        private TempBattleHudTheme _theme;
        private VisualElement _panel;
        private VisualElement _durabilityFill;
        private VisualElement _actionPointSegmentsRoot;
        private VisualElement _diagnosticsDrawer;
        private VisualElement _targetingPrompt;
        private VisualElement _unitCard;
        private VisualElement _actionDock;
        private VisualElement _turnControlDock;
        private VisualElement _normalActionsRoot;
        private VisualElement _targetingActionsRoot;
        private Label _roundLabel;
        private Label _factionLabel;
        private Label _unitLabel;
        private Label _durabilityLabel;
        private Label _actionPointLabel;
        private Label _messageLabel;
        private Label _selectedTargetLabel;
        private Label _selectedAttackTargetLabel;
        private Button _endActionButton;
        private Button _endFactionTurnButton;
        private Button _attackButton;
        private Button _abilityButton;
        private Button _targetingAbilityButton;
        private Button _cancelAbilityButton;
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

            _actionDock = CreateHudCard(ActionPanelWidth);
            _actionDock.style.position = Position.Absolute;
            _actionDock.style.right = 22;
            _actionDock.style.bottom = 22;
            _actionDock.style.display = DisplayStyle.None;
            var actionEyebrow = CreateLabel("行动指令", 18, AccentText,
                FontStyle.Bold);
            _actionDock.Add(actionEyebrow);
            var actionHint = CreateLabel("点击格子移动\n点击敌方机兵选择普通攻击目标", 20, SecondaryText,
                FontStyle.Bold);
            actionHint.style.marginTop = 5;
            actionHint.style.marginBottom = 14;
            _actionDock.Add(actionHint);

            _normalActionsRoot = new VisualElement();
            _normalActionsRoot.Add(CreateCommandSectionLabel("技能"));
            _abilityButton = CreateButton("强袭炮击 · AP 2 · 伤害 20",
                AbilityButton, 72);
            _normalActionsRoot.Add(_abilityButton);

            _normalActionsRoot.Add(CreateCommandSectionLabel("直接攻击"));
            _selectedAttackTargetLabel = CreateLabel("目标 · 尚未选择", 20, TargetText, FontStyle.Bold);
            _selectedAttackTargetLabel.style.marginBottom = 8;
            _normalActionsRoot.Add(_selectedAttackTargetLabel);
            _attackButton = CreateButton("快速普通攻击",
                AttackButton, 64);
            _normalActionsRoot.Add(_attackButton);
            _actionDock.Add(_normalActionsRoot);

            _targetingActionsRoot = new VisualElement();
            _targetingActionsRoot.style.display = DisplayStyle.None;
            _targetingActionsRoot.Add(CreateCommandSectionLabel("技能目标选择"));
            _selectedTargetLabel = CreateLabel("目标 · 尚未选择", 20, TargetText, FontStyle.Bold);
            _selectedTargetLabel.style.marginBottom = 10;
            var targetingButtonRow = new VisualElement();
            targetingButtonRow.style.flexDirection = FlexDirection.Row;
            _targetingAbilityButton = CreateButton("强袭炮击 · 请选择目标",
                AbilityButton, 72);
            _targetingAbilityButton.style.flexGrow = 1;
            _cancelAbilityButton = CreateButton("取消",
                NeutralButton, 64);
            _cancelAbilityButton.style.width = 96;
            _cancelAbilityButton.style.height = 72;
            _cancelAbilityButton.style.marginLeft = 8;
            _targetingActionsRoot.Add(_selectedTargetLabel);
            targetingButtonRow.Add(_targetingAbilityButton);
            targetingButtonRow.Add(_cancelAbilityButton);
            _targetingActionsRoot.Add(targetingButtonRow);
            _actionDock.Add(_targetingActionsRoot);
            _panel.Add(_actionDock);

            _turnControlDock = CreateHudCard(UnitPanelWidth);
            _turnControlDock.style.position = Position.Absolute;
            _turnControlDock.style.left = 22;
            _turnControlDock.style.bottom = 22;
            _turnControlDock.style.display = DisplayStyle.None;
            var turnControlTitle = CreateLabel("回合控制", 18, AccentText, FontStyle.Bold);
            var turnControlHint = CreateLabel("无需选中机兵，可直接推进战斗流程", 19, SecondaryText);
            turnControlHint.style.marginTop = 5;
            turnControlHint.style.marginBottom = 14;
            _endActionButton = CreateButton("结束当前机兵行动", EndActionButton, 68);
            _endFactionTurnButton = CreateButton("结束当前阵营回合", EndTurnButton, 64);
            _turnControlDock.Add(turnControlTitle);
            _turnControlDock.Add(turnControlHint);
            _turnControlDock.Add(_endActionButton);
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
            _diagnosticsDrawer.style.right = 22;
            _diagnosticsDrawer.style.top = 74;
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

            _targetingPrompt = CreateHudCard(540);
            _targetingPrompt.style.position = Position.Absolute;
            _targetingPrompt.style.left = new Length(50, LengthUnit.Percent);
            _targetingPrompt.style.bottom = 24;
            _targetingPrompt.style.marginLeft = -270;
            _targetingPrompt.style.display = DisplayStyle.None;
            _targetingPrompt.style.borderTopColor = AccentText;
            _messageLabel = CreateLabel("请选择技能范围内的高亮目标",
                20, PrimaryText, FontStyle.Bold);
            _messageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _targetingPrompt.Add(_messageLabel);
            _panel.Add(_targetingPrompt);

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
            _endActionButton.clicked += OnEndActionClicked;
            _endFactionTurnButton.clicked += OnEndFactionTurnClicked;
            _attackButton.clicked += OnAttackClicked;
            _abilityButton.clicked += OnAbilityClicked;
            _targetingAbilityButton.clicked += OnAbilityClicked;
            _cancelAbilityButton.clicked += OnCancelAbilityClicked;
            _damageButton.clicked += OnDamageClicked;
            _logButton.clicked += OnLogClicked;
            _exportButton.clicked += OnExportClicked;
            _diagnosticsButton.clicked += OnDiagnosticsClicked;
        }

        private void UnregisterCallbacks()
        {
            if (_endActionButton == null)
            {
                return;
            }

            _endActionButton.clicked -= OnEndActionClicked;
            _endFactionTurnButton.clicked -= OnEndFactionTurnClicked;
            _attackButton.clicked -= OnAttackClicked;
            _abilityButton.clicked -= OnAbilityClicked;
            _targetingAbilityButton.clicked -= OnAbilityClicked;
            _cancelAbilityButton.clicked -= OnCancelAbilityClicked;
            _damageButton.clicked -= OnDamageClicked;
            _logButton.clicked -= OnLogClicked;
            _exportButton.clicked -= OnExportClicked;
            _diagnosticsButton.clicked -= OnDiagnosticsClicked;
        }

        private void Refresh()
        {
            var ready = _actions != null && _actions.IsBattleReady;
            var hasSelectedUnit = ready && _actions.IsCurrentUnitSelected;
            _unitCard.style.display = hasSelectedUnit ? DisplayStyle.Flex : DisplayStyle.None;
            _actionDock.style.display = hasSelectedUnit ? DisplayStyle.Flex : DisplayStyle.None;
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

            var targeting = ready && _actions.IsAbilityTargeting;
            _targetingPrompt.style.display = targeting ? DisplayStyle.Flex : DisplayStyle.None;
            _normalActionsRoot.style.display = targeting ? DisplayStyle.None : DisplayStyle.Flex;
            _targetingActionsRoot.style.display = targeting ? DisplayStyle.Flex : DisplayStyle.None;
            SetButtonsEnabled(ready);
            if (ready && _actions.IsBattleEnded)
            {
                SetBattleInteractionButtonsEnabled(false);
            }

            var selectedAttackTargetName = ready
                ? _actions.SelectedAttackTargetName
                : null;
            _selectedAttackTargetLabel.text = string.IsNullOrEmpty(selectedAttackTargetName)
                ? "目标 · 尚未选择"
                : $"目标 · {selectedAttackTargetName}";
            _attackButton.text = string.IsNullOrEmpty(selectedAttackTargetName)
                ? "快速普通攻击 · 请选择目标"
                : $"快速普通攻击 · 攻击 {selectedAttackTargetName}";
            _attackButton.SetEnabled(ready && _actions.CanConfirmBasicAttack);
            if (ready)
            {
                var unavailableReason = _actions.AbilityUnavailableReason;
                _abilityButton.text = string.IsNullOrEmpty(unavailableReason)
                    ? $"{_actions.AbilityName} · AP {_actions.AbilityActionPointCost} · " +
                      $"伤害 {_actions.AbilityDamage}"
                    : $"{_actions.AbilityName} · 不可使用\n{unavailableReason}";
                _abilityButton.SetEnabled(string.IsNullOrEmpty(unavailableReason));
                _damageButton.text = $"对我方机兵造成 {_actions.DebugDamage} 点伤害";
                var selectedTargetName = _actions.SelectedAbilityTargetName;
                _selectedTargetLabel.text = string.IsNullOrEmpty(selectedTargetName)
                    ? "目标 · 尚未选择"
                    : $"目标 · {selectedTargetName}";
                _targetingAbilityButton.text = string.IsNullOrEmpty(selectedTargetName)
                    ? $"{_actions.AbilityName} · 请选择目标"
                    : $"{_actions.AbilityName} · 再次点击确认";
                _targetingAbilityButton.SetEnabled(_actions.CanConfirmAbilityTargeting);
                _messageLabel.text = string.IsNullOrEmpty(selectedTargetName)
                    ? "请选择技能范围内的高亮目标"
                    : $"已选择 {selectedTargetName}，再次点击技能确认释放";
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            _endActionButton.SetEnabled(enabled);
            _endFactionTurnButton.SetEnabled(enabled);
            _attackButton.SetEnabled(enabled);
            _abilityButton.SetEnabled(enabled);
            _targetingAbilityButton.SetEnabled(enabled);
            _cancelAbilityButton.SetEnabled(enabled);
            _damageButton.SetEnabled(enabled);
            _logButton.SetEnabled(enabled);
            _exportButton.SetEnabled(enabled);
        }

        private void SetBattleInteractionButtonsEnabled(bool enabled)
        {
            _endActionButton.SetEnabled(enabled);
            _endFactionTurnButton.SetEnabled(enabled);
            _attackButton.SetEnabled(enabled);
            _abilityButton.SetEnabled(enabled);
            _targetingAbilityButton.SetEnabled(enabled);
            _cancelAbilityButton.SetEnabled(enabled);
            _damageButton.SetEnabled(enabled);
        }

        private void OnEndActionClicked()
        {
            _actions?.EndCurrentUnitAction();
            Refresh();
        }

        private void OnEndFactionTurnClicked()
        {
            _actions?.EndCurrentFactionTurn();
            Refresh();
        }

        private void OnAttackClicked()
        {
            _actions?.ConfirmBasicAttack();
            Refresh();
        }

        private void OnAbilityClicked()
        {
            if (_actions?.IsAbilityTargeting == true)
            {
                _actions.ConfirmAbilityTargeting();
            }
            else
            {
                _actions?.BeginAbilityTargeting();
            }

            Refresh();
        }

        private void OnCancelAbilityClicked()
        {
            _actions?.CancelAbilityTargeting();
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

        private Label CreateCommandSectionLabel(string text)
        {
            var label = CreateLabel(text, 16, SecondaryText, FontStyle.Bold);
            label.style.marginTop = 5;
            label.style.marginBottom = 5;
            return label;
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
    }
}
