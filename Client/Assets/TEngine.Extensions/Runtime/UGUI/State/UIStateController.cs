using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TEngine.Extensions.UGUI
{
    public enum UIStateBindingType
    {
        GameObjectActive,
        GraphicColor,
        ImageSprite,
        ImageSpriteAndColor,
        TextContent,
        SelectableInteractable,
        CanvasGroupAlpha,
        RectTransformAnchoredPosition
    }

    [Serializable]
    public sealed class UIStateBinding
    {
        [SerializeField] private UIStateBindingType _type;
        [SerializeField] private Component _target;
        [SerializeField] private bool _boolValue = true;
        [SerializeField] private Color _colorValue = Color.white;
        [SerializeField] private Sprite _spriteValue;
        [SerializeField] private string _textValue;
        [SerializeField, Range(0f, 1f)] private float _floatValue = 1f;
        [SerializeField] private Vector2 _vector2Value;

        public UIStateBindingType Type => _type;
        public Component Target => _target;

        public void Apply()
        {
            if (_target == null)
            {
                return;
            }

            switch (_type)
            {
                case UIStateBindingType.GameObjectActive:
                    _target.gameObject.SetActive(_boolValue);
                    break;
                case UIStateBindingType.GraphicColor:
                    if (_target is Graphic graphic)
                    {
                        graphic.color = _colorValue;
                    }

                    break;
                case UIStateBindingType.ImageSprite:
                    if (_target is Image image)
                    {
                        image.sprite = _spriteValue;
                    }

                    break;
                case UIStateBindingType.ImageSpriteAndColor:
                    if (_target is Image imageWithColor)
                    {
                        imageWithColor.sprite = _spriteValue;
                        imageWithColor.color = _colorValue;
                    }

                    break;
                case UIStateBindingType.TextContent:
                    if (_target is Text text)
                    {
                        text.text = _textValue ?? string.Empty;
                    }

                    break;
                case UIStateBindingType.SelectableInteractable:
                    if (_target is Selectable selectable)
                    {
                        selectable.interactable = _boolValue;
                    }

                    break;
                case UIStateBindingType.CanvasGroupAlpha:
                    if (_target is CanvasGroup canvasGroup)
                    {
                        canvasGroup.alpha = _floatValue;
                    }

                    break;
                case UIStateBindingType.RectTransformAnchoredPosition:
                    if (_target is RectTransform rectTransform)
                    {
                        rectTransform.anchoredPosition = _vector2Value;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    [Serializable]
    public sealed class UIStateDefinition
    {
        [SerializeField] private string _name = "State";
        [SerializeField] private List<UIStateBinding> _bindings = new();

        public string Name => string.IsNullOrWhiteSpace(_name) ? "State" : _name;
        public IReadOnlyList<UIStateBinding> Bindings => _bindings;

        internal void Apply()
        {
            for (var i = 0; i < _bindings.Count; i++)
            {
                _bindings[i]?.Apply();
            }
        }
    }

    [Serializable]
    public sealed class UIStateGroup
    {
        [SerializeField] private string _name = "State";
        [SerializeField] private int _defaultStateIndex;
        [SerializeField] private int _currentStateIndex;
        [SerializeField] private List<UIStateDefinition> _states = new();

        public string Name => string.IsNullOrWhiteSpace(_name) ? "State" : _name;
        public int DefaultStateIndex => ClampStateIndex(_defaultStateIndex);
        public int CurrentStateIndex => ClampStateIndex(_currentStateIndex);
        public string CurrentStateName => GetState(CurrentStateIndex)?.Name;
        public IReadOnlyList<UIStateDefinition> States => _states;

        public bool SetCurrentStateIndex(int index)
        {
            var nextIndex = ClampStateIndex(index);
            if (_currentStateIndex == nextIndex)
            {
                return false;
            }

            _currentStateIndex = nextIndex;
            return true;
        }

        public bool SetCurrentStateName(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            for (var i = 0; i < _states.Count; i++)
            {
                if (string.Equals(_states[i]?.Name, stateName, StringComparison.Ordinal))
                {
                    return SetCurrentStateIndex(i);
                }
            }

            return false;
        }

        public bool HasStateName(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            for (var i = 0; i < _states.Count; i++)
            {
                if (string.Equals(_states[i]?.Name, stateName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public UIStateDefinition GetState(int index)
        {
            return index >= 0 && index < _states.Count ? _states[index] : null;
        }

        internal void ApplyCurrent()
        {
            GetState(CurrentStateIndex)?.Apply();
        }

        private int ClampStateIndex(int index)
        {
            return _states.Count == 0 ? -1 : Mathf.Clamp(index, 0, _states.Count - 1);
        }
    }

    [Serializable]
    public sealed class UIStateLink
    {
        [SerializeField] private string _sourceGroup;
        [SerializeField] private string _sourceState;
        [SerializeField] private string _targetGroup;
        [SerializeField] private string _targetState;

        public string SourceGroup => _sourceGroup;
        public string SourceState => _sourceState;
        public string TargetGroup => _targetGroup;
        public string TargetState => _targetState;
    }

    [AddComponentMenu("TEngine/UI/UI State Controller")]
    [DisallowMultipleComponent]
    public sealed class UIStateController : MonoBehaviour
    {
        [SerializeField] private List<UIStateGroup> _groups = new();
        [SerializeField] private List<UIStateLink> _links = new();

        private readonly Dictionary<string, UIStateGroup> _groupLookup =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> _applyingGroups = new(StringComparer.Ordinal);

        public IReadOnlyList<UIStateGroup> Groups => _groups;
        public IReadOnlyList<UIStateLink> Links => _links;

        public event Action<UIStateGroup, UIStateDefinition> StateChanged;

        private void Awake()
        {
            ApplyAll();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                ApplyAll();
            }
        }

        public void ApplyAll()
        {
            RebuildLookup();
            for (var i = 0; i < _groups.Count; i++)
            {
                ApplyGroup(_groups[i], false);
            }

            for (var i = 0; i < _groups.Count; i++)
            {
                ApplyLinkedGroups(_groups[i]?.Name);
            }
        }

        public bool SetState(string groupName, string stateName)
        {
            if (!TryGetGroup(groupName, out var group) ||
                !group.HasStateName(stateName))
            {
                return false;
            }

            group.SetCurrentStateName(stateName);
            ApplyLinkedGroups(group.Name);
            return true;
        }

        public bool SetState(string groupName, int stateIndex)
        {
            if (!TryGetGroup(groupName, out var group) ||
                group.GetState(stateIndex) == null)
            {
                return false;
            }

            group.SetCurrentStateIndex(stateIndex);
            ApplyLinkedGroups(group.Name);
            return true;
        }

        public bool ResetState(string groupName)
        {
            if (!TryGetGroup(groupName, out var group) ||
                group.DefaultStateIndex < 0)
            {
                return false;
            }

            group.SetCurrentStateIndex(group.DefaultStateIndex);
            ApplyLinkedGroups(group.Name);
            return true;
        }

        public string GetStateName(string groupName)
        {
            return TryGetGroup(groupName, out var group)
                ? group.CurrentStateName
                : null;
        }

        public bool TryGetGroup(string groupName, out UIStateGroup group)
        {
            RebuildLookup();
            return _groupLookup.TryGetValue(groupName ?? string.Empty, out group);
        }

        public void GetReferencedObjects(List<UnityEngine.Object> result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.Clear();
            for (var groupIndex = 0; groupIndex < _groups.Count; groupIndex++)
            {
                var group = _groups[groupIndex];
                if (group == null)
                {
                    continue;
                }

                for (var stateIndex = 0; stateIndex < group.States.Count; stateIndex++)
                {
                    var state = group.States[stateIndex];
                    if (state == null)
                    {
                        continue;
                    }

                    for (var bindingIndex = 0;
                         bindingIndex < state.Bindings.Count;
                         bindingIndex++)
                    {
                        var target = state.Bindings[bindingIndex]?.Target;
                        if (target != null && !result.Contains(target))
                        {
                            result.Add(target);
                        }
                    }
                }
            }
        }

        private void ApplyLinkedGroups(string sourceGroupName)
        {
            if (string.IsNullOrEmpty(sourceGroupName) ||
                !_applyingGroups.Add(sourceGroupName))
            {
                return;
            }

            try
            {
                if (!TryGetGroup(sourceGroupName, out var sourceGroup))
                {
                    return;
                }

                ApplyGroup(sourceGroup, true);
                for (var i = 0; i < _links.Count; i++)
                {
                    var link = _links[i];
                    if (link == null ||
                        !string.Equals(link.SourceGroup, sourceGroup.Name, StringComparison.Ordinal) ||
                        !string.Equals(link.SourceState, sourceGroup.CurrentStateName,
                            StringComparison.Ordinal) ||
                        !TryGetGroup(link.TargetGroup, out var targetGroup) ||
                        !targetGroup.SetCurrentStateName(link.TargetState))
                    {
                        continue;
                    }

                    ApplyLinkedGroups(targetGroup.Name);
                }
            }
            finally
            {
                _applyingGroups.Remove(sourceGroupName);
            }
        }

        private void ApplyGroup(UIStateGroup group, bool notify)
        {
            if (group == null)
            {
                return;
            }

            group.ApplyCurrent();
            if (notify)
            {
                StateChanged?.Invoke(group, group.GetState(group.CurrentStateIndex));
            }
        }

        private void RebuildLookup()
        {
            _groupLookup.Clear();
            for (var i = 0; i < _groups.Count; i++)
            {
                var group = _groups[i];
                if (group != null && !_groupLookup.ContainsKey(group.Name))
                {
                    _groupLookup.Add(group.Name, group);
                }
            }
        }
    }
}
