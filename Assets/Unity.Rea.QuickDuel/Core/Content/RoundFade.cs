using System;

namespace Unity.Rea.QuickDuel.Core
{
    // Where the round transition stands (UQD-T42).
    public enum RoundFadeStage
    {
        // The aftermath plays on, no image drawn yet.
        Aftermath,

        // The loading image fades in over the aftermath.
        FadingIn,

        // The image is fully up: the battle has been reset under it, and it fades out over the startup.
        FadingOut,

        // The image is gone.
        Off
    }

    // The round transition (UQD-T42), as gameclock_aftermath.gml, obj_fade_gamescreen and gameclock_startup.gml have it.
    // Every figure is the original's own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   obj_gameclock         aftermath_time = 3 * room_speed (180 steps at 60 a second)
    //   gameclock_aftermath   count += 1 every step from the cut; if (count >= aftermath_time) obj_fade_gamescreen.mode = "fadein"
    //   obj_fade_gamescreen   one of spr_loading1 to 14 (irandom_range(1, 14)), drawn over everything (depth -10000) at
    //                         image_alpha, fadespeed = 0.005;
    //                         "fadein":  if (image_alpha < 1) image_alpha += fadespeed, else reset the battle:
    //                                    with (obj_blood_emitter) instance_destroy(); with (obj_weapon) instance_destroy();
    //                                    gameclock.mode = "startup"
    //                         "fadeout": if (image_alpha > 0) image_alpha -= fadespeed, else mode = "off"
    //   gameclock_startup     first pass: set_state_idle for both fighters, reaction times cleared, the fade to "fadeout"
    //
    // So, counted in 60 a second steps from the cut: the aftermath plays alone for 180 steps, the image fades in 0.005 a
    // step for 200 more and is fully up on step 380, where the battle is reset under it (the splatters are not
    // obj_fade_gamescreen's to destroy: they fade on at their own 0.0003 a step), and it fades out 0.005 a step over the
    // startup, gone on step 580. The startup counts its 3 s only once the image is gone, and a click skips it from the
    // reset on while the image is still fading out (RoundStartup, UQD-T66).
    public static class RoundFade
    {
        public const int TicksPerSecond = 60;

        // aftermath_time: the aftermath's steps before the image starts to fade in.
        public const int AftermathTicks = 3 * TicksPerSecond;

        public const double FadeSpeed = 0.005;

        // The steps the image takes to go from nothing to full, and back: 1 / fadespeed.
        public const int FadeTicks = 200;

        // The step the image is fully up and the battle is reset under it.
        public const int ResetTick = AftermathTicks + FadeTicks;

        // The step the image is gone.
        public const int OffTick = ResetTick + FadeTicks;

        // spr_loading1 to 14.
        public const int Images = 14;

        // The step `seconds` after the cut lands on; negative before it.
        public static long TickAt(double seconds)
        {
            if (double.IsNaN(seconds))
                return -1;
            return (long)Math.Floor(seconds * TicksPerSecond);
        }

        public static RoundFadeStage StageAt(long tick)
        {
            if (tick < AftermathTicks) return RoundFadeStage.Aftermath;
            if (tick < ResetTick) return RoundFadeStage.FadingIn;
            if (tick < OffTick) return RoundFadeStage.FadingOut;
            return RoundFadeStage.Off;
        }

        // The image's alpha on step `tick` after the cut: 0 through the aftermath, rising 0.005 a step to 1 on the reset
        // step, then falling 0.005 a step to 0.
        public static double AlphaAt(long tick)
        {
            if (tick < AftermathTicks || tick >= OffTick)
                return 0;
            if (tick < ResetTick)
                return (tick - AftermathTicks + 1) * FadeSpeed;
            return 1 - (tick - ResetTick) * FadeSpeed;
        }

        // Whether the battle has been reset under the image by step `tick` (and the next round's startup runs).
        public static bool HasReset(long tick) => tick >= ResetTick;

        // The loading image a transition draws, 0 to 13 for spr_loading1 to 14: irandom_range(1, 14), each equally likely.
        public static int Pick(Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            return random.Next(Images);
        }
    }
}
