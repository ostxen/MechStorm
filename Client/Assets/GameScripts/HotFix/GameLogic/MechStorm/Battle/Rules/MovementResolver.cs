using System;
using System.Collections.Generic;
using System.Linq;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Spatial;
using MechStorm.Battle.Units;

namespace MechStorm.Battle.Rules
{
    public sealed class MovementResolver
    {
        private readonly SquareGrid _grid;
        private readonly Func<Vector2Int, bool> _isPositionBlocked;

        public MovementResolver(SquareGrid grid) : this(grid, _ => false)
        {
        }

        public MovementResolver(SquareGrid grid, Func<Vector2Int, bool> isPositionBlocked)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _isPositionBlocked = isPositionBlocked ?? throw new ArgumentNullException(nameof(isPositionBlocked));
        }

        public bool TryMoveTo(CombatUnit unit, Vector2Int targetPos)
        {
            return TryMoveTo(unit, unit?.Position ?? default, targetPos);
        }

        /// <summary>
        /// 以固定范围锚点校验移动，避免暂定走位后从新位置重新获得移动力。
        /// </summary>
        public bool TryMoveTo(CombatUnit unit, Vector2Int rangeOrigin, Vector2Int targetPos)
        {
            if (!CanMoveTo(unit, rangeOrigin, targetPos))
            {
                return false;
            }

            unit.MoveTo(targetPos);
            return true;
        }

        public bool CanMoveTo(CombatUnit unit, Vector2Int targetPos)
        {
            return CanMoveTo(unit, unit?.Position ?? default, targetPos);
        }

        /// <summary>
        /// 判断目标是否属于指定范围锚点生成的可达集合。
        /// </summary>
        public bool CanMoveTo(CombatUnit unit, Vector2Int rangeOrigin, Vector2Int targetPos)
        {
            if (unit == null)
            {
                return false;
            }

            if (!_grid.IsInside(rangeOrigin) || !_grid.IsInside(targetPos))
            {
                return false;
            }

            var reachablePositions = GetReachablePositions(unit, rangeOrigin);
            return reachablePositions.Contains(targetPos);
        }

        public IReadOnlyList<Vector2Int> GetReachablePositions(CombatUnit unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            return GetReachablePositions(unit, unit.Position);
        }

        /// <summary>
        /// 从固定范围锚点计算可达格，并忽略移动中机兵自身的当前占格。
        /// </summary>
        public IReadOnlyList<Vector2Int> GetReachablePositions(CombatUnit unit, Vector2Int rangeOrigin)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            if (!_grid.IsInside(rangeOrigin))
            {
                throw new ArgumentOutOfRangeException(nameof(rangeOrigin), rangeOrigin,
                    "Movement range origin must be inside the grid.");
            }

            return _grid.GetReachablePositions(rangeOrigin, unit.Mech.MoveRange,
                position => position != unit.Position && _isPositionBlocked(position));
        }
    }
}
