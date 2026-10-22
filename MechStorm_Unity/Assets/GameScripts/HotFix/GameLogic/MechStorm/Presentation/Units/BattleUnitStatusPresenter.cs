using System;
using System.Collections.Generic;
using MechStorm.Battle.Units;
using UnityEngine;

namespace MechStorm.Presentation.Units
{
    /// <summary>
    /// 刷新单位头顶的耐久、AP 条及其朝向，不负责单位模型或战斗规则。
    /// </summary>
    public class BattleUnitStatusPresenter
    {
        private readonly Dictionary<CombatUnit, UnitHealthBarView> _healthBarViews;

        public BattleUnitStatusPresenter(IReadOnlyDictionary<CombatUnit, Transform> healthBarAnchors, Camera camera)
        {
            if (healthBarAnchors == null)
            {
                throw new ArgumentNullException(nameof(healthBarAnchors));
            }

            _healthBarViews = new Dictionary<CombatUnit, UnitHealthBarView>(healthBarAnchors.Count);
            foreach (var pair in healthBarAnchors)
            {
                if (pair.Key == null)
                {
                    throw new ArgumentException(
                        "Health bar anchors cannot contain a null combat unit.",
                        nameof(healthBarAnchors));
                }

                if (pair.Value == null)
                {
                    throw new ArgumentException(
                        $"Combat unit {pair.Key.UnitId} requires a health bar anchor.",
                        nameof(healthBarAnchors));
                }

                _healthBarViews.Add(pair.Key, new UnitHealthBarView(pair.Value, camera));
            }

            RefreshAll();
            RefreshFacing();
        }

        public void Refresh(CombatUnit combatUnit)
        {
            if (combatUnit != null &&
                _healthBarViews.TryGetValue(combatUnit, out var healthBarView))
            {
                Refresh(combatUnit, healthBarView);
            }
        }

        public void RefreshAll()
        {
            foreach (var pair in _healthBarViews)
            {
                Refresh(pair.Key, pair.Value);
            }
        }

        public void RefreshFacing()
        {
            foreach (var healthBarView in _healthBarViews.Values)
            {
                healthBarView.RefreshFacing();
            }
        }

        private static void Refresh(CombatUnit combatUnit, UnitHealthBarView healthBarView)
        {
            healthBarView.RefreshValue(
                combatUnit.MechRuntime.CurrentDurability,
                combatUnit.Mech.MaxDurability);
            healthBarView.RefreshActionPoint(
                combatUnit.PilotRuntime.CurrentActionPoint,
                combatUnit.Pilot.MaxActionPoint);
        }
    }
}
