using System;

namespace Unity.Rea.QuickDuel.Core
{
    // The match clock at the top of the duel (UQD-T50), as obj_time has it. Every figure is the original's own, read from
    // BushidoCoda.gmx, not a tuning choice:
    //
    //   obj_game      Create: instance_create(-100, 0, obj_time), once per gamescreen room, so once per match
    //   obj_time      Create: elapsed = 0; strMinutes = "00"; minutes = 0; strSeconds = "0"; seconds = 0;
    //                         in_position = false; timeY = -100
    //                 Step:   if (!in_position) { if (timeY < 5) timeY += 5; else { timeY = 5; in_position = true; } }
    //                         else { elapsed += 1; seconds = floor(elapsed / room_speed);
    //                                if (seconds >= 60) { elapsed = 0; minutes += 1; strMinutes = minutes, "0"-padded }
    //                                strSeconds = seconds, "0"-padded }
    //                 Draw:   fnt_basic, white, draw_text_outline(..., 1, ...) at y = timeY: strMinutes right-aligned at
    //                         room_width / 2 - 3, ":" centred at room_width / 2, strSeconds left-aligned at room_width / 2 + 3
    //
    // So the clock drops in from 100 pixels above the room 5 a step, is home (y 5) on step 21, takes its place on step 22,
    // and from step 23 counts the steps since: mm:ss of the time the match has run since it settled. GameMaker runs a
    // room's first step before its first draw, so the frame on step `tick` (0 the first) shows the clock after tick + 1
    // steps, as CornerArt counts them.
    //
    // Fixed defects:
    //   - The clock counted frames and divided by room_speed, but the duel's action phase ran the room at 1000 steps a
    //     second (gameclock_action.gml), so at every cue the reading fell to a sixteenth of itself and, back at 60, leapt
    //     ahead by every millisecond step the action had counted. The rebuild counts the clock's own seconds.
    //   - On the step a minute turned, strSeconds was set from the 60 just reset, so "mm:60" showed for a step in place of
    //     "mm:00"; the rebuild shows ":00".
    //   - strSeconds started as "0", not "00", so the clock slid in reading "00:0"; it reads "00:00" from the start, as
    //     every later reading pads it.
    public static class RoundClock
    {
        public const int TicksPerSecond = 60;

        public const int RoomWidth = 640;

        // timeY: where the clock starts, how far it drops a step, and its place (the top of its text).
        public const int StartY = -100;
        public const int DropPerStep = 5;
        public const int HomeY = 5;

        // The text's x either side of the colon: the minutes end 3 pixels left of the room's middle, the seconds start 3
        // right of it.
        public const int ColonX = RoomWidth / 2;
        public const int Gap = 3;
        public const int MinutesRight = ColonX - Gap;
        public const int SecondsLeft = ColonX + Gap;

        // Steps until timeY reaches its place (21), and the step that finds it there and sets in_position (22).
        public const int HomeSteps = (HomeY - StartY + DropPerStep - 1) / DropPerStep;
        public const int InPositionSteps = HomeSteps + 1;

        // timeY after `steps` steps.
        public static int YAfter(long steps) => (int)Math.Min(HomeY, StartY + DropPerStep * Math.Max(0, steps));

        // in_position after `steps` steps.
        public static bool InPositionAfter(long steps) => steps >= InPositionSteps;

        // elapsed: the steps counted after `steps` steps, every step from the one after in_position was set.
        public static long ElapsedStepsAfter(long steps) => Math.Max(0, steps - InPositionSteps);

        // The frame on step `tick` shows the clock after tick + 1 steps; before the first it is where it started.
        public static int YAt(long tick) => tick < 0 ? StartY : YAfter(tick + 1);
        public static bool InPosition(long tick) => tick >= 0 && InPositionAfter(tick + 1);
        public static long ElapsedStepsAt(long tick) => tick < 0 ? 0 : ElapsedStepsAfter(tick + 1);

        // Whole seconds the clock has counted by step `tick`.
        public static long SecondsAt(long tick) => ElapsedStepsAt(tick) / TicksPerSecond;

        // strMinutes and strSeconds on step `tick`, each padded to two digits ("00", "07", "59"); the minutes run on past 99.
        public static string MinutesText(long tick) => Pad(SecondsAt(tick) / 60);
        public static string SecondsText(long tick) => Pad(SecondsAt(tick) % 60);

        // The whole reading, "mm:ss".
        public static string Text(long tick) => MinutesText(tick) + ":" + SecondsText(tick);

        // The step `seconds` after the clock was made lands on; negative before it.
        public static long TickAt(double seconds)
        {
            if (double.IsNaN(seconds))
                return -1;
            return (long)Math.Floor(seconds * TicksPerSecond);
        }

        private static string Pad(long value) => value < 10 ? "0" + value : value.ToString();
    }
}
