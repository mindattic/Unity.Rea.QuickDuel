using System;

namespace Unity.Rea.QuickDuel.Core
{
    // What the aftermath draws (UQD-T39), as obj_gameclock's draw event and gameclock_aftermath.gml have it. Every figure
    // is the original's own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   if (!premature_strike)   on the loser's side (right_position_idle when the left won, left_position_idle otherwise):
    //       fnt_oldenglish_small: draw_text_outline_alpha(x, room_height/2 - 80, 1, character_name[winner], default_alpha)
    //       fnt_oldenglish:       draw_text_outline_alpha(x, room_height/2 - 60, 1, "Wins!", default_alpha)
    //       and the winner's reaction: draw_text_outline_alpha(aftermathX, room_height/2 - 16, 1,
    //                                                          string(ceil(winner.reaction_time)), alpha)
    //   else                     fnt_oldenglish: draw_text_outline_alpha(room_width/2, room_height/2 - 60, 1,
    //                                                                    "Premature Strike!", default_alpha)
    //   step:  aftermathX = room_width/2 at startup; if (last_winner = 0) aftermathX -= 1 else aftermathX += 1,
    //          every step at room_speed 60, so the number slides away from the loser one pixel a step
    //          (and alpha -= 0.003, ReactionCounter.FrozenAlpha)
    public static class VerdictBanner
    {
        public const string Wins = "Wins!";

        // The GameMaker build's own line; the rebuild draws a line from the Android taunt list in its place (BIBLE).
        public const string PrematureStrike = "Premature Strike!";

        // "Wins!" and "Premature Strike!" sit on Get Ready's line, 44 original pixels above the number
        // (room_height/2 - 60 against - 16), and the winner's name 20 above that (- 80 against - 60).
        public const int BannerAboveNumber = 60 - 16;
        public const int NameAboveBanner = 80 - 60;

        // The number's slide: one original pixel a step at the aftermath's 60 steps a second, timed by the clock.
        public const double SlidePixelsPerTick = 1;
        public const double SlideTicksPerSecond = 60;
        public const double SlidePixelsPerSecond = SlidePixelsPerTick * SlideTicksPerSecond;

        // The side "Wins!" is drawn on: the loser's.
        public static Side BannerSide(Side winner) => winner == Side.Left ? Side.Right : Side.Left;

        // How far, in original pixels (x right), the winner's number has slid from the centre `seconds` into the
        // aftermath: away from the loser, so left when the left fighter won. 0 before it starts or for NaN.
        public static double Slide(Side winner, double seconds)
        {
            if (double.IsNaN(seconds) || seconds <= 0)
                return 0;
            return (winner == Side.Left ? -1 : 1) * seconds * SlidePixelsPerSecond;
        }

        // The slide for a verdict: only a round won by reaction slides its number, timed from the first strike (where
        // the original's aftermath, and the number's fade, began). Anything else (an early strike, a tie, nobody
        // striking, no verdict yet) leaves it where it is.
        public static double Slide(DuelResult verdict, double? firstStrikeAt, double now)
        {
            if (verdict == null || verdict.Outcome != DuelOutcome.FasterStrike || !verdict.Winner.HasValue || !firstStrikeAt.HasValue)
                return 0;
            return Slide(verdict.Winner.Value, now - firstStrikeAt.Value);
        }

        // Whether the aftermath shows the winner's name and "Wins!": a round won by reaction.
        public static bool ShowsWinner(DuelResult verdict) =>
            verdict != null && verdict.Outcome == DuelOutcome.FasterStrike && verdict.Winner.HasValue;

        // Whether it shows the premature strike line, centred: a round lost by striking early.
        public static bool ShowsPremature(DuelResult verdict) =>
            verdict != null && verdict.Outcome == DuelOutcome.EarlyStrike;
    }
}
