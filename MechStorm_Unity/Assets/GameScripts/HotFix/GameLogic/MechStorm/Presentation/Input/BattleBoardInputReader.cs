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

        public BattleBoardInputReader(Camera camera, Collider boardCollider, GridCoordinateConverter coordinateConverter,
            IReadOnlyDictionary<Collider, CombatUnit> combatUnitColliders, int layerMask,
            Func<bool> isPointerBlocked = null)
        {
            _camera = camera ? camera : throw new ArgumentNullException(nameof(camera));
            _boardCollider = boardCollider ? boardCollider : throw new ArgumentNullException(nameof(boardCollider));
            _coordinateConverter = coordinateConverter ?? throw new ArgumentNullException(nameof(coordinateConverter));
            _combatUnitColliders = combatUnitColliders ?? throw new ArgumentNullException(nameof(combatUnitColliders));
            _layerMask = layerMask;
            _isPointerBlocked = isPointerBlocked;
        }

        public bool Tick(out CombatUnit combatUnit, out Vector2Int gridPosition)
        {
            combatUnit = null;
            gridPosition = default;

            if (!UnityInput.GetMouseButtonDown(0) || _isPointerBlocked?.Invoke() == true) return false;

            var ray = _camera.ScreenPointToRay(UnityInput.mousePosition);
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
