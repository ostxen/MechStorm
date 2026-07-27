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

        /// <summary>
        /// 复用的“对局进行中”结果，此时没有胜方和全灭方。
        /// </summary>
        public static BattleOutcome InProgress => _inProgress;

        /// <summary>
        /// 是否已经产生最终胜负。
        /// </summary>
        public bool IsBattleEnded { get; }

        /// <summary>
        /// 对局结束时的胜方；进行中为 null。
        /// </summary>
        public CombatFaction? WinningFaction { get; }

        /// <summary>
        /// 对局结束时被全灭的一方；进行中为 null。
        /// </summary>
        public CombatFaction? EliminatedFaction { get; }

        /// <summary>
        /// 对局结束原因；进行中为 None。
        /// </summary>
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
            // 当前最小对局只有 TeamA 和 TeamB 参与胜负，Neutral 不能成为胜方或全灭方。
            if (faction != CombatFaction.TeamA && faction != CombatFaction.TeamB)
            {
                throw new ArgumentOutOfRangeException(parameterName, faction,
                    "Battle outcome only supports TeamA and TeamB.");
            }
        }
    }
}
