using System;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    public class MatchTests
    {
        private static DuelResult Win(Side winner, double left, double right) =>
            new DuelResult(DuelOutcome.FasterStrike, winner, left, right);

        [Test]
        public void First_to_the_wins_needed_takes_the_match()
        {
            var match = new Match(3);
            match.Record(Win(Side.Left, 0.2, 0.3));
            match.Record(Win(Side.Right, 0.4, 0.3));
            match.Record(Win(Side.Left, 0.2, 0.3));
            Assert.That(match.IsOver, Is.False);

            match.Record(Win(Side.Left, 0.25, 0.3));
            Assert.That(match.IsOver, Is.True);
            Assert.That(match.Winner, Is.EqualTo(Side.Left));
            Assert.That(match.WinsOf(Side.Left), Is.EqualTo(3));
            Assert.That(match.WinsOf(Side.Right), Is.EqualTo(1));
            Assert.That(match.Rounds, Has.Count.EqualTo(4));
        }

        [Test]
        public void Wins_needed_is_configurable()
        {
            var match = new Match(1);
            match.Record(Win(Side.Right, 0.4, 0.3));
            Assert.That(match.Winner, Is.EqualTo(Side.Right));
        }

        [Test]
        public void Ties_and_no_strike_rounds_award_nobody()
        {
            var match = new Match(1);
            match.Record(new DuelResult(DuelOutcome.Tie, null, 0.2, 0.2));
            match.Record(new DuelResult(DuelOutcome.NoStrike, null, null, null));

            Assert.That(match.WinsOf(Side.Left), Is.Zero);
            Assert.That(match.WinsOf(Side.Right), Is.Zero);
            Assert.That(match.IsOver, Is.False);
            Assert.That(match.Winner, Is.Null);
        }

        [Test]
        public void Early_strike_awards_the_round_to_the_other_side()
        {
            var match = new Match(3);
            match.Record(new DuelResult(DuelOutcome.EarlyStrike, Side.Right, -0.5, null));
            Assert.That(match.WinsOf(Side.Right), Is.EqualTo(1));
        }

        [Test]
        public void Best_reaction_is_the_smallest_non_negative_one()
        {
            var match = new Match(3);
            match.Record(Win(Side.Left, 0.30, 0.35));
            match.Record(new DuelResult(DuelOutcome.EarlyStrike, Side.Right, -0.8, null));
            match.Record(Win(Side.Left, 0.22, 0.40));
            match.Record(Win(Side.Right, 0.50, 0.33));

            Assert.That(match.BestReactionOf(Side.Left), Is.EqualTo(0.22).Within(1e-9));
            Assert.That(match.BestReactionOf(Side.Right), Is.EqualTo(0.33).Within(1e-9));
        }

        [Test]
        public void Best_reaction_is_null_before_any_valid_strike()
        {
            var match = new Match(3);
            match.Record(new DuelResult(DuelOutcome.EarlyStrike, Side.Right, -0.8, null));
            Assert.That(match.BestReactionOf(Side.Left), Is.Null);
            Assert.That(match.BestReactionOf(Side.Right), Is.Null);
        }

        [Test]
        public void Recording_after_the_match_is_over_throws()
        {
            var match = new Match(1);
            match.Record(Win(Side.Left, 0.2, 0.3));
            Assert.Throws<InvalidOperationException>(() => match.Record(Win(Side.Left, 0.2, 0.3)));
        }

        [Test]
        public void Rounds_and_the_match_win_are_announced()
        {
            var match = new Match(1);
            DuelResult recorded = null;
            Side? won = null;
            match.RoundRecorded += r => recorded = r;
            match.Won += s => won = s;

            var result = Win(Side.Right, 0.4, 0.3);
            match.Record(result);

            Assert.That(recorded, Is.SameAs(result));
            Assert.That(won, Is.EqualTo(Side.Right));
        }

        [Test]
        public void Wins_needed_below_one_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Match(0));
        }
    }
}
