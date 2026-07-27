using System;
using MechStorm.Battle.Units;

namespace MechStorm.Battle.Outcomes
{
    /// <summary>
    /// 记录当前对局是否结束，以及全灭规则产生的胜负结果。
    /// </summary>
    public sealed class BattleOutcome
    {
        private static readonly BattleOutcome _inProgress =
            new BattleOutcome(false, null, null, BattleEndReason.None);

        public static BattleOutcome InProgress => _inProgress;

        public bool IsBattleEnded { get; }

        public CombatFaction? WinningFaction { get; }

        public CombatFaction? EliminatedFaction { get; }

        public BattleEndReason EndReason { get; }

        private BattleOutcome(bool isBattleEnded, CombatFaction? winningFaction,
            CombatFaction? eliminatedFaction, BattleEndReason endReason)
        {
            IsBattleEnded = isBattleEnded;
            WinningFaction = winningFaction;
            EliminatedFaction = eliminatedFaction;
            EndReason = endReason;
        }

        /// <summary>
        /// 创建一个由参战阵营全灭产生的已结束结果。
        /// </summary>
        public static BattleOutcome FactionEliminated(
            CombatFaction winningFaction, CombatFaction eliminatedFaction)
        {
            ValidateParticipatingFaction(winningFaction, nameof(winningFaction));
            ValidateParticipatingFaction(eliminatedFaction, nameof(eliminatedFaction));
            if (winningFaction == eliminatedFaction)
            {
                throw new ArgumentException(
                    "Winning and eliminated factions must be different.",
                    nameof(eliminatedFaction));
            }

            return new BattleOutcome(true, winningFaction, eliminatedFaction,
                BattleEndReason.FactionEliminated);
        }

        private static void ValidateParticipatingFaction(CombatFaction faction, string parameterName)
        {
            if (faction != CombatFaction.TeamA && faction != CombatFaction.TeamB)
            {
                throw new ArgumentOutOfRangeException(parameterName, faction,
                    "Battle outcome only supports TeamA and TeamB.");
            }
        }
    }
}
