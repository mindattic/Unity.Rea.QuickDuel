namespace Unity.Rea.QuickDuel.Core
{
    // The white flash over the cut (UQD-T40), as gameclock_aftermath.gml and obj_flash have it. Every figure is the
    // original's own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   aftermath:  instance_create(0, 0, obj_flash)       spr_flash, white, scaled to room_width x room_height
    //   create:     on = true; count = 0; rate = 1; alpha = 0.6; image_alpha = alpha; flash_count = 10; alarm[0] = rate
    //   alarm:      if (on) { on = false; image_alpha = alpha } else { on = true; image_alpha = 0; count += 1 }
    //               if (count >= flash_count) instance_destroy(); alarm[0] = rate
    //
    // So from its creation step the screen is drawn white at 0.6 on steps 0 and 1, then clear on every even step and
    // white on every odd one, ten blinks in all, and the flash is gone on step 20 (a third of a second at 60 a second).
    // Depth -1000: over everything but the letterbox and the loading fade.
    public static class WhiteFlash
    {
        public const double Alpha = 0.6;
        public const int Blinks = 10;
        public const int TicksPerSecond = 60;

        // The step it is destroyed on: every blink is two steps (white, then clear).
        public const int Ticks = 2 * Blinks;

        // Its alpha `tick` steps after it was created: 0.6 or 0; 0 before it and once it is gone.
        public static double AlphaAt(int tick)
        {
            if (tick < 0 || tick >= Ticks)
                return 0;
            return tick == 0 || tick % 2 == 1 ? Alpha : 0;
        }
    }
}
