using System.Collections.Generic;
using MechStorm.Battle;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using MechStorm.Presentation.Controllers;
using NUnit.Framework;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Presentation.Tests.Controllers
{
    public class BattleActionSelectionCoordinatorTests
    {
        [Test]
        public void GetAvailableActions_ReturnsBasicAttackAndConfiguredAbility()
        {
            var context = CreateContext();

            var actions = context.Coordinator.GetAvailableActions();

            Assert.AreEqual(2, actions.Count);
            Assert.AreEqual(BattleActionSelectionKind.BasicAttack, actions[0].Kind);
            Assert.AreEqual(BattleActionSelectionKind.ActiveAbility, actions[1].Kind);
            Assert.AreSame(context.Ability, actions[1].Ability);
        }

        [Test]
        public void GetAvailableActions_WithoutBasicAttackActionPoint_OmitsBasicAttack()
        {
            var context = CreateContext(basicAttackActionPointCost: 4);

            var actions = context.Coordinator.GetAvailableActions();

            Assert.AreEqual(1, actions.Count);
            Assert.AreEqual(BattleActionSelectionKind.ActiveAbility, actions[0].Kind);
        }

        [Test]
        public void SelectAbility_WithoutTargetsStillShowsRangeAndAllowsCancel()
        {
            var context = CreateContext(
                enemyPosition: new Vector2Int(5, 5));
            Assert.IsEmpty(
                context.Session.GetCurrentCombatUnitAbilityTargets(
                    context.Ability));

            var selected = context.Coordinator.TrySelectAction(
                BattleActionSelection.ForAbility(context.Ability),
                out var rejectionReason);

            Assert.IsTrue(selected);
            Assert.IsNull(rejectionReason);
            Assert.AreEqual(
                BattleActionSelectionState.ActionSelected,
                context.Coordinator.State);
            Assert.IsTrue(context.Coordinator.IsInputLocked);

            context.Coordinator.Cancel();

            Assert.AreEqual(
                BattleActionSelectionState.ActionList,
                context.Coordinator.State);
            Assert.IsFalse(context.Coordinator.IsInputLocked);
        }

        [Test]
        public void GetAvailableActions_WithoutTargetsStillListsConfiguredActions()
        {
            var context = CreateContext(
                enemyPosition: new Vector2Int(5, 5));

            var actions = context.Coordinator.GetAvailableActions();

            Assert.AreEqual(2, actions.Count);
            Assert.AreEqual(
                BattleActionSelectionKind.BasicAttack,
                actions[0].Kind);
            Assert.AreEqual(
                BattleActionSelectionKind.ActiveAbility,
                actions[1].Kind);
        }

        [Test]
        public void ActionFirst_InvalidTargetDoesNotChangePartialSelection()
        {
            var context = CreateContext();
            var unavailableTarget = CreateCombatUnit(
                3,
                new Vector2Int(4, 4),
                basicAttackRange: 1);

            Assert.IsTrue(context.Coordinator.TrySelectAction(
                BattleActionSelection.ForBasicAttack(),
                out var actionRejection));
            Assert.IsNull(actionRejection);

            Assert.IsFalse(context.Coordinator.TrySelectUnitTarget(
                unavailableTarget,
                out var targetRejection));

            Assert.IsNotEmpty(targetRejection);
            Assert.AreEqual(
                BattleActionSelectionState.ActionSelected,
                context.Coordinator.State);
            Assert.AreEqual(
                BattleActionSelectionKind.BasicAttack,
                context.Coordinator.SelectedAction.Kind);
            Assert.IsNull(context.Coordinator.SelectedTarget);
        }

        [Test]
        public void ActionFirst_ValidTargetEntersAwaitingConfirmation()
        {
            var context = CreateContext();

            context.Coordinator.TrySelectAction(
                BattleActionSelection.ForAbility(context.Ability),
                out _);

            var selected = context.Coordinator.TrySelectUnitTarget(
                context.Enemy,
                out var rejectionReason);

            Assert.IsTrue(selected);
            Assert.IsNull(rejectionReason);
            Assert.AreEqual(
                BattleActionSelectionState.AwaitingConfirmation,
                context.Coordinator.State);
            Assert.IsTrue(context.Coordinator.CanConfirm);
        }

        [Test]
        public void TargetFirst_ListsCompatibleActionsAndAcceptsAbility()
        {
            var context = CreateContext();

            Assert.IsTrue(context.Coordinator.TrySelectUnitTarget(
                context.Enemy,
                out _));

            var actions =
                context.Coordinator.GetAvailableActionsForSelectedTarget();
            Assert.AreEqual(2, actions.Count);
            Assert.AreEqual(
                BattleActionSelectionState.AwaitingConfirmation,
                context.Coordinator.State);
            Assert.AreEqual(
                BattleActionSelectionKind.BasicAttack,
                context.Coordinator.SelectedAction.Kind);

            Assert.IsTrue(context.Coordinator.TrySelectAction(
                actions[1],
                out var rejectionReason));

            Assert.IsNull(rejectionReason);
            Assert.AreEqual(
                BattleActionSelectionState.AwaitingConfirmation,
                context.Coordinator.State);
            Assert.AreEqual(
                BattleActionSelectionPath.TargetFirst,
                context.Coordinator.Path);
        }

        [Test]
        public void TargetFirst_ListsOnlyBasicAttackAndWeaponDependentAbilities()
        {
            var weaponAbility = new AbilityDefinition(
                10, "Weapon Strike", 2, 3, 20, 1, TargetRule.EnemyUnit, true);
            var independentAbility = new AbilityDefinition(
                11, "Independent Strike", 1, 1, 20, 1, TargetRule.EnemyUnit);
            var context = CreateContext(weaponAbility, independentAbility);

            Assert.IsTrue(context.Coordinator.TrySelectUnitTarget(
                context.Enemy,
                out _));

            var actions =
                context.Coordinator.GetAvailableActionsForSelectedTarget();

            Assert.AreEqual(2, actions.Count);
            Assert.AreEqual(BattleActionSelectionKind.BasicAttack, actions[0].Kind);
            Assert.AreSame(weaponAbility, actions[1].Ability);
            Assert.AreEqual(
                AbilityTargetRangeSource.BasicWeapon,
                actions[1].RangeSource);
        }

        [Test]
        public void TargetFirst_IndependentAbilityCannotUseSelectedWeaponTarget()
        {
            var independentAbility = new AbilityDefinition(
                10, "Independent Strike", 1, 1, 20, 1, TargetRule.EnemyUnit);
            var context = CreateContext(independentAbility);

            Assert.IsTrue(context.Coordinator.TrySelectUnitTarget(
                context.Enemy,
                out _));

            var selected = context.Coordinator.TrySelectAction(
                BattleActionSelection.ForAbility(independentAbility),
                out var rejectionReason);

            Assert.IsFalse(selected);
            Assert.IsNotEmpty(rejectionReason);
            Assert.AreEqual(
                BattleActionSelectionKind.BasicAttack,
                context.Coordinator.SelectedAction.Kind);
            Assert.AreSame(context.Enemy, context.Coordinator.SelectedTarget.Unit);
        }

        [Test]
        public void TargetFirst_AwaitingConfirmationCanSwitchWeaponAbility()
        {
            var firstAbility = new AbilityDefinition(
                10, "First Strike", 2, 3, 20, 1, TargetRule.EnemyUnit, true);
            var secondAbility = new AbilityDefinition(
                11, "Second Strike", 3, 4, 30, 2, TargetRule.EnemyUnit, true);
            var context = CreateContext(firstAbility, secondAbility);

            Assert.IsTrue(context.Coordinator.TrySelectUnitTarget(
                context.Enemy,
                out _));
            Assert.IsTrue(context.Coordinator.TrySelectAction(
                BattleActionSelection.ForAbility(
                    firstAbility,
                    AbilityTargetRangeSource.BasicWeapon),
                out _));

            var switched = context.Coordinator.TrySelectAction(
                BattleActionSelection.ForAbility(
                    secondAbility,
                    AbilityTargetRangeSource.BasicWeapon),
                out var rejectionReason);

            Assert.IsTrue(switched);
            Assert.IsNull(rejectionReason);
            Assert.AreSame(secondAbility, context.Coordinator.SelectedAction.Ability);
            Assert.AreSame(context.Enemy, context.Coordinator.SelectedTarget.Unit);
            Assert.AreEqual(
                BattleActionSelectionPath.TargetFirst,
                context.Coordinator.Path);
            Assert.IsTrue(context.Coordinator.CanConfirm);
        }

        [Test]
        public void WeaponDependentAbility_UsesWeaponRangeOnlyOnTargetFirstPath()
        {
            var ability = new AbilityDefinition(
                10, "Weapon Strike", 2, 2, 20, 1, TargetRule.EnemyUnit, true);
            var context = CreateContext(ability);

            Assert.IsTrue(context.Coordinator.TrySelectUnitTarget(
                context.Enemy,
                out _));
            var targetFirstActions =
                context.Coordinator.GetAvailableActionsForSelectedTarget();
            Assert.AreEqual(2, targetFirstActions.Count);

            context.Coordinator.Cancel();
            Assert.IsTrue(context.Coordinator.TrySelectAction(
                BattleActionSelection.ForAbility(ability),
                out _));

            Assert.IsFalse(context.Coordinator.TrySelectUnitTarget(
                context.Enemy,
                out var rejectionReason));
            Assert.IsNotEmpty(rejectionReason);
            Assert.AreEqual(
                AbilityTargetRangeSource.Ability,
                context.Coordinator.SelectedAction.RangeSource);
        }

        [Test]
        public void ActionFirst_AwaitingConfirmationRejectsActionSwitch()
        {
            var firstAbility = new AbilityDefinition(
                10, "First Strike", 1, 1, 20, 1, TargetRule.EnemyUnit);
            var secondAbility = new AbilityDefinition(
                11, "Second Strike", 1, 1, 30, 2, TargetRule.EnemyUnit);
            var context = CreateContext(firstAbility, secondAbility);

            Assert.IsTrue(context.Coordinator.TrySelectAction(
                BattleActionSelection.ForAbility(firstAbility),
                out _));
            Assert.IsTrue(context.Coordinator.TrySelectUnitTarget(
                context.Enemy,
                out _));

            var switched = context.Coordinator.TrySelectAction(
                BattleActionSelection.ForAbility(secondAbility),
                out var rejectionReason);

            Assert.IsFalse(switched);
            Assert.IsNotEmpty(rejectionReason);
            Assert.AreSame(firstAbility, context.Coordinator.SelectedAction.Ability);
            Assert.AreEqual(
                BattleActionSelectionPath.ActionFirst,
                context.Coordinator.Path);
        }

        [Test]
        public void ActionFirst_AwaitingConfirmationCanSwitchValidTarget()
        {
            var context = CreateContext(
                additionalEnemyPosition: new Vector2Int(1, 2));

            Assert.IsTrue(context.Coordinator.TrySelectAction(
                BattleActionSelection.ForAbility(context.Ability),
                out _));
            Assert.IsTrue(context.Coordinator.TrySelectUnitTarget(
                context.Enemy,
                out _));

            var switched = context.Coordinator.TrySelectUnitTarget(
                context.AdditionalEnemy,
                out var rejectionReason);

            Assert.IsTrue(switched);
            Assert.IsNull(rejectionReason);
            Assert.AreSame(
                context.AdditionalEnemy,
                context.Coordinator.SelectedTarget.Unit);
            Assert.AreEqual(
                BattleActionSelectionPath.ActionFirst,
                context.Coordinator.Path);
        }

        [Test]
        public void AwaitingConfirmation_InvalidTargetSwitchPreservesSelection()
        {
            var context = CreateContext(
                additionalEnemyPosition: new Vector2Int(4, 4));

            Assert.IsTrue(context.Coordinator.TrySelectAction(
                BattleActionSelection.ForAbility(context.Ability),
                out _));
            Assert.IsTrue(context.Coordinator.TrySelectUnitTarget(
                context.Enemy,
                out _));

            var switched = context.Coordinator.TrySelectUnitTarget(
                context.AdditionalEnemy,
                out var rejectionReason);

            Assert.IsFalse(switched);
            Assert.IsNotEmpty(rejectionReason);
            Assert.AreSame(
                context.Enemy,
                context.Coordinator.SelectedTarget.Unit);
            Assert.AreSame(
                context.Ability,
                context.Coordinator.SelectedAction.Ability);
        }

        [Test]
        public void TargetFirst_DefaultsBasicWeaponSkillAndCanSwitchValidTarget()
        {
            var context = CreateContext(
                additionalEnemyPosition: new Vector2Int(1, 2));

            Assert.IsTrue(context.Coordinator.TrySelectUnitTarget(
                context.Enemy,
                out _));

            Assert.AreEqual(
                BattleActionSelectionKind.BasicAttack,
                context.Coordinator.SelectedAction.Kind);
            Assert.AreEqual(
                BattleActionSelectionPath.TargetFirst,
                context.Coordinator.Path);

            Assert.IsTrue(context.Coordinator.TrySelectUnitTarget(
                context.AdditionalEnemy,
                out var rejectionReason));

            Assert.IsNull(rejectionReason);
            Assert.AreSame(
                context.AdditionalEnemy,
                context.Coordinator.SelectedTarget.Unit);
            Assert.AreEqual(
                BattleActionSelectionKind.BasicAttack,
                context.Coordinator.SelectedAction.Kind);
        }

        [Test]
        public void TargetFirst_IncompatibleActionFailsWithoutClearingTarget()
        {
            var ability = new AbilityDefinition(
                10, "Long Strike", 2, 2, 20, 1, TargetRule.EnemyUnit);
            var context = CreateContext(ability);

            Assert.IsTrue(context.Coordinator.TrySelectUnitTarget(
                context.Enemy,
                out _));

            var selected = context.Coordinator.TrySelectAction(
                BattleActionSelection.ForAbility(ability),
                out var rejectionReason);

            Assert.IsFalse(selected);
            Assert.IsNotEmpty(rejectionReason);
            Assert.AreEqual(
                BattleActionSelectionState.AwaitingConfirmation,
                context.Coordinator.State);
            Assert.AreEqual(
                BattleActionSelectionKind.BasicAttack,
                context.Coordinator.SelectedAction.Kind);
            Assert.AreSame(
                context.Enemy,
                context.Coordinator.SelectedTarget.Unit);
            Assert.AreEqual(
                1,
                context.Coordinator.GetAvailableActionsForSelectedTarget().Count);
        }

        [Test]
        public void Cancel_ReturnsToActionListWithoutChangingBattleState()
        {
            var context = CreateContext();
            var durabilityBefore =
                context.Enemy.MechRuntime.CurrentDurability;
            var actionPointBefore =
                context.Player.PilotRuntime.CurrentActionPoint;

            context.Coordinator.TrySelectUnitTarget(context.Enemy, out _);
            var targetActions =
                context.Coordinator.GetAvailableActionsForSelectedTarget();
            context.Coordinator.TrySelectAction(
                targetActions[1],
                out _);
            context.Coordinator.Cancel();

            Assert.AreEqual(
                BattleActionSelectionState.ActionList,
                context.Coordinator.State);
            Assert.IsFalse(context.Coordinator.IsInputLocked);
            Assert.AreEqual(
                durabilityBefore,
                context.Enemy.MechRuntime.CurrentDurability);
            Assert.AreEqual(
                actionPointBefore,
                context.Player.PilotRuntime.CurrentActionPoint);
        }

        [Test]
        public void ConfirmBasicAttack_ExecutesAndResetsSelection()
        {
            var context = CreateContext();
            var durabilityBefore =
                context.Enemy.MechRuntime.CurrentDurability;

            context.Coordinator.TrySelectAction(
                BattleActionSelection.ForBasicAttack(),
                out _);
            context.Coordinator.TrySelectUnitTarget(context.Enemy, out _);

            var result = context.Coordinator.Confirm();

            Assert.IsTrue(result.IsSuccess);
            Assert.Less(
                context.Enemy.MechRuntime.CurrentDurability,
                durabilityBefore);
            Assert.AreEqual(
                BattleActionSelectionState.ActionList,
                context.Coordinator.State);
            Assert.IsFalse(context.Coordinator.IsInputLocked);
        }

        private static TestContext CreateContext(
            AbilityDefinition ability = null,
            AbilityDefinition additionalAbility = null,
            Vector2Int? enemyPosition = null,
            int basicAttackActionPointCost = 0,
            Vector2Int? additionalEnemyPosition = null)
        {
            var player = CreateCombatUnit(
                1,
                new Vector2Int(1, 1),
                basicAttackRange: 1,
                basicAttackActionPointCost: basicAttackActionPointCost);
            var enemy = CreateCombatUnit(
                2,
                enemyPosition ?? new Vector2Int(2, 1),
                basicAttackRange: 1);
            var additionalEnemy = additionalEnemyPosition.HasValue
                ? CreateCombatUnit(
                    3,
                    additionalEnemyPosition.Value,
                    basicAttackRange: 1)
                : null;
            var session = new BattleSession(
                6,
                6,
                new[] { player },
                additionalEnemy == null
                    ? new[] { enemy }
                    : new[] { enemy, additionalEnemy });
            ability ??= new AbilityDefinition(
                10, "Strike", 1, 2, 20, 1, TargetRule.EnemyUnit, true);
            IReadOnlyList<AbilityDefinition> configuredAbilities =
                additionalAbility == null
                    ? new[] { ability }
                    : new[] { ability, additionalAbility };
            var abilities = new Dictionary<int, IReadOnlyList<AbilityDefinition>>
            {
                [player.UnitId] = configuredAbilities,
            };
            return new TestContext(
                player,
                enemy,
                additionalEnemy,
                ability,
                session,
                new BattleActionSelectionCoordinator(session, abilities));
        }

        private static CombatUnit CreateCombatUnit(
            int unitId,
            Vector2Int position,
            int basicAttackRange,
            int basicAttackActionPointCost = 0)
        {
            var pilot = new PilotData(unitId, $"Pilot {unitId}", 3);
            var mech = new MechData(
                unitId,
                $"Mech {unitId}",
                new BasicAttackData(
                    10,
                    1,
                    basicAttackRange,
                    basicAttackActionPointCost),
                100,
                3);
            return new CombatUnitFactory().Create(
                unitId,
                pilot,
                mech,
                position);
        }

        private sealed class TestContext
        {
            public TestContext(
                CombatUnit player,
                CombatUnit enemy,
                CombatUnit additionalEnemy,
                AbilityDefinition ability,
                BattleSession session,
                BattleActionSelectionCoordinator coordinator)
            {
                Player = player;
                Enemy = enemy;
                AdditionalEnemy = additionalEnemy;
                Ability = ability;
                Session = session;
                Coordinator = coordinator;
            }

            public CombatUnit Player { get; }

            public CombatUnit Enemy { get; }

            public CombatUnit AdditionalEnemy { get; }

            public AbilityDefinition Ability { get; }

            public BattleSession Session { get; }

            public BattleActionSelectionCoordinator Coordinator { get; }
        }
    }
}
