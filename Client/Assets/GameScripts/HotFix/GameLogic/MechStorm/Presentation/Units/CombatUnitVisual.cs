using System;
using MechStorm.Presentation.Board;
using UnityEngine;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Units
{
    public sealed class CombatUnitVisual
    {
        private readonly Transform _unitTrans;
        private readonly GridCoordinateConverter _coordConverter;
        private readonly float _visualHeightOffset;

        public CombatUnitVisual(Transform unitTrans, GridCoordinateConverter coordConverter, float visualHeightOffset = 0.5f)
        {
            _unitTrans = unitTrans ? unitTrans : throw new ArgumentNullException(nameof(unitTrans));
            _coordConverter = coordConverter ?? throw new ArgumentNullException(nameof(coordConverter));
            _visualHeightOffset = visualHeightOffset;
        }

        public void RefreshPosition(Vector2Int gridPosition)
        {
            _unitTrans.position = _coordConverter.GridToWorld(gridPosition) + Vector3.up * _visualHeightOffset;
        }
    }
}
