using System;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // obj_fade_gamescreen's round transition (UQD-T42).
    public class RoundFadeTests
    {
        [Test]
        public void The_aftermath_plays_alone_for_aftermath_time_three_seconds_at_60_a_second()
        {
            Assert.That(RoundFade.AftermathTicks, Is.EqualTo(180));
            for (var tick = 0; tick < 180; tick++)
            {
                Assert.That(RoundFade.StageAt(tick), Is.EqualTo(RoundFadeStage.Aftermath));
                Assert.That(RoundFade.AlphaAt(tick), Is.Zero);
                Assert.That(RoundFade.HasReset(tick), Is.False);
            }
            Assert.That(RoundFade.AlphaAt(-1), Is.Zero, "nothing before the cut");
        }

        [Test]
        public void The_image_fades_in_at_fadespeed_0_005_a_step_and_the_battle_resets_once_it_is_full()
        {
            Assert.That(RoundFade.FadeSpeed, Is.EqualTo(0.005));
            Assert.That(RoundFade.AlphaAt(180), Is.EqualTo(0.005).Within(1e-12));
            Assert.That(RoundFade.AlphaAt(181), Is.EqualTo(0.010).Within(1e-12));
            for (var tick = 181; tick < RoundFade.ResetTick; tick++)
            {
                Assert.That(RoundFade.StageAt(tick), Is.EqualTo(RoundFadeStage.FadingIn));
                Assert.That(RoundFade.AlphaAt(tick) - RoundFade.AlphaAt(tick - 1), Is.EqualTo(0.005).Within(1e-12));
                Assert.That(RoundFade.HasReset(tick), Is.False, "nothing is reset while the image is still coming up");
            }
            Assert.That(RoundFade.ResetTick, Is.EqualTo(380));
            Assert.That(RoundFade.AlphaAt(379), Is.EqualTo(1).Within(1e-12));
            Assert.That(RoundFade.AlphaAt(380), Is.EqualTo(1).Within(1e-12), "the reset happens under a full image");
            Assert.That(RoundFade.HasReset(380), Is.True);
        }

        [Test]
        public void The_image_fades_out_over_the_startup_and_is_gone_200_steps_after_the_reset()
        {
            for (var tick = RoundFade.ResetTick + 1; tick < RoundFade.OffTick; tick++)
            {
                Assert.That(RoundFade.StageAt(tick), Is.EqualTo(RoundFadeStage.FadingOut));
                Assert.That(RoundFade.AlphaAt(tick - 1) - RoundFade.AlphaAt(tick), Is.EqualTo(0.005).Within(1e-12));
                Assert.That(RoundFade.HasReset(tick), Is.True);
            }
            Assert.That(RoundFade.OffTick, Is.EqualTo(580));
            Assert.That(RoundFade.AlphaAt(579), Is.EqualTo(0.005).Within(1e-12));
            Assert.That(RoundFade.StageAt(580), Is.EqualTo(RoundFadeStage.Off));
            Assert.That(RoundFade.AlphaAt(580), Is.Zero);
            Assert.That(RoundFade.AlphaAt(10000), Is.Zero);
        }

        [Test]
        public void Steps_are_counted_off_the_clock_at_60_a_second()
        {
            Assert.That(RoundFade.TickAt(0), Is.EqualTo(0));
            Assert.That(RoundFade.TickAt(1.0 / 60 - 1e-9), Is.EqualTo(0));
            Assert.That(RoundFade.TickAt(3.0), Is.EqualTo(180));
            Assert.That(RoundFade.TickAt(380.0 / 60), Is.EqualTo(380));
            Assert.That(RoundFade.TickAt(-0.01), Is.LessThan(0));
            Assert.That(RoundFade.TickAt(double.NaN), Is.LessThan(0));
        }

        [Test]
        public void Each_transition_draws_one_of_the_14_loading_images_each_equally_likely()
        {
            var random = new Random(42);
            var counts = new int[RoundFade.Images];
            for (var i = 0; i < 14000; i++)
                counts[RoundFade.Pick(random)]++;
            Assert.That(RoundFade.Images, Is.EqualTo(14));
            Assert.That(counts.All(c => c > 800 && c < 1200), string.Join(", ", counts));
            Assert.Throws<ArgumentNullException>(() => RoundFade.Pick(null));
        }

        [Test]
        public void A_round_ends_at_its_first_strike_its_early_strike_or_its_timeout()
        {
            var settings = TestTuning.Profile.Duel.ToSettings();

            var reaction = new Duel(settings, new Random(1));
            reaction.Begin(0);
            Assert.That(reaction.EndedAt, Is.Null, "not ended while it runs");
            var cue = reaction.CueAt;
            reaction.Tick(cue);
            reaction.Strike(Side.Right, cue + 0.30);
            reaction.Strike(Side.Left, cue + 0.20);
            Assert.That(reaction.Phase, Is.EqualTo(DuelPhase.Resolved));
            Assert.That(reaction.EndedAt, Is.EqualTo(cue + 0.20).Within(1e-9), "the earlier strike, though it was delivered later");

            var settled = new Duel(settings, new Random(1));
            settled.Begin(0);
            settled.Tick(settled.CueAt);
            settled.Strike(Side.Left, settled.CueAt + 0.25);
            settled.Tick(settled.CueAt + 0.25 + settings.SettleSeconds + 0.01);
            Assert.That(settled.Phase, Is.EqualTo(DuelPhase.Resolved));
            Assert.That(settled.EndedAt, Is.EqualTo(settled.CueAt + 0.25).Within(1e-9), "the strike, not the settle window's close");

            var early = new Duel(settings, new Random(1));
            early.Begin(0);
            early.Tick(settings.CountdownSeconds);
            early.Strike(Side.Left, settings.CountdownSeconds + 0.5);
            Assert.That(early.Result.Outcome, Is.EqualTo(DuelOutcome.EarlyStrike));
            Assert.That(early.EndedAt, Is.EqualTo(settings.CountdownSeconds + 0.5).Within(1e-9));

            var none = new Duel(settings, new Random(1));
            none.Begin(0);
            none.Tick(none.CueAt);
            none.Tick(none.CueAt + settings.StrikeTimeoutSeconds + 1);
            Assert.That(none.Result.Outcome, Is.EqualTo(DuelOutcome.NoStrike));
            Assert.That(none.EndedAt, Is.EqualTo(none.CueAt + settings.StrikeTimeoutSeconds).Within(1e-9));
        }
    }
}
