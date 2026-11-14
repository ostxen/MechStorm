using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameConfig;
using GameConfig.Mech;
using GameConfig.Skill;
using GameConfig.Weapon;
using MechStorm.Battle;
using MechStorm.Battle.Data;
using MechStorm.Battle.Units;
using GridPosition = MechStorm.Battle.Foundation.Vector2Int;

namespace MechStorm.Config
{
    /// <summary>
    /// 将配置投影到当前原型规则：躯干耐久代表整机、唯一武器确定伤害、每回合满AP。
    /// 四部位独立结算、驾驶倍率和完整技能机制不属于此适配层。
    /// </summary>
    public sealed class LubanBattleScenarioBuilder
    {
        public BattleScenarioBuildResult Build(Tables tables, int scenarioId)
        {
            var scenario = tables.TbScenario.Get(scenarioId);
            var deployments = tables.TbDeployment.DataList.Where(d => d.ScenarioId == scenarioId)
                .OrderBy(d => d.SpawnOrder).ToList();
            Require(deployments.Count == 4 && deployments.Count(d => d.Faction == GameConfig.Battle.CombatFaction.TeamA) == 2 &&
                deployments.Count(d => d.Faction == GameConfig.Battle.CombatFaction.TeamB) == 2, "当前关卡必须为2v2且无中立单位。");
            Require(scenario.BoardWidth > 0 && scenario.BoardHeight > 0, "棋盘尺寸必须为正数。");
            var unitIds = new HashSet<int>();
            var orders = new HashSet<int>();
            var positions = new HashSet<GridPosition>();
            var units = new List<CombatUnit>();
            var teamA = new List<CombatUnit>();
            var teamB = new List<CombatUnit>();
            var abilities = new Dictionary<int, IReadOnlyList<AbilityDefinition>>();
            foreach (var deployment in deployments)
            {
                var position = new GridPosition(deployment.X, deployment.Y);
                Require(deployment.UnitId > 0 && unitIds.Add(deployment.UnitId), $"部署{deployment.Id}单位ID重复或非法。");
                Require(deployment.SpawnOrder > 0 && orders.Add(deployment.SpawnOrder), $"部署{deployment.Id}出场顺序重复或非法。");
                Require(deployment.X >= 0 && deployment.Y >= 0 && deployment.X < scenario.BoardWidth &&
                    deployment.Y < scenario.BoardHeight && positions.Add(position), $"部署{deployment.Id}位置越界或重复。");
                var loadout = tables.TbMechLoadout.Get(deployment.MechLoadoutId);
                ValidateParts(tables, loadout);
                var pilot = tables.TbPilot.Get(deployment.PilotId);
                Require(pilot.AllowedMechClassIds.Contains(loadout.ClassId) &&
                    pilot.AllowedMechClassIds.Distinct().Count() == pilot.AllowedMechClassIds.Count, $"机师{pilot.Id}驾驶资格非法。");
                Require(pilot.InitialActionPoint == pilot.MaxActionPoint && pilot.ActionPointRecoveryPerTurn == pilot.MaxActionPoint &&
                    pilot.InitialPotentialPoint == 0 && pilot.MaxPotentialPoint == 0, $"机师{pilot.Id}需要未实现的AP/PP资源规则。");
                var weapon = GetWeapon(tables, loadout);
                var basicSkill = tables.TbWeaponDefaultSkill.Get(weapon.Kind).SkillId_Ref;
                Require(basicSkill.Category == SkillCategory.BasicAttack, $"武器{weapon.Id}默认技能不是普攻。");
                var basic = ConvertSkill(basicSkill, weapon, true);
                var mech = new MechData(loadout.Id, loadout.Name,
                    new BasicAttackData(basic.Damage, basic.MinRange, basic.MaxRange, basic.ActionPointCost),
                    tables.TbMechPart.Get(loadout.BodyPartId).MaxDurability, tables.TbMechClass.Get(loadout.ClassId).BaseMoveRange);
                var unit = new CombatUnitFactory().Create(deployment.UnitId,
                    new PilotData(pilot.Id, pilot.Name, pilot.MaxActionPoint), mech, position);
                units.Add(unit);
                (deployment.Faction == GameConfig.Battle.CombatFaction.TeamA ? teamA : teamB).Add(unit);
                abilities.Add(unit.UnitId, BuildAbilities(tables, deployment.Id, pilot.Id, weapon));
            }
            return new BattleScenarioBuildResult(new BattleSession(scenario.BoardWidth, scenario.BoardHeight, teamA, teamB), units, abilities);
        }

        private static void ValidateParts(Tables tables, MechLoadout loadout)
        {
            var ids = new[] { loadout.BodyPartId, loadout.LeftArmPartId, loadout.RightArmPartId, loadout.LegsPartId };
            for (int i = 0; i < ids.Length; i++)
            {
                var part = tables.TbMechPart.Get(ids[i]);
                Require(part.PartType == (PartType)i && part.MechId_Ref.ClassId == loadout.ClassId,
                    $"装配{loadout.Id}部件{part.Id}位置或机型不匹配。");
                Require(part.MaxDurability > 0 && part.Firepower == 0 && part.Evasion == 0,
                    $"部件{part.Id}耐久非法或需要未实现的火力/闪避公式。");
            }
        }

