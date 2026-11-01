using System;
using System.Collections.Generic;

namespace Unity.Rea.QuickDuel.Core
{
    // One drifting cloud of the intro (obj_cloud1 to 4, children of obj_cloud_parent): which of spr_cloud1 to 4 it draws
    // (0 to 3) and its room position, the sprite's top left (the clouds' origin is 0, 0).
    public sealed class IntroCloud
    {
        internal IntroCloud(int kind, int x, int y)
        {
            Kind = kind;
            X = x;
            Y = y;
        }

        public int Kind { get; }
        public int X { get; internal set; }
        public int Y { get; internal set; }
    }

    // The intro room (UQD-T44), stepped at the original's 60 a second. Every figure is the original's own, read from
    // BushidoCoda.gmx, not a tuning choice:
    //
    //   intro.room            640 x 480 at 60 fps, bg_mapscreen (640 x 480) as its background; obj_intro, then obj_cloud1,
    //                         2, 3, 4 three times over, then obj_fade_intro (their room positions are overwritten on create)
    //   obj_intro             Create: globals(); alarm[0] = 3 * room_speed
    //                         Alarm 0: instance_create(0, 0, obj_typewriter_intro)
    //                         Global left pressed: obj_fade_intro.mode = "fadein"
    //   obj_cloud_parent      Create: hspeed = -1; image_alpha = 0.5;
    //                                 x = irandom_range(-sprite_width / 2, room_width + sprite_width / 2);
    //                                 y = irandom_range(-sprite_height / 2, room_height - sprite_height / 2)
    //                         Step:   if (x < -sprite_width) { x = irandom_range(room_width, room_width * 2);
    //                                 y = irandom_range(-sprite_height / 2, room_height - sprite_height / 2) }
    //   obj_cloud1 to 4       spr_cloud1 to 4, each 256 x 48 with the origin at the top left, depth -100 (over the text)
    //   obj_typewriter_intro  Create: x = 40; y = 50; type_speed = 4; strText = introText; alarm[0] = type_speed
    //                         Alarm 0: if (copystring != strText) one more character of strText is shown, else the first
    //                                  time: is_complete = true; alarm[1] = 300; then alarm[0] = type_speed again
    //                         Alarm 1: obj_fade_intro.mode = "fadein"
    //                         Draw:    fnt_mono, white, draw_text_outline_alpha(x, y, 2, copystring, alpha) at alpha 1 (its
    //                                  fadeout is never set in the intro), depth 1
    //   obj_fade_intro        LoadingFade: spr_loading1 fading out from full at 0.01 a step on start; the fade-in takes the
    //                         room to the main menu
    //
    // GameMaker's step: alarms, then the mouse, then each instance's Step, then movement (x += hspeed), then the draw. An
    // alarm set to n during a step goes off n steps later; an instance created during a step runs its alarms from the
    // next. So, in steps from the room's first (1): the typewriter appears on step 180, shows its first character on step
    // 184 and one more every 4 steps, holds the whole text 300 steps (5 s) from the step after its last character's alarm
    // found nothing left to type, and then the loading image fades in over 100 steps and the menu follows on the next.
    // A click, at any moment, starts that fade in at once.
    public sealed class IntroScene
    {
        public const int TicksPerSecond = 60;

        public const int RoomWidth = 640;
        public const int RoomHeight = 480;

        // spr_cloud1 to 4.
        public const int CloudKinds = 4;
        public const int CloudWidth = 256;
        public const int CloudHeight = 48;

        // Each of obj_cloud1 to 4 is placed three times in the room.
        public const int CloudCopies = 3;

        // hspeed and image_alpha.
        public const int CloudSpeed = -1;
        public const double CloudAlpha = 0.5;

        // obj_intro's alarm[0]: the steps before the typewriter appears.
        public const int TypewriterDelay = 3 * TicksPerSecond;

        // obj_typewriter_intro.
        public const int TextX = 40;
        public const int TextY = 50;
        public const int TypeSpeed = 4;
        public const int HoldSteps = 300;

