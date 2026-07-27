using System;
using MechStorm.Battle.Outcomes;
using MechStorm.Battle.Units;
using NUnit.Framework;

namespace MechStorm.Battle.Tests.Outcomes
{
    /// <summary>
    /// 验证进行中、阵营全灭和非法胜负组合的 Outcome 契约。
    /// </summary>
    public class BattleOutcomeTests
    {
        [Test]
        public void InProgress_HasNoWinnerOrEndReason()
        {
            var outcome = BattleOutcome.InProgress;

            Assert.IsFalse(outcome.IsBattleEnded);
            Assert.IsNull(outcome.WinningFaction);
            Assert.IsNull(outcome.EliminatedFaction);
            Assert.AreEqual(BattleEndReason.None, outcome.EndReason);
        }

        [Test]
        public void FactionEliminated_CreatesCompletedOutcome()
        {
            var outcome = BattleOutcome.FactionEliminated(
                CombatFaction.TeamA, CombatFaction.TeamB);

            Assert.IsTrue(outcome.IsBattleEnded);
            Assert.AreEqual(CombatFaction.TeamA, outcome.WinningFaction);
            Assert.AreEqual(CombatFaction.TeamB, outcome.EliminatedFaction);
            Assert.AreEqual(BattleEndReason.FactionEliminated, outcome.EndReason);
        }

        [Test]
        public void FactionEliminated_WithInvalidFactions_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BattleOutcome.FactionEliminated(
                    CombatFaction.Neutral, CombatFaction.TeamB));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                BattleOutcome.FactionEliminated(
                    CombatFaction.TeamA, CombatFaction.Neutral));
            Assert.Throws<ArgumentException>(() =>
                BattleOutcome.FactionEliminated(
                    CombatFaction.TeamA, CombatFaction.TeamA));
        }
    }
}
