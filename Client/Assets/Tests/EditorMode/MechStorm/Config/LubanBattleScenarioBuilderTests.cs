using System.IO;
using System.Linq;
using GameConfig;
using Luban;
using System.Collections.Generic;
using MechStorm.Battle.Actions;
using MechStorm.Battle.Data;
using MechStorm.Presentation.Interaction;
using MechStorm.Battle.Outcomes;
using MechStorm.Battle.Units;
using Vector2Int = MechStorm.Battle.Foundation.Vector2Int;
using NUnit.Framework;
using UnityEngine;

namespace MechStorm.Config.Tests
{
    public sealed class LubanBattleScenarioBuilderTests
    {
        private static Tables Load()
        {
            return new Tables(name => new ByteBuf(File.ReadAllBytes(
                Path.Combine(Application.dataPath, "AssetRaw/Configs/bytes", name + ".bytes"))));
        }

        [Test]
        public void Build_ProjectsAllFourUnitsAndResolvedSkills()
        {
            var tables = Load();
            var result = new LubanBattleScenarioBuilder().Build(tables, 1);
            Assert.AreEqual(15, result.BattleSession.Grid.Width);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, result.Units.Select(u => u.UnitId));
            CollectionAssert.AreEqual(new[] { 200, 150, 200, 300 }, result.Units.Select(u => u.MechRuntime.CurrentDurability));
            CollectionAssert.AreEqual(new[] { 9, 10, 8, 9 }, result.Units.Select(u => u.PilotRuntime.CurrentActionPoint));
            CollectionAssert.AreEqual(new[] { 4, 3, 3, 4 }, result.Units.Select(u => u.Mech.MoveRange));
            CollectionAssert.AreEqual(new[] { 100, 100, 500, 500 }, result.Units.Select(u => u.Mech.BasicAttack.Damage));
            Assert.AreEqual(new Vector2Int(2, 3), result.Units[0].Position);
            foreach (var unit in result.Units)
            {
                var skill = result.GetAbilitiesForUnit(unit.UnitId).Single(s => s.AbilityId == 9001);
                Assert.AreEqual(9001, skill.AbilityId);
                Assert.AreEqual("强击", skill.Name);
                Assert.AreEqual(3, skill.ActionPointCost);
                Assert.AreEqual(unit.UnitId < 3 ? 150 : 750, skill.Damage);
                Assert.AreEqual(AbilityCategory.Command, skill.Category);
            }
            CollectionAssert.AreEqual(new[] { 9001, 1004 }, result.GetAbilitiesForUnit(1).Select(s => s.AbilityId));
            Assert.IsTrue(result.Units.Skip(1).All(u => result.GetAbilitiesForUnit(u.UnitId).Count == 1));
            Assert.IsEmpty(result.BattleSession.ActionLogs);
            Assert.IsEmpty(result.GetAbilitiesForUnit(999));
        }

        [Test]
        public void DisplayText_ContainsNamesAndRulesWithoutDevelopmentMarkers()
        {
            var tables = Load();
            var text = tables.TbPilot.DataList.Select(p => p.Name)
                .Concat(tables.TbMechLoadout.DataList.Select(m => m.Name))
                .Concat(tables.TbWeapon.DataList.SelectMany(w => new[] { w.Name, w.Description }))
                .Concat(tables.TbSkill.DataList.SelectMany(s => new[] { s.Name, s.Description }));
            foreach (var value in text)
            {
                StringAssert.DoesNotContain("验收", value);
                StringAssert.DoesNotContain("非正式", value);
            }
            CollectionAssert.AreEqual(new[] { "艾达", "洛莎", "索妮亚", "曜" }, tables.TbPilot.DataList.Select(p => p.Name));
            Assert.AreEqual("强击", tables.TbSkill.Get(9001).Name);
            Assert.AreEqual("消耗3AP，使用刀剑对1至3格内的单个敌方单位造成1.5倍武器伤害。", tables.TbSkill.Get(9001).Description);
        }

