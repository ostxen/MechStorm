using System;
using System.Collections.Generic;

namespace MechStorm.Battle.Statistics
{
    /// <summary>
    /// 单台单位在一场战斗中的只读统计结果。
    /// </summary>
    public sealed class BattleUnitStatistics
    {
        public int UnitId { get; }

        public string PilotName { get; }

        public int KillCount { get; }

        public int DamageDealt { get; }

        public int DamageTaken { get; }

        public BattleUnitStatistics(int unitId, string pilotName, int killCount, int damageDealt, int damageTaken)
        {
            if (unitId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitId), unitId, "Unit ID must be greater than zero.");
            }

            if (string.IsNullOrEmpty(pilotName))
            {
                throw new ArgumentException("Pilot name must be provided.", nameof(pilotName));
            }

            if (killCount < 0 || damageDealt < 0 || damageTaken < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(killCount),
                    $"{nameof(killCount)}, {nameof(damageDealt)} and {nameof(damageTaken)} must not be negative.");
            }

            UnitId = unitId;
            PilotName = pilotName;
            KillCount = killCount;
            DamageDealt = damageDealt;
            DamageTaken = damageTaken;
        }
    }

    /// <summary>
    /// 一场战斗结束后的只读统计快照，按注册表顺序覆盖双方全部单位（含阵亡单位）。
    /// </summary>
    public sealed class BattleStatisticsReport
    {
        public IReadOnlyList<BattleUnitStatistics> Units { get; }

        public BattleStatisticsReport(IReadOnlyList<BattleUnitStatistics> units)
        {
            if (units == null)
            {
                throw new ArgumentNullException(nameof(units));
            }

            Units = units;
        }
    }
}
