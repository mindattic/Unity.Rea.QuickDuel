using System;

namespace Unity.Rea.QuickDuel.Core
{
    // The startup before each round (UQD-T66), as obj_gameclock and gameclock_startup.gml run it. Every rule is the
    // original's own, read from BushidoCoda.gmx; the length is tuning (DuelTuning.StartupSeconds, startup_time):
    //
    //   obj_gameclock      juststarted = true; mode = "startup"; startup_time = 3 * room_speed
    //   gameclock_startup  if (obj_fade_gamescreen.mode = "off" || juststarted) { juststarted = false; count += 1 }
    //                      if (count >= startup_time) mode = "countdown"
    //   obj_game           global left press: if (gameclock.mode == "startup") mode = "countdown"   (the skip)
    //   obj_fade_gamescreen  "fadein" full: the battle is reset and gameclock.mode = "startup"; then "fadeout" to "off"
    //
    // So the first round's countdown starts startup_time after the room starts (juststarted: it counts at once, under the
    // fade the room opened with), and every later round's startup_time after the loading image of the round transition is
    // gone (RoundFade.OffTick steps after the round ended). The startup itself runs from the room's start, and later from
    // the battle's reset under the full image (RoundFade.ResetTick), and a left click any time in it starts the countdown
    // at once. Times are seconds on the duel's clock.
    public static class RoundStartup
    {
        // When the countdown of the next round begins by itself: `startupSeconds` after the scene started for the first
        // round (no round ended yet), else `startupSeconds` after the last round's loading image is gone.
        public static double BeginsAt(double sceneStartedAt, double? lastRoundEndedAt, double startupSeconds)
        {
            if (double.IsNaN(startupSeconds) || startupSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(startupSeconds), "The startup must not be negative.");
            var countsFrom = lastRoundEndedAt.HasValue
                ? lastRoundEndedAt.Value + (double)RoundFade.OffTick / RoundFade.TicksPerSecond
                : sceneStartedAt;
            return countsFrom + startupSeconds;
        }

        // Whether the startup is running at `now` (a click may skip it): from the scene's start until the first round,
        // and from the battle's reset under the loading image (gameclock.mode = "startup") until the next round.
        public static bool IsRunning(double? lastRoundEndedAt, double now) =>
            !lastRoundEndedAt.HasValue || RoundFade.HasReset(RoundFade.TickAt(now - lastRoundEndedAt.Value));
    }
}
