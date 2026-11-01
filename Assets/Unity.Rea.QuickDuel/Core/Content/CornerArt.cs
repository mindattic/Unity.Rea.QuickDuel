using System;

namespace Unity.Rea.QuickDuel.Core
{
    // The corner arts sliding in on start (UQD-T43), as obj_cornerart_left and obj_cornerart_right have them. Every figure is
    // the original's own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   gamescreen.room       obj_cornerart_left at (0, 24), obj_cornerart_right at (488, 24); the room is 640 wide
    //   both objects          spr_cornerart_left2 / right2, 152 x 32 with the origin at the top left, depth -101, Draw GUI
    //   obj_cornerart_left    Create: x = -sprite_width; in_position = false
    //                         Step:   if (!in_position) { if (x < 0) x += 1 + abs(x) * 0.05; else { x = 0; in_position = true; } }
    //   obj_cornerart_right   Create: x = room_width; in_position = false
    //                         Step:   if (!in_position) { if (x > room_width - sprite_width)
    //                                     x -= 1 + (x - (room_width - sprite_width)) * 0.05;
    //                                 else { x = room_width - sprite_width; in_position = true; } }
    //
    // Both start a whole sprite width off their edge and close the distance left by 1 + 0.05 of it a step, so the two move
    // as mirror images of one distance: 152 off, then 0.95 d - 1 each step. The step that takes it past its place draws it
    // there, a hair over (0.03 of a pixel on step 42); the next step snaps it home and it stays. GameMaker runs a room's
    // first step before its first draw, so the frame on step `tick` (0 the first) shows the art after tick + 1 steps.
    public static class CornerArt
    {
        public const int TicksPerSecond = 60;

        // spr_cornerart_left2 and right2.
        public const int SpriteWidth = 152;
        public const int SpriteHeight = 32;

        public const int RoomWidth = 640;

        // Both arts' room y, their top edge.
        public const int Y = 24;

        // The right art's place: room_width - sprite_width.
        public const int RightX = RoomWidth - SpriteWidth;

        public const double SlideFraction = 0.05;
        public const double SlideMinimum = 1;

        // The steps the slide takes until it is snapped home (in_position).
        public static readonly int InPositionSteps = CountSteps();

        // How far both arts are from their places after `steps` steps (positive while still off it): 152 before any.
        public static double DistanceAfter(long steps)
        {
            var distance = (double)SpriteWidth;
            for (long step = 0; step < steps; step++)
            {
                if (distance > 0)
                    distance -= SlideMinimum + distance * SlideFraction;
                else
                    return 0;
            }
            return distance;
        }

        // The distance drawn on the frame of step `tick` (0 the room's first): after tick + 1 steps; the full width before it.
        public static double DistanceAt(long tick) => tick < 0 ? SpriteWidth : DistanceAfter(Math.Min(tick + 1, InPositionSteps));

        // The left art's room x on step `tick`: -distance.
        public static double LeftXAt(long tick) => -DistanceAt(tick);

        // The right art's room x on step `tick`: room_width - sprite_width + distance.
        public static double RightXAt(long tick) => RightX + DistanceAt(tick);

        // Whether both arts are home by step `tick`.
        public static bool InPosition(long tick) => tick >= 0 && tick + 1 >= InPositionSteps;

        // The step `seconds` after the start lands on; negative before it.
        public static long TickAt(double seconds)
        {
            if (double.IsNaN(seconds))
                return -1;
            return (long)Math.Floor(seconds * TicksPerSecond);
        }

        private static int CountSteps()
        {
            var distance = (double)SpriteWidth;
            var steps = 0;
            while (true)
            {
                steps++;
                if (distance > 0)
                    distance -= SlideMinimum + distance * SlideFraction;
                else
                    return steps;
            }
        }
    }
}