        private readonly Random random;
        private readonly List<IntroCloud> clouds = new List<IntroCloud>();
        private int introAlarm = TypewriterDelay;
        private int typeAlarm = -1;
        private int holdAlarm = -1;
        private bool skipPending;

        // The room as it is created: every cloud placed at random, the typewriter not there yet, and the fade at
        // spr_loading1 (globals.gml's fade_asset) full and about to fade out. `text` is introText.
        public IntroScene(string text, Random random)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            for (var copy = 0; copy < CloudCopies; copy++)
                for (var kind = 0; kind < CloudKinds; kind++)
                {
                    var x = random.Next(-CloudWidth / 2, RoomWidth + CloudWidth / 2 + 1);
                    var y = NextCloudY();
                    clouds.Add(new IntroCloud(kind, x, y));
                }
            Fade = new LoadingFade(0);
        }

        // introText, "#" line breaks already newlines.
        public string Text { get; }

        // The steps run so far.
        public long Steps { get; private set; }

        // The twelve clouds, in the room's instance order (obj_cloud1, 2, 3, 4, three times over).
        public IReadOnlyList<IntroCloud> Clouds => clouds;

        // obj_typewriter_intro has been created.
        public bool TypewriterShown { get; private set; }

        // How many characters of Text the typewriter shows (copystring_pos).
        public int Typed { get; private set; }

        // copystring: the start of the text typed so far.
        public string TypedText => Text.Substring(0, Typed);

        // is_complete: the whole text is up and the 300-step hold has begun.
        public bool Complete { get; private set; }

        // obj_fade_intro.
        public LoadingFade Fade { get; }

        // The fade in is full: room_goto(mainmenu).
        public bool Finished => Fade.Finished;

        // obj_intro's global left press (and, here, any key or pad button): the loading fade fades in from the next step.
        public void Skip() => skipPending = true;

        // Runs steps until `steps` have run.
        public void RunTo(long steps)
        {
            while (Steps < steps && !Finished)
                Step();
        }

        // One 60 a second step of the whole room.
        public void Step()
        {
            if (Finished) return;
            Steps++;

            // Alarms. The typewriter, created by obj_intro's alarm this step, runs its own from the next.
            var typewriterWasThere = TypewriterShown;
            if (introAlarm > 0 && --introAlarm == 0)
            {
                TypewriterShown = true;
                typeAlarm = TypeSpeed;
            }
            if (typewriterWasThere)
            {
                // The hold is counted before alarm 0 can set it, so the step that sets it is not one of its 300.
                var holdOver = holdAlarm > 0 && --holdAlarm == 0;
                if (typeAlarm > 0 && --typeAlarm == 0)
                    Type();
                if (holdOver)
                    Fade.FadeIn();
            }

            // The mouse.
            if (skipPending)
            {
                skipPending = false;
                Fade.FadeIn();
            }

            // Step events: the clouds wrap back to the right, the fade fades.
            foreach (var cloud in clouds)
                if (cloud.X < -CloudWidth)
                {
                    cloud.X = random.Next(RoomWidth, RoomWidth * 2 + 1);
                    cloud.Y = NextCloudY();
                }
            Fade.Step(random);

            // Movement.
            foreach (var cloud in clouds)
                cloud.X += CloudSpeed;
        }

        // obj_typewriter_intro's alarm 0. The hold alarm is set here and counts from the next step.
        private void Type()
        {
            if (Typed < Text.Length)
            {
                Typed++;
            }
            else if (!Complete)
            {
                Complete = true;
                holdAlarm = HoldSteps;
            }
            typeAlarm = TypeSpeed;
        }

        private int NextCloudY() => random.Next(-CloudHeight / 2, RoomHeight - CloudHeight / 2 + 1);

        // The step the typewriter's `characters`-th character appears on (1 the first), counted from the room's first step.
        public static long StepTyping(int characters) => TypewriterDelay + (long)TypeSpeed * characters;

        // The step the loading image starts to fade in on when nothing is clicked, for a text of `length` characters.
        public static long StepFadingIn(int length) => StepTyping(length + 1) + HoldSteps;
    }
}
