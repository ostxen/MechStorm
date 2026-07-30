using MechStorm.Presentation.Controllers;
using NUnit.Framework;

namespace MechStorm.Presentation.Tests
{
    [TestFixture]
    public sealed class TempBattleActionPanelStateTests
    {
        [Test]
        public void ActionList_ShowsCatalogWithoutSelectionControls()
        {
            var state = TempBattleActionPanelState.Create(
                true,
                false,
                BattleActionSelectionState.ActionList,
                BattleActionSelectionPath.None,
                false,
                false,
                null,
                null);

            Assert.That(state.ShowActionCatalog, Is.True);
            Assert.That(state.ShowSelectedAction, Is.False);
            Assert.That(state.ShowTargetActions, Is.False);
            Assert.That(state.ShowSelectionControls, Is.False);
            Assert.That(state.ShowConfirm, Is.False);
        }

        [Test]
        public void ActionSelected_ShowsDisabledConfirmAndCancel()
        {
            var state = TempBattleActionPanelState.Create(
                true,
                false,
                BattleActionSelectionState.ActionSelected,
                BattleActionSelectionPath.ActionFirst,
                false,
                false,
                "强袭炮击",
                null);

            Assert.That(state.ShowActionCatalog, Is.False);
            Assert.That(state.ShowSelectedAction, Is.True);
            Assert.That(state.ShowTargetActions, Is.False);
            Assert.That(state.ShowBasicWeapon, Is.False);
            Assert.That(state.ShowSelectionControls, Is.True);
            Assert.That(state.ShowConfirm, Is.False);
            Assert.That(state.CanConfirm, Is.False);
            Assert.That(state.Prompt, Does.Contain("强袭炮击"));
        }

        [Test]
        public void TargetSelected_ShowsCompatibleActionListWithoutConfirmationControls()
        {
            var state = TempBattleActionPanelState.Create(
                true,
                false,
                BattleActionSelectionState.TargetSelected,
                BattleActionSelectionPath.TargetFirst,
                false,
                false,
                null,
                "敌方机兵");

            Assert.That(state.ShowActionCatalog, Is.False);
            Assert.That(state.ShowSelectedAction, Is.False);
            Assert.That(state.ShowTargetActions, Is.True);
            Assert.That(state.ShowBasicWeapon, Is.False);
            Assert.That(state.ShowSelectionControls, Is.False);
            Assert.That(state.ShowConfirm, Is.False);
            Assert.That(state.Prompt, Does.Contain("敌方机兵"));
        }

        [Test]
        public void AwaitingConfirmation_ShowsEnabledConfirmAndCancel()
        {
            var state = TempBattleActionPanelState.Create(
                true,
                false,
                BattleActionSelectionState.AwaitingConfirmation,
                BattleActionSelectionPath.TargetFirst,
                false,
                true,
                "默认基础武器",
                "敌方机兵");

            Assert.That(state.ShowActionCatalog, Is.False);
            Assert.That(state.ShowSelectedAction, Is.False);
            Assert.That(state.ShowTargetActions, Is.True);
            Assert.That(state.ShowBasicWeapon, Is.False);
            Assert.That(state.ShowSelectionControls, Is.True);
            Assert.That(state.ShowConfirm, Is.True);
            Assert.That(state.CanConfirm, Is.True);
            Assert.That(state.Prompt, Does.Contain("默认基础武器"));
            Assert.That(state.Prompt, Does.Contain("敌方机兵"));
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void ActionFirstAwaitingConfirmation_ShowsOnlySelectedSkillDependencyWeapon(
            bool requiresBasicWeapon,
            bool expectedWeapon)
        {
            var state = TempBattleActionPanelState.Create(
                true,
                false,
                BattleActionSelectionState.AwaitingConfirmation,
                BattleActionSelectionPath.ActionFirst,
                requiresBasicWeapon,
                true,
                "主动技能",
                "敌方机兵");

            Assert.That(state.ShowSelectedAction, Is.True);
            Assert.That(state.ShowTargetActions, Is.False);
            Assert.That(state.ShowBasicWeapon, Is.EqualTo(expectedWeapon));
            Assert.That(state.ShowSelectionControls, Is.True);
            Assert.That(state.ShowConfirm, Is.True);
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void MissingSelectionOrEndedBattle_HidesAllActionRegions(
            bool hasSelectedUnit,
            bool isBattleEnded)
        {
            var state = TempBattleActionPanelState.Create(
                hasSelectedUnit,
                isBattleEnded,
                BattleActionSelectionState.ActionList,
                BattleActionSelectionPath.None,
                false,
                false,
                null,
                null);

            Assert.That(state.ShowActionCatalog, Is.False);
            Assert.That(state.ShowSelectedAction, Is.False);
            Assert.That(state.ShowTargetActions, Is.False);
            Assert.That(state.ShowSelectionControls, Is.False);
            Assert.That(state.ShowConfirm, Is.False);
            Assert.That(state.CanConfirm, Is.False);
        }
    }
}
