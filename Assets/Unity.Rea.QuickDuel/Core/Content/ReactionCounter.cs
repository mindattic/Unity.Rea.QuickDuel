using System;

namespace Unity.Rea.QuickDuel.Core
{
    // The counting-up number drawn in the centre while the cue is up (UQD-T38), as obj_gameclock's draw event,
    // gameclock_action.gml, action_attack.gml and gameclock_aftermath.gml have it. Every figure is the original's own,
    // read from BushidoCoda.gmx, not a tuning choice:
    //
    //   action:     room_speed = actionFPS (1000); count = 0; alpha = default_alpha (1); then count += 1 every step
    //               draw_text_outline_alpha(room_width/2, room_height/2 - 16, 1, string(ceil(count)), alpha)
    //   strike:     action_attack: reaction_time = gameclock.count; mode = "aftermath"   the number stops at the strike
    //   aftermath:  room_speed = normalFPS (60); alpha = default_alpha; if (alpha > 0) alpha -= 0.003 every step,
    //               the number now string(ceil(reaction_time))
    //
    // So the count is milliseconds since the cue, one per 1 ms tick, starting at 1 on the cue's own step. The rebuild
    // reads it from the event clock (UQD-LAW-2), never from rendered frames, so it is the same at any frame rate.
    public static class ReactionCounter
    {
        public const double TicksPerSecond = 1000;

        // The frozen number's fade: 0.003 alpha per step at the aftermath's 60 steps a second, so it is gone 333 steps
        // (5.6 s) after the strike.
        public const double FadePerTick = 0.003;
        public const double FadeTicksPerSecond = 60;
        public const double FadePerSecond = FadePerTick * FadeTicksPerSecond;

        // The number shown `seconds` after the cue: ceil(count) at one tick a millisecond, never below the 1 the cue's
        // own step draws (count = 0; count += 1 before the first draw). NaN or before the cue: 1.
        public static int Count(double seconds)
        {
            if (double.IsNaN(seconds) || seconds <= 0)
                return 1;
            // Rounded to a microsecond first, so a timestamp like 0.25 that is 0.2500000001 in binary reads 250, not 251.
            var ticks = Math.Round(seconds * TicksPerSecond, 3);
            return (int)Math.Max(1, Math.Ceiling(ticks));
        }

        // The frozen number's alpha `seconds` after the strike that froze it: 1, falling 0.18 a second, held at 0.
        // Before the strike (negative) it is still at full alpha; NaN, 0.
        public static double FrozenAlpha(double seconds)
        {
            if (double.IsNaN(seconds))
                return 0;
            if (seconds <= 0)
                return 1;
            return Math.Max(0.0, 1.0 - seconds * FadePerSecond);
        }
    }
}
