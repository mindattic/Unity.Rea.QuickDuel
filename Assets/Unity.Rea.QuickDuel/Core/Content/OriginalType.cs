using System;
using System.Collections.Generic;

namespace Unity.Rea.QuickDuel.Core
{
    // The original's type (UQD-T36): which of its five GameMaker bitmap fonts each kind of text was drawn in, and how
    // thick its outline was. Every figure is the original's own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   obj_gameclock, obj_timer, obj_counter: fnt_oldenglish, draw_text_outline(_alpha) ..., 1   "Get Ready", 3-2-1,
    //                                          "Wins!", "Premature Strike!", the reaction count   -> Banner, Countdown
    //   obj_locationart:                       fnt_oldenglish, outline 1, the arena's name            -> Banner
    //   obj_gameclock (aftermath):             fnt_oldenglish_small, outline 1, the winner's name     -> Name
    //   obj_gameover:                          fnt_oldenglish, draw_text_outline(..., 2, "Game Over") -> Title
    //   obj_victory:                           fnt_oldenglish_small, outline 2, its result lines      -> Results
    //   obj_typewriter(_intro):                fnt_mono, draw_text_outline_alpha(..., 2, ...)         -> Intro
    //   obj_gameclock debug, obj_time, obj_btn_*: fnt_basic, outline 1                                -> Hud, Button
    //   health, focus, skill, buff readouts:   fnt_tiny, outline 1                                    -> Tiny
    //   mouseover_message_draw:                fnt_basic, plain draw_text, a menu button's message    -> Message
    //
    // draw_text_outline draws the text in black at every offset of a (2t + 1) x (2t + 1) square around the spot (x
    // outer, y inner, the spot itself included) and then once in white on top; OutlineOffsets is that loop.
    public static class OriginalType
    {
        public const string OldEnglish = "fnt_oldenglish";
        public const string OldEnglishSmall = "fnt_oldenglish_small";
        public const string Mono = "fnt_mono";
        public const string Basic = "fnt_basic";
        public const string Tiny = "fnt_tiny";

        // Every bitmap font the original draws with, as the asset manifest lists them.
        public static readonly IReadOnlyList<string> Fonts = new[] { Basic, Mono, OldEnglish, OldEnglishSmall, Tiny };

        public static readonly TypeStyle Banner = new TypeStyle(OldEnglish, 1);
        public static readonly TypeStyle Countdown = new TypeStyle(OldEnglish, 1);
        public static readonly TypeStyle Title = new TypeStyle(OldEnglish, 2);
        public static readonly TypeStyle Name = new TypeStyle(OldEnglishSmall, 1);
        public static readonly TypeStyle Results = new TypeStyle(OldEnglishSmall, 2);
        public static readonly TypeStyle Intro = new TypeStyle(Mono, 2);
        public static readonly TypeStyle Hud = new TypeStyle(Basic, 1);
        public static readonly TypeStyle Button = new TypeStyle(Basic, 1);
        public static readonly TypeStyle Message = new TypeStyle(Basic, 0);
        public static readonly TypeStyle Small = new TypeStyle(Tiny, 1);

        // draw_text_outline's own colours: a black outline under white text.
        public static readonly Rgb Fill = new Rgb(255, 255, 255);
        public static readonly Rgb Outline = new Rgb(0, 0, 0);

        // The offsets, in original pixels, draw_text_outline draws the outline copy at, in its order: for
        // i = x - t .. x + t, for n = y - t .. y + t (y down). Thickness 0 is no outline.
        public static IReadOnlyList<(int X, int Y)> OutlineOffsets(int thickness)
        {
            if (thickness < 0) throw new ArgumentOutOfRangeException(nameof(thickness), "An outline cannot be thinner than nothing.");
            if (thickness == 0) return Array.Empty<(int, int)>();
            var offsets = new List<(int, int)>((2 * thickness + 1) * (2 * thickness + 1));
            for (var i = -thickness; i <= thickness; i++)
                for (var n = -thickness; n <= thickness; n++)
                    offsets.Add((i, n));
            return offsets;
        }
    }

    // One kind of text: the bitmap font it is drawn in and its outline thickness in original pixels.
    public readonly struct TypeStyle : IEquatable<TypeStyle>
    {
        public TypeStyle(string font, int outline)
        {
            if (string.IsNullOrEmpty(font)) throw new ArgumentException("A text style needs a font.", nameof(font));
            if (outline < 0) throw new ArgumentOutOfRangeException(nameof(outline), "An outline cannot be thinner than nothing.");
            Font = font;
            Outline = outline;
        }

        public string Font { get; }
        public int Outline { get; }

        public bool Equals(TypeStyle other) => Font == other.Font && Outline == other.Outline;
        public override bool Equals(object obj) => obj is TypeStyle other && Equals(other);
        public override int GetHashCode() => (Font?.GetHashCode() ?? 0) * 31 + Outline;
        public override string ToString() => $"{Font} outline {Outline}";
    }
}
