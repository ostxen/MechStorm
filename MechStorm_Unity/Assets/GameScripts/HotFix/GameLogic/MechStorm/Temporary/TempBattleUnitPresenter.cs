using MechStorm.Battle.Units;
using UnityEngine;

namespace MechStorm.Presentation
{
    public sealed class TempBattleUnitPresenter : BattleUnitPresenter
    {
        public TempBattleUnitPresenter(
            System.Collections.Generic.IReadOnlyDictionary<CombatUnit, Transform> healthBarAnchors,
            Camera camera)
            : base(healthBarAnchors, camera)
        {
        }
    }
}
