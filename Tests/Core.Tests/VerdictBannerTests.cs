using System;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    public class VerdictBannerTests
    {
        [Test]
        public void Wins_is_drawn_on_the_losers_side()
        {
            Assert.That(VerdictBanner.BannerSide(Side.Left), Is.EqualTo(Side.Right));
            Assert.That(VerdictBanner.BannerSide(Side.Right), Is.EqualTo(Side.Left));
            Assert.That(VerdictBanner.Wins, Is.EqualTo("Wins!"));
        }

        [Test]
        public void The_banner_sits_where_obj_gameclock_drew_it()
        {
            // room_height/2 - 80 (name), - 60 ("Wins!", "Premature Strike!"), - 16 (the number).
            Assert.That(VerdictBanner.BannerAboveNumber, Is.EqualTo(44));
            Assert.That(VerdictBanner.NameAboveBanner, Is.EqualTo(20));
        }

        [Test]
        public void The_winners_number_slides_one_pixel_a_60_fps_step_away_from_the_loser()
        {
            Assert.That(VerdictBanner.Slide(Side.Left, 1.0), Is.EqualTo(-60).Within(1e-9), "left won: aftermathX -= 1");
            Assert.That(VerdictBanner.Slide(Side.Right, 1.0), Is.EqualTo(60).Within(1e-9), "right won: aftermathX += 1");
            Assert.That(VerdictBanner.Slide(Side.Right, 10 / 60.0), Is.EqualTo(10).Within(1e-9));
            Assert.That(VerdictBanner.Slide(Side.Right, 0), Is.EqualTo(0));
            Assert.That(VerdictBanner.Slide(Side.Left, -1), Is.EqualTo(0));
            Assert.That(VerdictBanner.Slide(Side.Left, double.NaN), Is.EqualTo(0));
        }

        [Test]
        public void Only_a_round_won_by_reaction_slides_and_shows_the_winner()
        {
            var won = new DuelResult(DuelOutcome.FasterStrike, Side.Right, 0.3, 0.2);
            Assert.That(VerdictBanner.Slide(won, 5.2, 5.7), Is.EqualTo(30).Within(1e-9), "timed from the first strike");
            Assert.That(VerdictBanner.ShowsWinner(won), Is.True);
            Assert.That(VerdictBanner.ShowsPremature(won), Is.False);

            var early = new DuelResult(DuelOutcome.EarlyStrike, Side.Left, null, -0.4);
            Assert.That(VerdictBanner.Slide(early, 5.2, 6), Is.EqualTo(0));
            Assert.That(VerdictBanner.ShowsWinner(early), Is.False);
            Assert.That(VerdictBanner.ShowsPremature(early), Is.True);

            var tie = new DuelResult(DuelOutcome.Tie, null, 0.2, 0.2);
            Assert.That(VerdictBanner.Slide(tie, 5.2, 6), Is.EqualTo(0));
            Assert.That(VerdictBanner.ShowsWinner(tie), Is.False);

            Assert.That(VerdictBanner.Slide(null, 5.2, 6), Is.EqualTo(0));
            Assert.That(VerdictBanner.Slide(won, null, 6), Is.EqualTo(0));
            Assert.That(VerdictBanner.ShowsWinner(null), Is.False);
            Assert.That(VerdictBanner.ShowsPremature(null), Is.False);
        }

        [Test]
        public void A_duel_reports_its_first_strike()
        {
            var duel = new Duel(TestTuning.Profile.Duel.ToSettings(), new Random(7));
            duel.Begin(0);
            var cue = duel.CueAt;
            duel.Tick(cue);
            Assert.That(duel.FirstStrikeAt, Is.Null);
            duel.Strike(Side.Left, cue + 0.3);
            duel.Strike(Side.Right, cue + 0.2);
            Assert.That(duel.FirstStrikeAt, Is.EqualTo(cue + 0.2));
            Assert.That(VerdictBanner.Slide(duel.Result, duel.FirstStrikeAt, cue + 1.2), Is.EqualTo(60).Within(1e-9));
        }
    }
}
