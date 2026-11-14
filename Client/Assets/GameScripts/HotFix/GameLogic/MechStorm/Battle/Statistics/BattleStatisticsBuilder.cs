using System;
using System.Collections.Generic;
using MechStorm.Battle.Actions;
using MechStorm.Battle.Units;

namespace MechStorm.Battle.Statistics
{
    /// <summary>
    /// 从 ActionLog 聚合实际造成伤害、承受伤害与击破数的只读统计构建器。
    /// </summary>
    public static class BattleStatisticsBuilder
    {
        /// <summary>
        /// 按单位注册表顺序构建统计快照；不修改战斗状态，可在对局任意时刻调用。
        /// </summary>
        public static BattleStatisticsReport Build(BattleSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            // 先为全部注册单位初始化累加器，日志中的行动者与目标都来自同一注册表。
            var killCounts = new Dictionary<int, int>();
            var damageDealtByUnit = new Dictionary<int, int>();
            var damageTakenByUnit = new Dictionary<int, int>();
            foreach (var unit in session.CombatUnits)
            {
                killCounts.Add(unit.UnitId, 0);
                damageDealtByUnit.Add(unit.UnitId, 0);
                damageTakenByUnit.Add(unit.UnitId, 0);
            }

            foreach (var log in session.ActionLogs)
            {
                if (!IsDamageAction(log))
                {
                    continue;
                }

                // 耐久在运行时已按 0 截断，前后差即为实际伤害，过量伤害不会计入。
                int damage = log.DurabilityBefore.Value - log.DurabilityAfter.Value;
                damageDealtByUnit[log.ActorUnitId] += damage;
                damageTakenByUnit[log.TargetUnitId.Value] += damage;
                if (log.DurabilityAfter.Value == 0)
                {
                    killCounts[log.ActorUnitId] += 1;
                }
            }

            var units = new List<BattleUnitStatistics>(session.CombatUnits.Count);
            foreach (var unit in session.CombatUnits)
            {
                units.Add(new BattleUnitStatistics(
                    unit.UnitId,
                    unit.Pilot.Name,
                    killCounts[unit.UnitId],
                    damageDealtByUnit[unit.UnitId],
                    damageTakenByUnit[unit.UnitId]));
            }

            return new BattleStatisticsReport(units);
        }

        private static bool IsDamageAction(BattleActionLog log)
        {
            // 只有成功的普攻与技能记录了目标耐久变化；失败操作与移动/回合日志不参与统计。
            return log.IsSuccess &&
                (log.ActionType == BattleActionType.Attack || log.ActionType == BattleActionType.UseAbility);
        }
    }
}
