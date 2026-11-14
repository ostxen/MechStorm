using System;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using MechStorm.Presentation.Controllers;
using NUnit.Framework;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Tests.Controllers
{
    public class BattleActionSelectionStateMachineTests
    {
        [Test]
        public void NewStateMachine_StartsAtUnlockedActionList()
        {
            var stateMachine = new BattleActionSelectionStateMachine();

            Assert.AreEqual(BattleActionSelectionState.ActionList, stateMachine.State);
            Assert.IsFalse(stateMachine.IsInputLocked);
            Assert.IsFalse(stateMachine.CanConfirm);
            Assert.IsNull(stateMachine.SelectedAction);
            Assert.IsNull(stateMachine.SelectedTarget);
            Assert.AreEqual(BattleActionSelectionPath.None, stateMachine.Path);
        }

        [Test]
        public void SelectActionThenTarget_EntersAwaitingConfirmation()
        {
            var stateMachine = new BattleActionSelectionStateMachine();
            var action = BattleActionSelection.ForBasicAttack();
            var target = BattleActionTargetSelection.ForUnit(CreateCombatUnit(2));

            stateMachine.SelectAction(action);

            Assert.AreEqual(BattleActionSelectionState.ActionSelected, stateMachine.State);
            Assert.AreEqual(BattleActionSelectionPath.ActionFirst, stateMachine.Path);
            Assert.IsTrue(stateMachine.IsInputLocked);
            Assert.IsFalse(stateMachine.CanConfirm);

            stateMachine.SelectTarget(target);

            Assert.AreEqual(BattleActionSelectionState.AwaitingConfirmation, stateMachine.State);
            Assert.IsTrue(stateMachine.CanConfirm);
            Assert.AreEqual(BattleActionSelectionPath.ActionFirst, stateMachine.Path);
            Assert.AreSame(action, stateMachine.SelectedAction);
            Assert.AreSame(target, stateMachine.SelectedTarget);
        }

        [Test]
        public void SelectTargetThenAction_EntersAwaitingConfirmation()
        {
            var stateMachine = new BattleActionSelectionStateMachine();
            var ability = new AbilityDefinition(
                10, "Strike", 1, 2, 20, 1, TargetRule.EnemyUnit);
            var action = BattleActionSelection.ForAbility(ability);
            var target = BattleActionTargetSelection.ForUnit(CreateCombatUnit(2));

            stateMachine.SelectTarget(target);
            Assert.AreEqual(BattleActionSelectionState.TargetSelected, stateMachine.State);
            Assert.AreEqual(BattleActionSelectionPath.TargetFirst, stateMachine.Path);

            stateMachine.SelectAction(action);

            Assert.AreEqual(BattleActionSelectionState.AwaitingConfirmation, stateMachine.State);
            Assert.IsTrue(stateMachine.CanConfirm);
            Assert.AreEqual(BattleActionSelectionPath.TargetFirst, stateMachine.Path);
            Assert.AreSame(ability, stateMachine.SelectedAction.Ability);
        }

        [Test]
        public void ClearPartialSelection_ReturnsToRemainingExplicitState()
        {
            var stateMachine = new BattleActionSelectionStateMachine();
            stateMachine.SelectAction(BattleActionSelection.ForBasicAttack());
            stateMachine.SelectTarget(
                BattleActionTargetSelection.ForUnit(CreateCombatUnit(2)));

            stateMachine.ClearAction();
            Assert.AreEqual(BattleActionSelectionState.TargetSelected, stateMachine.State);
            Assert.AreEqual(BattleActionSelectionPath.TargetFirst, stateMachine.Path);

            stateMachine.SelectAction(BattleActionSelection.ForBasicAttack());
            stateMachine.ClearTarget();
            Assert.AreEqual(BattleActionSelectionState.ActionSelected, stateMachine.State);
            Assert.AreEqual(BattleActionSelectionPath.ActionFirst, stateMachine.Path);
        }

        [Test]
        public void Cancel_ClearsActionAndTargetAndUnlocksInput()
        {
            var stateMachine = new BattleActionSelectionStateMachine();
            stateMachine.SelectAction(BattleActionSelection.ForBasicAttack());
            stateMachine.SelectTarget(
                BattleActionTargetSelection.ForUnit(CreateCombatUnit(2)));

            stateMachine.Cancel();

            Assert.AreEqual(BattleActionSelectionState.ActionList, stateMachine.State);
            Assert.IsFalse(stateMachine.IsInputLocked);
            Assert.IsNull(stateMachine.SelectedAction);
            Assert.IsNull(stateMachine.SelectedTarget);
            Assert.AreEqual(BattleActionSelectionPath.None, stateMachine.Path);
        }

        [Test]
        public void InvalidSelections_ThrowWithoutChangingState()
        {
            var stateMachine = new BattleActionSelectionStateMachine();
            var action = BattleActionSelection.ForBasicAttack();
            stateMachine.SelectAction(action);

            Assert.Throws<ArgumentNullException>(() => stateMachine.SelectAction(null));
            Assert.Throws<ArgumentNullException>(() => stateMachine.SelectTarget(null));

            Assert.AreEqual(BattleActionSelectionState.ActionSelected, stateMachine.State);
            Assert.AreSame(action, stateMachine.SelectedAction);
            Assert.IsNull(stateMachine.SelectedTarget);
        }

        [Test]
        public void TargetFactories_PreserveReservedTargetKinds()
        {
            var unit = CreateCombatUnit(2);
            var grid = new Vector2Int(3, 4);

            Assert.AreEqual(
                BattleActionTargetKind.Unit,
                BattleActionTargetSelection.ForUnit(unit).Kind);
            Assert.AreEqual(
                BattleActionTargetKind.Grid,
                BattleActionTargetSelection.ForGrid(grid).Kind);
            Assert.AreEqual(
                BattleActionTargetKind.Self,
                BattleActionTargetSelection.ForSelf(unit).Kind);
            Assert.AreEqual(
                BattleActionTargetKind.None,
                BattleActionTargetSelection.WithoutTarget().Kind);
            Assert.Throws<ArgumentNullException>(() =>
                BattleActionTargetSelection.ForUnit(null));
            Assert.Throws<ArgumentNullException>(() =>
                BattleActionTargetSelection.ForSelf(null));
        }

        [Test]
        public void ActionFactories_PreserveSourceAndTargetContracts()
        {
            var ability = new AbilityDefinition(
                10, "Strike", 1, 2, 20, 1, TargetRule.EnemyUnit, true);

            var basicAttack = BattleActionSelection.ForBasicAttack();
            var activeAbility = BattleActionSelection.ForAbility(ability);

            Assert.AreEqual(
                BattleActionSelectionKind.BasicAttack,
                basicAttack.Kind);
            Assert.AreEqual(
                AbilityTargetRangeSource.BasicWeapon,
                basicAttack.RangeSource);
            Assert.AreEqual(BattleActionTargetKind.Unit, basicAttack.TargetKind);
            Assert.IsNull(basicAttack.Ability);
            Assert.AreEqual(
                BattleActionSelectionKind.ActiveAbility,
                activeAbility.Kind);
            Assert.AreEqual(
                AbilityTargetRangeSource.Ability,
                activeAbility.RangeSource);
            Assert.AreEqual(BattleActionTargetKind.Unit, activeAbility.TargetKind);
            Assert.AreSame(ability, activeAbility.Ability);
            Assert.AreEqual(
                AbilityTargetRangeSource.BasicWeapon,
                BattleActionSelection.ForAbility(
                    ability,
                    AbilityTargetRangeSource.BasicWeapon).RangeSource);
            Assert.Throws<ArgumentNullException>(() =>
                BattleActionSelection.ForAbility(null));
            Assert.Throws<ArgumentException>(() =>
                BattleActionSelection.ForAbility(
                    new AbilityDefinition(
                        11, "Independent", 1, 2, 20, 1, TargetRule.EnemyUnit),
                    AbilityTargetRangeSource.BasicWeapon));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BattleActionSelection.ForAbility(
                    ability,
                    (AbilityTargetRangeSource)999));
        }

        [Test]
        public void ActionIdentity_MatchesKindAbilityAndRangeSource()
        {
            var ability = new AbilityDefinition(
                10, "Strike", 1, 2, 20, 1, TargetRule.EnemyUnit, true);
            var otherAbility = new AbilityDefinition(
                11, "Other", 1, 2, 20, 1, TargetRule.EnemyUnit, true);
            var abilityAction = BattleActionSelection.ForAbility(ability);

            Assert.IsTrue(
                BattleActionSelection.ForBasicAttack().Matches(
                    BattleActionSelection.ForBasicAttack()));
            Assert.IsTrue(
                abilityAction.Matches(
                    BattleActionSelection.ForAbility(ability)));
            Assert.IsFalse(
                abilityAction.Matches(
                    BattleActionSelection.ForAbility(
                        ability,
                        AbilityTargetRangeSource.BasicWeapon)));
            Assert.IsFalse(
                abilityAction.Matches(
                    BattleActionSelection.ForAbility(otherAbility)));
            Assert.IsFalse(abilityAction.Matches(null));
        }

        private static CombatUnit CreateCombatUnit(int unitId)
        {
            var pilot = new PilotData(unitId, $"Pilot {unitId}", 3);
            var mech = new MechData(
                unitId, $"Mech {unitId}", new BasicAttackData(10, 1, 1), 100, 3);
            return new CombatUnitFactory().Create(
                unitId, pilot, mech, new Vector2Int(unitId, 0));
        }
    }
}
