using System;

namespace Unity.Rea.QuickDuel.Core
{
    // How the countdown is drawn (UQD-T37), as obj_gameclock's draw event and gameclock_countdown.gml have it. Every
    // figure is the original's own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   draw:  draw_text_outline_alpha(room_width/2, room_height/2 - 60, 1, "Get Ready", default_alpha)   the whole countdown
    //          if (count >= countdown_time * 0.25)
    //              draw_text_outline_alpha(room_width/2, room_height/2 - 16, 1, string(countdown_string), alpha)
    //   step:  at 25%, 50%, 75% of countdown_time: countdown_string = 3, 2, 1; sfx_beep; alpha = default_alpha (1)
    //          if (alpha > 0) alpha -= 0.02                                  every step, at room_speed 60
    public static class CountdownBanner
    {
        public const string Text = "Get Ready";

        // How far above the number the banner is drawn, in original pixels (room_height/2 - 60 against - 16).
        public const int BannerAboveNumber = 60 - 16;

        // Each number's fade: 0.02 alpha per step at 60 steps a second. Timed by the clock, never by the rendered
        // frame (UQD-LAW-2), so a number is gone the original's 50 steps (0.83 s) after it appears at any frame rate.
        public const double FadePerTick = 0.02;
        public const double TicksPerSecond = 60;
        public const double FadePerSecond = FadePerTick * TicksPerSecond;

        // The number's alpha `seconds` after it appeared: 1, falling 1.2 a second, held at 0. Before it appears
        // (negative or NaN), 0.
        public static double NumberAlpha(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0)
                return 0;
            return Math.Max(0.0, 1.0 - seconds * FadePerSecond);
        }
    }
}
