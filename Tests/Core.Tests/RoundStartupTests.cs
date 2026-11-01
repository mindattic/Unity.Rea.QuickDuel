using System;
using NUnit.Framework;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The startup before each round (UQD-T66): gameclock_startup's startup_time, counted at once for the first round
    // (juststarted) and from the loading image's end for every later one; a click skips it while it runs.
    public class RoundStartupTests
    {
        [Test]
        public void The_first_round_begins_startup_time_after_the_scene_starts()
        {
            Assert.That(RoundStartup.BeginsAt(10, null, 3), Is.EqualTo(13));
        }

        [Test]
        public void A_later_round_begins_startup_time_after_the_loading_image_is_gone()
        {
            var off = 20 + (double)RoundFade.OffTick / RoundFade.TicksPerSecond;
            Assert.That(RoundStartup.BeginsAt(10, 20, 3), Is.EqualTo(off + 3).Within(1e-9));
            Assert.That(RoundFade.OffTick, Is.EqualTo(580), "180 aftermath steps, 200 in, 200 out");
        }

        [Test]
        public void The_startup_runs_before_the_first_round_and_from_the_battles_reset()
        {
            Assert.That(RoundStartup.IsRunning(null, 0), Is.True);
            Assert.That(RoundStartup.IsRunning(100, 100 + (RoundFade.ResetTick - 0.5) / 60.0), Is.False, "over the aftermath");
            Assert.That(RoundStartup.IsRunning(100, 100 + (RoundFade.ResetTick + 0.5) / 60.0), Is.True, "under the full image");
        }

        [Test]
        public void A_negative_startup_is_refused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RoundStartup.BeginsAt(0, null, -1));
        }

        [Test]
        public void The_duel_opens_under_its_image_fading_at_obj_fade_gamescreens_speed()
        {
            // gameclock_startup's first pass: obj_fade_gamescreen.mode = "fadeout" from full at 0.005 a step.
            var fade = new LoadingFade(3, RoundFade.FadeSpeed);
            for (var step = 0; step < 100; step++) fade.Step(null);
            Assert.That(fade.Alpha, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(fade.Mode, Is.EqualTo(LoadingFadeMode.FadeOut));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LoadingFade(3, 0));
        }
    }
}
