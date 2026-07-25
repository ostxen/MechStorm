using UnityEngine;

namespace MechStorm.Presentation
{
    [CreateAssetMenu(
        fileName = "TempBattleHudTheme",
        menuName = "MechStorm/Temporary/Battle HUD Theme")]
    public sealed class TempBattleHudTheme : ScriptableObject
    {
        [SerializeField] private Color _panelBackground;
        [SerializeField] private Color _panelBorder;
        [SerializeField] private Color _primaryText;
        [SerializeField] private Color _secondaryText;
        [SerializeField] private Color _accentText;
        [SerializeField] private Color _targetText;
        [SerializeField] private Color _abilityButton;
        [SerializeField] private Color _attackButton;
        [SerializeField] private Color _endActionButton;
        [SerializeField] private Color _endTurnButton;
        [SerializeField] private Color _neutralButton;
        [SerializeField] private Color _buttonText;
        [SerializeField] private Color _durabilityTrack;
        [SerializeField] private Color _durabilityFill;
        [SerializeField] private Color _actionPointFilled;
        [SerializeField] private Color _actionPointEmpty;

        public Color PanelBackground => _panelBackground;
        public Color PanelBorder => _panelBorder;
        public Color PrimaryText => _primaryText;
        public Color SecondaryText => _secondaryText;
        public Color AccentText => _accentText;
        public Color TargetText => _targetText;
        public Color AbilityButton => _abilityButton;
        public Color AttackButton => _attackButton;
        public Color EndActionButton => _endActionButton;
        public Color EndTurnButton => _endTurnButton;
        public Color NeutralButton => _neutralButton;
        public Color ButtonText => _buttonText;
        public Color DurabilityTrack => _durabilityTrack;
        public Color DurabilityFill => _durabilityFill;
        public Color ActionPointFilled => _actionPointFilled;
        public Color ActionPointEmpty => _actionPointEmpty;
    }
}