        private static WeaponDefinition GetWeapon(Tables tables, MechLoadout loadout)
        {
            var mounts = tables.TbMechLoadoutWeapon.DataList.Where(w => w.LoadoutId == loadout.Id).ToList();
            Require(mounts.Count == 1, $"装配{loadout.Id}当前仅支持恰好一把武器。");
            var mount = mounts[0];
            var weapon = tables.TbWeapon.Get(mount.WeaponId);
            var allowedMounts = tables.TbMechClass.Get(loadout.ClassId).WeaponMounts;
            Require(allowedMounts.Distinct().Count() == allowedMounts.Count && allowedMounts.Contains(mount.WeaponMount) &&
                (mount.WeaponMount == WeaponMount.LeftHand || mount.WeaponMount == WeaponMount.RightHand) &&
                weapon.MountType == MountType.Hand && weapon.GripType == GripType.OneHanded &&
                weapon.AllowedMechClassIds.Contains(loadout.ClassId), $"装配{loadout.Id}武器{weapon.Id}挂载/机型不匹配。");
            Require(weapon.HitCount == 1 && weapon.AmmoCapacity == -1 && weapon.Accuracy == 0 && weapon.Critical == 0 && weapon.Weight == 0,
                $"武器{weapon.Id}需要未实现的连击/弹药/命中/暴击/重量规则。");
            ValidateRange(weapon.Range, $"武器{weapon.Id}");
            return weapon;
        }

        private static IReadOnlyList<AbilityDefinition> BuildAbilities(Tables tables, int deploymentId, int pilotId, WeaponDefinition weapon)
        {
            var slots = tables.TbUnitSkill.DataList.Where(s => s.DeploymentId == deploymentId).OrderBy(s => s.SlotOrder).ToList();
            Require(slots.Select(s => s.SlotOrder).Distinct().Count() == slots.Count &&
                slots.Select(s => s.SkillId).Distinct().Count() == slots.Count && slots.All(s => s.SlotOrder > 0), $"部署{deploymentId}技能或栏位重复/非法。");
            var result = new List<AbilityDefinition>();
            foreach (var slot in slots)
            {
                Require(tables.TbPilotSkill.DataList.Any(s => s.PilotId == pilotId && s.SkillId == slot.SkillId),
                    $"部署{deploymentId}技能{slot.SkillId}不在机师技能池中。");
                result.Add(ConvertSkill(tables.TbSkill.Get(slot.SkillId), weapon, false));
            }
            return result.AsReadOnly();
        }

        private static AbilityDefinition ConvertSkill(SkillDefinition skill, WeaponDefinition weapon, bool basic)
        {
            var definition = skill.Definition;
            string context = $"技能{skill.Id}";
            Require(definition.Limits.Count == 0 && definition.OngoingModifiers.Count == 0 && definition.Costs.Count <= 1 &&
                definition.Costs.All(c => c.Resource == ResourceKind.ActionPoint && c.Amount > 0), context + "存在未支持的次数/持续修正/资源费用。");
            var range = weapon.Range;
            if (basic)
            {
                Require(definition.Activation is ManualActivation manual && manual.Selection == TargetSelection.EnemyUnitInSourceWeaponRange &&
                    manual.UseConditions.Count == 1 && manual.UseConditions[0] is SourceWeaponUsable usable && usable.WeaponKind == weapon.Kind,
                    context + "默认普攻来源或目标规则不支持。");
            }
            else
            {
                Require(skill.Category == SkillCategory.Active || skill.Category == SkillCategory.Command, context + "不是可转换的主动技能。");
                Require(definition.Activation is FixedRangeManualActivation, context + "仅支持固定菱形射程单体技能。");
                var activation = (FixedRangeManualActivation)definition.Activation;
                Require(activation.AllowFlyingTargets && activation.UseConditions.Count == 1 &&
                    activation.UseConditions[0] is EquippedWeaponUsable usable && usable.WeaponKind == weapon.Kind,
                    context + "存在未支持的目标或武器条件。");
                range = activation.Range;
                // 武器技能仍走目标优先；角色指令行动优先，使用配置自身的射程。
                Require(skill.Category == SkillCategory.Command || (range.Shape == weapon.Range.Shape &&
                    range.MinRange == weapon.Range.MinRange && range.MaxRange == weapon.Range.MaxRange),
                    context + "当前武器技能与唯一武器射程必须一致。");
            }
            ValidateRange(range, context);
            Require(definition.Effects.Count == 1 && definition.Effects[0] is AttackEffect, context + "仅支持单个攻击效果。");
            var effect = (AttackEffect)definition.Effects[0];
            Require(effect.Target == EffectTarget.SelectedTarget && effect.Attacks.Count == 1, context + "仅支持单目标单段攻击。");
            var attack = effect.Attacks[0];
            Require(attack.Classification.InitiativeTag == "Attack.Mode.Active" &&
                attack.Classification.InteractionTag == (basic ? "Attack.Mode.Duel" : "Attack.Mode.NonDuel"), context + "攻击分类不支持。");
            Require(basic ? attack.Weapon is GameConfig.Attack.SourceWeapon :
                attack.Weapon is GameConfig.Attack.SelectedWeapon selected && selected.WeaponKind == weapon.Kind,
                context + "武器来源不支持。");
            long scaledDamage = (long)weapon.AttackPower * attack.DamageRate;
            Require(scaledDamage >= 0 && scaledDamage % 10000 == 0 && scaledDamage / 10000 <= int.MaxValue, context + "伤害不是可精确表示的非负整数。");
            return new AbilityDefinition(skill.Id, skill.Name, range.MinRange, range.MaxRange, (int)(scaledDamage / 10000),
                definition.Costs.Count == 0 ? 0 : definition.Costs[0].Amount, TargetRule.EnemyUnit, !basic,
                skill.Category == SkillCategory.Command ? AbilityCategory.Command : AbilityCategory.Active);
        }

        private static void ValidateRange(WeaponRange range, string context)
        {
            Require(range.Shape == RangeShape.Diamond && range.MinRange >= 1 && range.MaxRange >= range.MinRange,
                context + "仅支持合法菱形射程。");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException("Luban战斗配置：" + message);
        }
    }
}
