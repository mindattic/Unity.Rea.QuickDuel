using System;

namespace Unity.Rea.QuickDuel.Core
{
    // Where and how the two fighters stand before the cue (UQD-T34), as the original's globals.gml, obj_game and
    // obj_player have it. Engine-free numbers only; DuelView turns them into its stage. Every figure here is the
    // original's own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   left_position_idle  = room_width / 4                         -> LeftX  0.25 of the width
    //   right_position_idle = room_width - room_width / 4            -> RightX 0.75 of the width
    //   vertical_position   = room_height - room_height / 3          -> Y      2/3 of the height, from the top
    //   player[right].image_xscale = -1                              -> the right fighter is mirrored to face left
    //   obj_player: image_alpha = 0; fadespeed = 0.01, added every step at room_speed 60
    //   obj_game:   same character_id -> image_blend color_red (left), color_blue (right)
    //   globals.gml: color_red = RGB(196,64,64), color_blue = RGB(64,64,196)
    public static class FighterLineup
    {
        // The original's room (gamescreen.room.gmx), the scale its sprites were drawn for.
        public const int OriginalWidth = 640;
        public const int OriginalHeight = 480;

        public const double LeftX = 0.25;
        public const double RightX = 0.75;
        public const double Y = 2.0 / 3.0;

        // obj_player's fade: 0.01 alpha per step at 60 steps a second. The rebuild times it by the clock, never by the
        // rendered frame (UQD-LAW-2), so it takes the original's 100 steps (1.67 s) at any frame rate.
        public const double FadePerTick = 0.01;
        public const double TicksPerSecond = 60;
        public const double FadePerSecond = FadePerTick * TicksPerSecond;

        public static readonly Rgb Red = new Rgb(196, 64, 64);
        public static readonly Rgb Blue = new Rgb(64, 64, 196);

        // The fraction of the width `side`'s fighter stands at.
        public static double X(Side side) => side == Side.Left ? LeftX : RightX;

        // 1 for the left fighter (faces right), -1 for the right one (mirrored to face left).
        public static int Facing(Side side) => side == Side.Left ? 1 : -1;

        // The fighter's alpha `seconds` after it took the field: 0, rising 0.6 a second, held at 1.
        public static double Alpha(double seconds)
        {
            if (double.IsNaN(seconds) || seconds <= 0)
                return 0;
            return Math.Min(1.0, seconds * FadePerSecond);
        }

        // The image_blend `side`'s fighter is drawn with: red on the left and blue on the right when both seats hold
        // the same fighter, else none (null, drawn untinted).
        public static Rgb? Tint(Side side, bool sameFighter) =>
            !sameFighter ? (Rgb?)null : side == Side.Left ? Red : Blue;
    }

    // An 8-bit colour, as GameMaker's make_color_rgb took it.
    public readonly struct Rgb : IEquatable<Rgb>
    {
        public Rgb(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public bool Equals(Rgb other) => R == other.R && G == other.G && B == other.B;
        public override bool Equals(object obj) => obj is Rgb other && Equals(other);
        public override int GetHashCode() => (R << 16) | (G << 8) | B;
        public override string ToString() => $"RGB({R},{G},{B})";
    }
}
