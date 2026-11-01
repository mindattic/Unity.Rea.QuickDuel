using System;

namespace Unity.Rea.QuickDuel.Core
{
    // The main menu's loading fade (UQD-T45), obj_fade_mainmenu exactly. It differs from the intro's (LoadingFade) in
    // where it chooses its image and in how its one firstPass is shared, and both are kept, since neither is broken here.
    // Every figure is the original's own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   Create   sprite_index = fade_asset; image_alpha = 1; mode = "fadeout"; fadespeed = 0.01; firstPass = true
    //   Step     "off":     if (firstPass) { firstPass = false; image_alpha = 0; fade_asset = one of spr_loading1 to 14
    //                       (irandom_range(1, 14)); sprite_index = fade_asset }
    //            "fadein":  if (firstPass) { firstPass = false; image_alpha = 0 }
    //                       if (image_alpha < 1) image_alpha += fadespeed; else room_goto(gamescreen)
    //            "fadeout": if (firstPass) { firstPass = false; image_alpha = 1 }
    //                       if (image_alpha > 0) image_alpha -= fadespeed; else { firstPass = true; mode = "off" }
    //   Draw GUI draw_self() at depth -10000, over everything
    //
    // So the image the menu leaves under is chosen the moment its opening fade is over, and a button pressed while that
    // fade is still running turns it round: the image the menu opened under fades back in from where it had got to.
    // A button pressed on the one step between the fade-out ending and "off" choosing an image fades the opening image
    // in from nothing. The new game (vs computer) and versus buttons both set mode = "fadein"; nothing else does.
    public sealed class MenuFade
    {
        public const double FadeSpeed = LoadingFade.FadeSpeed;

        public const int Images = LoadingFade.Images;

        private bool firstPass = true;

        // The fade the menu opens with: `image` (0 to 13 for spr_loading1 to 14, the global fade_asset) full, fading out.
        public MenuFade(int image)
        {
            if (image < 0 || image >= Images) throw new ArgumentOutOfRangeException(nameof(image), "There are 14 loading images.");
            Image = image;
            Alpha = 1;
            Mode = LoadingFadeMode.FadeOut;
        }

        // The loading image drawn, 0 to 13 for spr_loading1 to 14.
        public int Image { get; private set; }

        // image_alpha.
        public double Alpha { get; private set; }

        public LoadingFadeMode Mode { get; private set; }

        // The fade-in is full: room_goto(gamescreen).
        public bool Finished { get; private set; }

        // mode = "fadein": firstPass is left as it is, as the buttons left it.
        public void FadeIn() => Mode = LoadingFadeMode.FadeIn;

        // One 60 a second step.
        public void Step(Random random)
        {
            switch (Mode)
            {
                case LoadingFadeMode.Off:
                    if (firstPass)
                    {
                        if (random == null) throw new ArgumentNullException(nameof(random));
                        firstPass = false;
                        Alpha = 0;
                        Image = random.Next(Images);
                    }
                    break;
                case LoadingFadeMode.FadeIn:
                    if (firstPass)
                    {
                        firstPass = false;
                        Alpha = 0;
                    }
                    if (Alpha < 1) Alpha += FadeSpeed;
                    else Finished = true;
                    break;
                default:
                    if (firstPass)
                    {
                        firstPass = false;
                        Alpha = 1;
                    }
                    if (Alpha > 0)
                    {
                        Alpha -= FadeSpeed;
                    }
                    else
                    {
                        firstPass = true;
                        Mode = LoadingFadeMode.Off;
                    }
                    break;
            }
        }

        // The alpha drawn, never below nothing nor over full.
        public double DrawnAlpha => Math.Max(0, Math.Min(1, Alpha));
    }
}
