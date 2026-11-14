using System;
using System.Collections.Generic;
using MechStorm.Battle.Foundation;
using MechStorm.Battle.Spatial;
using MechStorm.Battle.Units;

namespace MechStorm.Battle.Rules
{
    public sealed class AttackResolver
    {
        private readonly SquareGrid _squareGrid;

        public AttackResolver(SquareGrid squareGrid)
        {
            _squareGrid = squareGrid ?? throw new ArgumentNullException(nameof(squareGrid));
        }

        public bool TryAttack(CombatUnit source, CombatUnit target)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!IsTargetInRange(source, target))
            {
                return false;
            }

            var attack = source.Mech.BasicAttack;
            if (!source.PilotRuntime.TrySpendActionPoint(attack.ActionPointCost))
            {
                return false;
            }

            target.MechRuntime.TakeDamage(attack.Damage);
            return true;
        }

        private bool IsTargetInRange(CombatUnit source, CombatUnit target)
        {
            return IsPositionInRange(source, target.Position);
        }

        public bool IsPositionInRange(CombatUnit source, Vector2Int targetPosition)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!_squareGrid.IsInside(source.Position) || !_squareGrid.IsInside(targetPosition))
            {
                return false;
            }

            var distance = _squareGrid.GetManhattanDistance(source.Position, targetPosition);
            var attack = source.Mech.BasicAttack;
            return distance >= attack.MinRange && distance <= attack.MaxRange;
        }

        public IReadOnlyList<Vector2Int> GetPositionsInRange(CombatUnit source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var positions = new List<Vector2Int>();
            for (var x = 0; x < _squareGrid.Width; x++)
            {
                for (var y = 0; y < _squareGrid.Height; y++)
                {
                    var position = new Vector2Int(x, y);
                    if (IsPositionInRange(source, position))
                    {
                        positions.Add(position);
                    }
                }
            }

            return positions;
        }
    }
}