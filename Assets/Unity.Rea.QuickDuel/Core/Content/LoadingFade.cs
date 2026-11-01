using System;

namespace Unity.Rea.QuickDuel.Core
{
    // What a room's loading fade is doing (UQD-T44).
    public enum LoadingFadeMode
    {
        // "fadeout": the image the room opened under fades away.
        FadeOut,

        // "off": nothing drawn.
        Off,

        // "fadein": an image fades in over the room, and once it is full the next room is entered.
        FadeIn
    }

    // A room's own loading fade (UQD-T44), as obj_fade_intro has it (obj_fade_mainmenu's differs: MenuFade). Every figure is the original's
    // own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   Create   sprite_index = fade_asset; image_alpha = 1; mode = "fadeout"; fadespeed = 0.01; firstPass = true
    //   Step     "off":     image_alpha = 0
    //            "fadein":  if (firstPass) { firstPass = false; image_alpha = 0; fade_asset = one of spr_loading1 to 14
    //                       (irandom_range(1, 14)); sprite_index = fade_asset }
    //                       if (image_alpha < 1) image_alpha += fadespeed; else room_goto(mainmenu)
    //            "fadeout": if (firstPass) { firstPass = false; image_alpha = 1 }
    //                       if (image_alpha > 0) image_alpha -= fadespeed; else mode = "off"
    //   Draw     draw_self() at depth -10000, over everything
    //
    // fade_asset is a global, so the next room's fade opens under the very image this one faded in (globals.gml starts it
    // at spr_loading1, so the intro, the game's first room, opens under spr_loading1).
    //
    // A fixed defect: obj_fade_intro shares its one firstPass between both modes and the fade-out on room start has
    // already spent it, so the fade-in's first pass, where the random image is chosen, could never run: the intro faded
    // spr_loading1 back in every time. Here entering "fadein" starts its own first pass, so it picks a random image and
    // fades it in from nothing, as the code was written to (BIBLE).
    //
    // The alpha is added and taken away in steps of 0.01 in double precision as GameMaker did, so a fade out from 1 takes
    // 100 steps to reach (about) nothing and turns "off" on the step after, and a fade in from 0 is full (1.0000000000000007)
    // after 100 steps and enters the next room on the 101st.
    public sealed class LoadingFade
    {
        public const double FadeSpeed = 0.01;

        // spr_loading1 to 14.
        public const int Images = 14;

        private bool firstPass = true;
        private readonly double fadeSpeed;

        // The fade a room opens with: `image` (0 to 13 for spr_loading1 to 14) full, fading out at `speed` a step
        // (obj_fade_intro's 0.01 unless given; obj_fade_gamescreen fades its room in at RoundFade.FadeSpeed, 0.005).
        public LoadingFade(int image, double speed = FadeSpeed)
        {
            if (image < 0 || image >= Images) throw new ArgumentOutOfRangeException(nameof(image), "There are 14 loading images.");
            if (!(speed > 0) || speed > 1) throw new ArgumentOutOfRangeException(nameof(speed), "A fade moves more than nothing and at most all a step.");
            fadeSpeed = speed;
            Image = image;
            Alpha = 1;
            Mode = LoadingFadeMode.FadeOut;
        }

        // The loading image drawn, 0 to 13 for spr_loading1 to 14.
        public int Image { get; private set; }

        // image_alpha.
        public double Alpha { get; private set; }

        public LoadingFadeMode Mode { get; private set; }

        // The fade-in is full: room_goto(mainmenu).
        public bool Finished { get; private set; }

        // mode = "fadein". Asking again while it already fades in changes nothing.
        public void FadeIn()
        {
            if (Mode == LoadingFadeMode.FadeIn) return;
            Mode = LoadingFadeMode.FadeIn;
            firstPass = true;
        }

        // One 60 a second step.
        public void Step(Random random)
        {
            switch (Mode)
            {
                case LoadingFadeMode.Off:
                    Alpha = 0;
                    break;
                case LoadingFadeMode.FadeIn:
                    if (firstPass)
                    {
                        if (random == null) throw new ArgumentNullException(nameof(random));
                        firstPass = false;
                        Alpha = 0;
                        Image = random.Next(Images);
                    }
                    if (Alpha < 1) Alpha += fadeSpeed;
                    else Finished = true;
                    break;
                default:
                    if (firstPass)
                    {
                        firstPass = false;
                        Alpha = 1;
                    }
                    if (Alpha > 0) Alpha -= fadeSpeed;
                    else Mode = LoadingFadeMode.Off;
                    break;
            }
        }

        // The alpha drawn, never below nothing (the last 0.01 taken off 1 may leave a hair under or over 0).
        public double DrawnAlpha => Math.Max(0, Math.Min(1, Alpha));

        // The steps `seconds` after a room starts have run by then: GameMaker runs a room's first step before its first
        // draw, so the frame at 0 s already shows one step; none before it.
        public static long StepsAt(double seconds, int ticksPerSecond = 60)
        {
            if (double.IsNaN(seconds) || seconds < 0) return 0;
            return (long)Math.Floor(seconds * ticksPerSecond) + 1;
        }
    }
}
