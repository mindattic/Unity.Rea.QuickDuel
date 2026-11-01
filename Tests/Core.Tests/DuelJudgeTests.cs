using System;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    public class DuelJudgeTests
    {
        [Test]
        public void Faster_reported_reaction_wins()
        {
            var result = DuelJudge.Judge(0.31, 0.24, TestTuning.Profile.Judge.HumanFloorSeconds);
            Assert.That(result.Outcome, Is.EqualTo(DuelOutcome.FasterStrike));
            Assert.That(result.Winner, Is.EqualTo(Side.Right));
            Assert.That(result.LeftReaction, Is.EqualTo(0.31));
            Assert.That(result.RightReaction, Is.EqualTo(0.24));
        }

        [Test]
        public void Lone_reported_strike_wins()
        {
            var result = DuelJudge.Judge(0.4, null, TestTuning.Profile.Judge.HumanFloorSeconds);
            Assert.That(result.Outcome, Is.EqualTo(DuelOutcome.FasterStrike));
            Assert.That(result.Winner, Is.EqualTo(Side.Left));
        }

        [Test]
        public void Exactly_equal_reactions_tie()
        {
            var result = DuelJudge.Judge(0.25, 0.25, TestTuning.Profile.Judge.HumanFloorSeconds);
            Assert.That(result.Outcome, Is.EqualTo(DuelOutcome.Tie));
            Assert.That(result.Winner, Is.Null);
        }

        [Test]
        public void Both_null_is_no_strike()
        {
            var result = DuelJudge.Judge(null, null, TestTuning.Profile.Judge.HumanFloorSeconds);
            Assert.That(result.Outcome, Is.EqualTo(DuelOutcome.NoStrike));
            Assert.That(result.Winner, Is.Null);
        }

        [Test]
        public void Early_strike_loses_even_against_no_strike()
        {
            var result = DuelJudge.Judge(null, -0.4, TestTuning.Profile.Judge.HumanFloorSeconds);
            Assert.That(result.Outcome, Is.EqualTo(DuelOutcome.EarlyStrike));
            Assert.That(result.Winner, Is.EqualTo(Side.Left));
        }

        [Test]
        public void Early_strike_loses_to_a_slow_strike()
        {
            var result = DuelJudge.Judge(-0.01, 0.9, TestTuning.Profile.Judge.HumanFloorSeconds);
            Assert.That(result.Outcome, Is.EqualTo(DuelOutcome.EarlyStrike));
            Assert.That(result.Winner, Is.EqualTo(Side.Right));
        }

        [Test]
        public void Both_early_the_earlier_striker_loses_as_in_a_local_duel()
        {
            var result = DuelJudge.Judge(-0.2, -1.5, TestTuning.Profile.Judge.HumanFloorSeconds);
            Assert.That(result.Outcome, Is.EqualTo(DuelOutcome.EarlyStrike));
            Assert.That(result.Winner, Is.EqualTo(Side.Left));
        }

        [Test]
        public void Both_early_at_the_same_instant_is_a_double_fault_with_no_winner()
        {
            var result = DuelJudge.Judge(-0.5, -0.5, TestTuning.Profile.Judge.HumanFloorSeconds);
            Assert.That(result.Outcome, Is.EqualTo(DuelOutcome.EarlyStrike));
            Assert.That(result.Winner, Is.Null);
        }

        [Test]
        public void Reaction_below_the_human_floor_forfeits()
        {
            var result = DuelJudge.Judge(0.05, 0.6, TestTuning.Profile.Judge.HumanFloorSeconds);
            Assert.That(result.Outcome, Is.EqualTo(DuelOutcome.Forfeit));
            Assert.That(result.Winner, Is.EqualTo(Side.Right));
        }

        [Test]
        public void Reaction_at_the_human_floor_counts()
        {
            var result = DuelJudge.Judge(0.6, TestTuning.Profile.Judge.HumanFloorSeconds, TestTuning.Profile.Judge.HumanFloorSeconds);
            Assert.That(result.Outcome, Is.EqualTo(DuelOutcome.FasterStrike));
            Assert.That(result.Winner, Is.EqualTo(Side.Right));
        }

        [Test]
        public void Suspicious_reaction_forfeits_ahead_of_the_other_side_striking_early()
        {
            var result = DuelJudge.Judge(-0.3, 0.0, TestTuning.Profile.Judge.HumanFloorSeconds);
            Assert.That(result.Outcome, Is.EqualTo(DuelOutcome.Forfeit));
            Assert.That(result.Winner, Is.EqualTo(Side.Left));
        }

        [Test]
        public void Both_suspicious_forfeit_with_no_winner()
        {
            var result = DuelJudge.Judge(0.01, 0.02, TestTuning.Profile.Judge.HumanFloorSeconds);
            Assert.That(result.Outcome, Is.EqualTo(DuelOutcome.Forfeit));
            Assert.That(result.Winner, Is.Null);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Non_finite_reaction_forfeits(double reported)
        {
            var result = DuelJudge.Judge(reported, 0.3, TestTuning.Profile.Judge.HumanFloorSeconds);
            Assert.That(result.Outcome, Is.EqualTo(DuelOutcome.Forfeit));
            Assert.That(result.Winner, Is.EqualTo(Side.Right));
        }

        [Test]
        public void Human_floor_is_configurable()
        {
            Assert.That(DuelJudge.Judge(0.15, 0.3, 0.2).Outcome, Is.EqualTo(DuelOutcome.Forfeit));
            Assert.That(DuelJudge.Judge(0.05, 0.3, 0).Winner, Is.EqualTo(Side.Left));
        }

        [Test]
        public void Negative_human_floor_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DuelJudge.Judge(0.2, 0.3, -0.1));
        }
    }
}