        [Test]
        public void Build_RepeatedBuildsHaveIndependentRuntimeAndDoNotMutateTables()
        {
            var tables = Load();
            var builder = new LubanBattleScenarioBuilder();
            var first = builder.Build(tables, 1);
            first.Units[0].PilotRuntime.TrySpendActionPoint(3);
            var second = builder.Build(tables, 1);
            Assert.AreNotSame(first.BattleSession, second.BattleSession);
            Assert.AreNotSame(first.Units[0], second.Units[0]);
            Assert.AreEqual(9, second.Units[0].PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(9, tables.TbPilot.Get(9001).InitialActionPoint);
            Assert.AreNotSame(first.GetAbilitiesForUnit(1)[0], second.GetAbilitiesForUnit(1)[0]);
            for (var index = 0; index < first.Units.Count; index++)
            {
                var firstUnit = first.Units[index];
                var secondUnit = second.Units[index];
                Assert.AreNotSame(firstUnit, secondUnit);
                Assert.AreNotSame(firstUnit.Pilot, secondUnit.Pilot);
                Assert.AreNotSame(firstUnit.PilotRuntime, secondUnit.PilotRuntime);
                Assert.AreNotSame(firstUnit.Mech, secondUnit.Mech);
                Assert.AreNotSame(firstUnit.Mech.BasicAttack, secondUnit.Mech.BasicAttack);
                Assert.AreNotSame(firstUnit.MechRuntime, secondUnit.MechRuntime);
                Assert.AreNotSame(first.GetAbilitiesForUnit(firstUnit.UnitId)[0], second.GetAbilitiesForUnit(secondUnit.UnitId)[0]);
            }
        }

        [Test]
        public void BattleLoop_CompletesAndRebuildsFreshScenarioFromSameTables()
        {
            var tables = Load();
            var builder = new LubanBattleScenarioBuilder();
            var battle = builder.Build(tables, 1);
            MoveAndWait(battle, 1, new Vector2Int(5, 4));
            MoveAndWait(battle, 2, new Vector2Int(4, 5));
            MoveAndWait(battle, 3, new Vector2Int(7, 5));
            MoveAndWait(battle, 4, new Vector2Int(7, 6));
            UseAttack(battle, 1, 3, true);
            Move(battle, 2, new Vector2Int(5, 5));
            UseAttack(battle, 2, 3, false);
            EndAction(battle, 4);
            Move(battle, 1, new Vector2Int(6, 4));
            UseAttack(battle, 1, 4, true);
            UseAttack(battle, 2, 4, true);

            var session = battle.BattleSession;
            Assert.IsTrue(session.IsBattleEnded);
            Assert.AreEqual(3, session.CurrentRoundNumber);
            Assert.AreEqual(CombatFaction.TeamA, session.Outcome.WinningFaction);
            Assert.AreEqual(CombatFaction.TeamB, session.Outcome.EliminatedFaction);
            Assert.AreEqual(BattleEndReason.FactionEliminated, session.Outcome.EndReason);
            foreach (var action in new[] { BattleActionType.Move, BattleActionType.Attack, BattleActionType.UseAbility })
                Assert.IsTrue(session.ActionLogs.Any(log => log.ActionType == action));

            var rebuilt = builder.Build(tables, 1);
            Assert.AreNotSame(session, rebuilt.BattleSession);
            Assert.AreSame(BattleOutcome.InProgress, rebuilt.BattleSession.Outcome);
            Assert.IsFalse(rebuilt.BattleSession.IsBattleEnded);
            Assert.AreEqual(1, rebuilt.BattleSession.CurrentRoundNumber);
            Assert.AreEqual(CombatFaction.TeamA, rebuilt.BattleSession.CurrentFaction);
            Assert.AreEqual(1, rebuilt.BattleSession.CurrentCombatUnit.UnitId);
            Assert.IsEmpty(rebuilt.BattleSession.ActionLogs);
            foreach (var deployment in tables.TbDeployment.DataList)
            {
                var unit = rebuilt.Units.Single(u => u.UnitId == deployment.UnitId);
                Assert.AreEqual(new Vector2Int(deployment.X, deployment.Y), unit.Position);
                Assert.AreEqual(unit.Mech.MaxDurability, unit.MechRuntime.CurrentDurability);
                Assert.AreEqual(unit.Pilot.MaxActionPoint, unit.PilotRuntime.CurrentActionPoint);
            }
            CollectionAssert.AreEqual(new[] { 200, 150, 0, 0 }, battle.Units.Select(u => u.MechRuntime.CurrentDurability));
        }

        private static void MoveAndWait(BattleScenarioBuildResult battle, int actor, Vector2Int position)
        {
            Move(battle, actor, position);
            EndAction(battle, actor);
        }

        private static void Move(BattleScenarioBuildResult battle, int actor, Vector2Int position)
        {
            Assert.AreEqual(actor, battle.BattleSession.CurrentCombatUnit.UnitId);
            var result = battle.BattleSession.TryMoveCurrentCombatUnit(position);
            Assert.IsTrue(result.IsSuccess, result.FailureReason.ToString());
        }

        private static void EndAction(BattleScenarioBuildResult battle, int actor)
        {
            Assert.AreEqual(actor, battle.BattleSession.CurrentCombatUnit.UnitId);
            var result = battle.BattleSession.EndCurrentUnitAction();
            Assert.IsTrue(result.IsSuccess, result.FailureReason.ToString());
        }

        private static void UseAttack(BattleScenarioBuildResult battle, int actor, int targetId, bool ability)
        {
            Assert.AreEqual(actor, battle.BattleSession.CurrentCombatUnit.UnitId);
            var target = battle.Units.Single(u => u.UnitId == targetId);
            var result = ability
                ? battle.BattleSession.TryUseAbility(battle.GetAbilitiesForUnit(actor).Single(s => s.AbilityId == 9001), target)
                : battle.BattleSession.AttackTargetCombatUnit(target);
            Assert.IsTrue(result.IsSuccess, result.FailureReason.ToString());
        }

        [Test]
        public void Starfall_RealConfigurationUsesOwnFourCellRangeAndEndsActorAfterThreeAP()
        {
            var battle = new LubanBattleScenarioBuilder().Build(Load(), 1);
            var session = battle.BattleSession;
            var ability = battle.GetAbilitiesForUnit(1).Single(s => s.AbilityId == 1004);
            Assert.AreEqual("星坠闪", ability.Name);
            Assert.AreEqual(AbilityCategory.Command, ability.Category);
            Assert.IsTrue(ability.RequiresBasicWeapon);
            Assert.AreEqual(1, ability.MinRange);
            Assert.AreEqual(4, ability.MaxRange);
            Assert.AreEqual(3, ability.ActionPointCost);
            Assert.AreEqual(120, ability.Damage);
            MoveAndWait(battle, 1, new Vector2Int(5, 4));
            EndAction(battle, 2);
            MoveAndWait(battle, 3, new Vector2Int(7, 6));
            EndAction(battle, 4);
            var actor = battle.Units[0];
            var target = battle.Units[2];
            var coordinator = new BattleActionSelectionCoordinator(session, battle.AbilitiesByUnitId);
            Assert.IsEmpty(session.GetCurrentCombatUnitBasicAttackTargets());
            Assert.Contains(target, session.GetCurrentCombatUnitAbilityTargets(ability).ToArray());
            int ap = actor.PilotRuntime.CurrentActionPoint, hp = target.MechRuntime.CurrentDurability, logs = session.ActionLogs.Count;
            Assert.IsFalse(coordinator.TrySelectUnitTarget(target, out _));
            Assert.IsTrue(coordinator.TrySelectAction(BattleActionSelection.ForAbility(ability), out _));
            Assert.IsFalse(coordinator.TrySelectUnitTarget(actor, out _));
            Assert.IsTrue(coordinator.TrySelectUnitTarget(target, out _));
            coordinator.Cancel();
            Assert.AreEqual(ap, actor.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(hp, target.MechRuntime.CurrentDurability);
            Assert.AreEqual(logs, session.ActionLogs.Count);
            Assert.IsTrue(coordinator.TrySelectAction(BattleActionSelection.ForAbility(ability), out _));
            Assert.IsTrue(coordinator.TrySelectUnitTarget(target, out _));
            Assert.IsTrue(coordinator.Confirm().IsSuccess);
            Assert.AreEqual(ap - 3, actor.PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(hp - 120, target.MechRuntime.CurrentDurability);
            CollectionAssert.AreEqual(new[] { BattleActionType.UseAbility, BattleActionType.EndUnitAction },
                session.ActionLogs.Skip(logs).Select(log => log.ActionType));
            Assert.AreEqual(2, session.CurrentCombatUnit.UnitId);
        }

        [Test]
        public void Build_RejectsDuplicateDeployment()
        {
            var tables = Load();
            ((IList<GameConfig.Battle.Deployment>)tables.TbDeployment.DataList)[1] = tables.TbDeployment.DataList[0];
            Assert.Throws<InvalidDataException>(() => new LubanBattleScenarioBuilder().Build(tables, 1));
        }

        [Test]
        public void Build_RejectsMultipleWeapons()
        {
            var tables = Load();
            ((IList<GameConfig.Mech.MechLoadoutWeapon>)tables.TbMechLoadoutWeapon.DataList).Add(tables.TbMechLoadoutWeapon.DataList[0]);
            StringAssert.Contains("一把武器", Assert.Throws<InvalidDataException>(
                () => new LubanBattleScenarioBuilder().Build(tables, 1)).Message);
        }

        [Test]
        public void Build_RejectsSkillMissingFromPilotPool()
        {
            var tables = Load();
            ((IList<GameConfig.Pilot.PilotSkill>)tables.TbPilotSkill.DataList).Clear();
            StringAssert.Contains("技能池", Assert.Throws<InvalidDataException>(
                () => new LubanBattleScenarioBuilder().Build(tables, 1)).Message);
        }

        [Test]
        public void Build_RejectsUnsupportedEffectRatherThanDroppingIt()
        {
            var tables = Load();
            tables.TbSkill.Get(9001).Definition.Effects.Add(tables.TbSkill.Get(1002).Definition.Effects[0]);
            StringAssert.Contains("单个攻击效果", Assert.Throws<InvalidDataException>(
                () => new LubanBattleScenarioBuilder().Build(tables, 1)).Message);
        }

        [Test]
        public void Build_RejectsUnsupportedConditionsRatherThanDroppingThem()
        {
            var tables = Load();
            var activation = (GameConfig.Skill.FixedRangeManualActivation)tables.TbSkill.Get(9001).Definition.Activation;
            activation.UseConditions.Clear();
            StringAssert.Contains("武器条件", Assert.Throws<InvalidDataException>(
                () => new LubanBattleScenarioBuilder().Build(tables, 1)).Message);
        }

        [Test]
        public void Build_RejectsMissingFaction()
        {
            var tables = Load();
            ((IList<GameConfig.Battle.Deployment>)tables.TbDeployment.DataList).RemoveAt(3);
            StringAssert.Contains("2v2", Assert.Throws<InvalidDataException>(
                () => new LubanBattleScenarioBuilder().Build(tables, 1)).Message);
        }

        [TestCase(15, 4)]
        [TestCase(2, 3)]
        public void Build_RejectsOutsideOrOverlappingPosition(int x, int y)
        {
            var tables = Load();
            var buffer = new ByteBuf();
            foreach (int value in new[] { 9002, 1, 2, 2, 0, x, y, 9002, 9002 }) buffer.WriteInt(value);
            ((IList<GameConfig.Battle.Deployment>)tables.TbDeployment.DataList)[1] = new GameConfig.Battle.Deployment(buffer);
            StringAssert.Contains("位置越界或重复", Assert.Throws<InvalidDataException>(
                () => new LubanBattleScenarioBuilder().Build(tables, 1)).Message);
        }

        [Test]
        public void Build_MissingPilotFailsWithoutPublishingPartialScenario()
        {
            var tables = Load();
            ((IDictionary<int, GameConfig.Pilot.PilotDefinition>)tables.TbPilot.DataMap).Remove(9002);
            BattleScenarioBuildResult result = null;
            Assert.Throws<KeyNotFoundException>(() => result = new LubanBattleScenarioBuilder().Build(tables, 1));
            Assert.IsNull(result);
        }

        [Test]
        public void BattleActions_DoNotChangeConfigurationAndRebuildRestoresDamageAndActionPoints()
        {
            var tables = Load();
            var builder = new LubanBattleScenarioBuilder();
            var battle = builder.Build(tables, 1);
            battle.BattleSession.TryMoveCurrentCombatUnit(new Vector2Int(5, 4));
            battle.BattleSession.EndCurrentUnitAction();
            battle.BattleSession.TryMoveCurrentCombatUnit(new Vector2Int(4, 5));
            battle.BattleSession.EndCurrentUnitAction();
            battle.BattleSession.TryMoveCurrentCombatUnit(new Vector2Int(6, 5));
            battle.BattleSession.TryUseAbility(battle.GetAbilitiesForUnit(3).Single(), battle.Units[0]);
            Assert.AreEqual(0, battle.Units[0].MechRuntime.CurrentDurability);
            Assert.AreEqual(5, battle.Units[2].PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(200, tables.TbMechPart.Get(90010).MaxDurability);
            Assert.AreEqual(8, tables.TbPilot.Get(9003).InitialActionPoint);
            var rebuilt = builder.Build(tables, 1);
            Assert.AreEqual(200, rebuilt.Units[0].MechRuntime.CurrentDurability);
            Assert.AreEqual(8, rebuilt.Units[2].PilotRuntime.CurrentActionPoint);
            Assert.AreEqual(new Vector2Int(2, 3), rebuilt.Units[0].Position);
            Assert.IsEmpty(rebuilt.BattleSession.ActionLogs);
        }
    }
}
