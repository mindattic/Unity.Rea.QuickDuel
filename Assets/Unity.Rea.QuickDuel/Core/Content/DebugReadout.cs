using System;

namespace Unity.Rea.QuickDuel.Core
{
    // The original's debug overlay (UQD-T50), F12's: globals.gml's `debug` toggled by obj_game's F12 key press. Every
    // figure is the original's own, read from BushidoCoda.gmx:
    //
    //   obj_game        Draw, if (debug): fnt_oldenglish_small, white, draw_text_outline(..., 1, ...)
    //                     string(room_speed) at (room_width / 2, 150), fa_left
    //                     string(player[left].reaction_time) at (10, room_height / 2), fa_left
    //                     string(player[right].reaction_time) at (room_width - 10, room_height / 2), fa_right
    //   obj_gameclock   Draw, if (debug): fnt_basic, white, outline 1, fa_left
    //                     string(count) at (10, 10); the phase's length (startup_time, countdown_time, drumroll_time or
    //                     aftermath_time) at (10, 28), nothing in the action
    //
    // A reaction_time was the 1 ms ticks from the cue to the strike, 9999 (gameclock_startup.gml) until one was made.
    //
    // Deliberate: globals.gml shipped `debug = true`, so the original opened with the overlay over the duel. The rebuild
    // makes it the developer's toggle: off at the start, F12 turns it on and off, and only in the editor and development
    // builds. room_speed (60, or the action's 1000) has no rebuild equivalent, since the rebuild times by the clock
    // (UQD-LAW-2); the frames drawn a second stand in its place. The phase's count reads in milliseconds, as the rebuild
    // counts them, with the phase's name above it.
    public static class DebugReadout
    {
        public const int RoomWidth = 640;
        public const int RoomHeight = 480;

        // obj_game's three lines.
        public const int RateX = RoomWidth / 2;
        public const int RateY = 150;
        public const int LeftReactionX = 10;
        public const int RightReactionX = RoomWidth - 10;
        public const int ReactionY = RoomHeight / 2;

        // obj_gameclock's two lines.
        public const int PhaseX = 10;
        public const int PhaseY = 10;
        public const int CountY = 28;

        // What a side's reaction reads before it has struck.
        public const string NoReaction = "9999";

        // A side's reaction_time: whole milliseconds from the cue (negative for a strike before it), or 9999 for none.
        public static string Reaction(double? seconds) =>
            seconds.HasValue ? ((long)Math.Round(seconds.Value * 1000, MidpointRounding.AwayFromZero)).ToString() : NoReaction;

        // The frames drawn a second, in room_speed's place: whole frames, 0 for no reading.
        public static string Rate(double framesPerSecond) =>
            double.IsNaN(framesPerSecond) || double.IsInfinity(framesPerSecond) || framesPerSecond <= 0
                ? "0"
                : ((long)Math.Round(framesPerSecond, MidpointRounding.AwayFromZero)).ToString();

        // A count in milliseconds, whole.
        public static string Milliseconds(double seconds) =>
            ((long)Math.Round(Math.Max(0, seconds) * 1000, MidpointRounding.AwayFromZero)).ToString();
    }
}
