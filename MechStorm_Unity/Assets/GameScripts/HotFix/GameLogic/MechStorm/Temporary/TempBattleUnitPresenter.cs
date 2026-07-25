using System;
using MechStorm.Battle.Units;
using MechStorm.Presentation.Units;
using UnityEngine;

namespace MechStorm.Presentation
{
    public sealed class TempBattleUnitPresenter
    {
        private readonly CombatUnit _playerAUnit;
        private readonly CombatUnit _enemyAUnit;
        private readonly UnitHealthBarView _playerAHealthBarView;
        private readonly UnitHealthBarView _enemyAHealthBarView;

        public TempBattleUnitPresenter(
            CombatUnit playerAUnit,
            Transform playerAHealthBarAnchor,
            CombatUnit enemyAUnit,
            Transform enemyAHealthBarAnchor,
            Camera camera)
        {
            _playerAUnit = playerAUnit ?? throw new ArgumentNullException(nameof(playerAUnit));
            _enemyAUnit = enemyAUnit ?? throw new ArgumentNullException(nameof(enemyAUnit));
            if (playerAHealthBarAnchor == null)
            {
                throw new ArgumentNullException(nameof(playerAHealthBarAnchor));
            }

            if (enemyAHealthBarAnchor == null)
            {
                throw new ArgumentNullException(nameof(enemyAHealthBarAnchor));
            }

            _playerAHealthBarView = new UnitHealthBarView(playerAHealthBarAnchor, camera);
            _enemyAHealthBarView = new UnitHealthBarView(enemyAHealthBarAnchor, camera);
            RefreshAll();
            RefreshFacing();
        }

        public void Refresh(CombatUnit combatUnit)
        {
            if (ReferenceEquals(combatUnit, _playerAUnit))
            {
                Refresh(_playerAUnit, _playerAHealthBarView);
                return;
            }

            if (ReferenceEquals(combatUnit, _enemyAUnit))
            {
                Refresh(_enemyAUnit, _enemyAHealthBarView);
            }
        }

        public void RefreshAll()
        {
            Refresh(_playerAUnit, _playerAHealthBarView);
            Refresh(_enemyAUnit, _enemyAHealthBarView);
        }

        public void RefreshFacing()
        {
            _playerAHealthBarView.RefreshFacing();
            _enemyAHealthBarView.RefreshFacing();
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
