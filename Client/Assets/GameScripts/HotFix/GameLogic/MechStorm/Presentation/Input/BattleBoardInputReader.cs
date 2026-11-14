using System;
using System.Collections.Generic;
using MechStorm.Battle.Units;
using MechStorm.Presentation.Board;
using UnityEngine;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;
using UnityInput = UnityEngine.Input;

namespace MechStorm.Presentation.Input
{
    /// <summary>
    /// 读取棋盘点击，将射线命中转换为单位或网格坐标，不判断行动是否合法。
    /// </summary>
    public sealed class BattleBoardInputReader
    {
        private const float MAX_RAY_DISTANCE = 1000f;

        private readonly Camera _camera;
        private readonly Collider _boardCollider;
        private readonly GridCoordinateConverter _coordinateConverter;
        // Unity Collider 到纯 C# 战斗单位的点击映射。
        private readonly IReadOnlyDictionary<Collider, CombatUnit> _combatUnitColliders;
        private readonly int _layerMask;
        private readonly Func<bool> _isPointerBlocked;
        private readonly Action<Vector2, Vector2> _panCamera;
        private readonly float _dragThresholdSquared;
        private bool _pressed;
        private bool _dragging;
        private Vector2 _pressPosition;
        private Vector2 _lastPosition;

        public BattleBoardInputReader(Camera camera, Collider boardCollider, GridCoordinateConverter coordinateConverter,
            IReadOnlyDictionary<Collider, CombatUnit> combatUnitColliders, int layerMask,
            float dragThreshold, Action<Vector2, Vector2> panCamera,
            Func<bool> isPointerBlocked = null)
        {
            _camera = camera;
            _boardCollider = boardCollider;
            _coordinateConverter = coordinateConverter;
            _combatUnitColliders = combatUnitColliders;
            _layerMask = layerMask;
            _isPointerBlocked = isPointerBlocked;
            _dragThresholdSquared = dragThreshold * dragThreshold;
            _panCamera = panCamera;
        }

        public bool Tick(out CombatUnit combatUnit, out Vector2Int gridPosition)
        {
            return ProcessPointer(UnityInput.mousePosition, UnityInput.GetMouseButtonDown(0),
                UnityInput.GetMouseButton(0), UnityInput.GetMouseButtonUp(0),
                _isPointerBlocked?.Invoke() == true, out combatUnit, out gridPosition);
        }

        public void CancelGesture()
        {
            _pressed = false;
            _dragging = false;
        }

        /// <summary>
        /// 按下只记录候选点击；超过阈值后独占为拖拽，只有未拖拽的松开才提交棋盘射线。
        /// </summary>
        public bool ProcessPointer(Vector2 position, bool down, bool held, bool up, bool blocked,
            out CombatUnit combatUnit, out Vector2Int gridPosition)
        {
            combatUnit = null;
            gridPosition = default;
            if (blocked || !_camera.pixelRect.Contains(position))
            {
                CancelGesture();
                return false;
            }
            if (down)
            {
                _pressed = true;
                _dragging = false;
                _pressPosition = _lastPosition = position;
            }
            if (!_pressed) return false;
            if (!held && !up)
            {
                CancelGesture();
                return false;
            }
            if ((position - _pressPosition).sqrMagnitude > _dragThresholdSquared) _dragging = true;
            if (_dragging)
            {
                _panCamera(_lastPosition, position);
                _lastPosition = position;
            }
            if (!up) return false;
            bool clicked = !_dragging;
            CancelGesture();
            return clicked && ReadHit(position, out combatUnit, out gridPosition);
        }

        private bool ReadHit(Vector2 position, out CombatUnit combatUnit, out Vector2Int gridPosition)
        {
            combatUnit = null;
            gridPosition = default;
            var ray = _camera.ScreenPointToRay(position);
            if (!Physics.Raycast(ray, out var hitInfo, MAX_RAY_DISTANCE, _layerMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (_combatUnitColliders.TryGetValue(hitInfo.collider, out combatUnit))
            {
                return true;
            }

            if (hitInfo.collider != _boardCollider)
            {
                return false;
            }

            gridPosition = _coordinateConverter.WorldToGrid(hitInfo.point);
            return true;
        }
    }
}
