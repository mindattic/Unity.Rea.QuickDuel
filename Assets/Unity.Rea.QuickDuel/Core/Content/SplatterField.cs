using System;
using System.Collections.Generic;

namespace Unity.Rea.QuickDuel.Core
{
    // The splatters the aftermath throws over the screen (UQD-T40), as gameclock_aftermath.gml and obj_splatter have it.
    // Every figure is the original's own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   aftermath:  repeat(irandom_range(6,12)) instance_create(0, 0, obj_splatter)
    //   create:     image_alpha = 0.95
    //               x = irandom_range(-(sprite_width/2), room_width + (sprite_width/2))
    //               y = irandom_range(-(sprite_height/2), room_width + (sprite_height/2))     (sic; see below)
    //               sprite_index = spr_splatter1 .. 10 (irandom_range(1,10)); fade_speed = 0.0003
    //   step:       if (image_alpha > 0) image_alpha -= fade_speed else instance_destroy()
    //   mouse:      left or right pressed on it, or the cursor entering it with either button held: fade_speed = 0.01
    //
    // Depth -100, over the fighters and the text; nothing destroys a splatter but its own fade, so they outlive the round.
    // Steps are 60 a second; positions are the splatter's centre (its origin, 48, 48) in room pixels, y down.
    //
    // Fixed defect: y is drawn over room_width (640), not room_height (480), so about a quarter of the original's
    // splatters landed below the bottom of the screen; the rebuild spreads them over the screen's own height (BIBLE).
    public sealed class SplatterField
    {
        public const int MinCount = 6;
        public const int MaxCount = 12;
        public const int Kinds = 10;
        public const int Size = 96;
        public const double StartAlpha = 0.95;
        public const double FadePerTick = 0.0003;
        public const double WipedFadePerTick = 0.01;

        private readonly Random random;
        private readonly List<Splatter> splatters = new List<Splatter>();

        public SplatterField(Random random = null)
        {
            this.random = random ?? new Random();
        }

        // Every splatter still on screen, oldest first.
        public IReadOnlyList<Splatter> Splatters => splatters;

        // A new 6 to 12, spread over the room; returns how many.
        public int Spill()
        {
            var count = Range(MinCount, MaxCount);
            for (var i = 0; i < count; i++)
            {
                var x = Range(-Size / 2, BloodSpray.RoomWidth + Size / 2);
                var y = Range(-Size / 2, BloodSpray.RoomHeight + Size / 2);
                var kind = Range(1, Kinds) - 1;
                splatters.Add(new Splatter(x, y, kind));
            }
            return count;
        }

        // Fades every splatter on by `ticks` steps, dropping those that are gone.
        public void Advance(int ticks)
        {
            for (var t = 0; t < ticks && splatters.Count > 0; t++)
            {
                for (var i = splatters.Count - 1; i >= 0; i--)
                    if (!splatters[i].Step())
                        splatters.RemoveAt(i);
            }
        }

        public void Clear() => splatters.Clear();

        private int Range(int min, int max) => random.Next(min, max + 1);
    }

    // One obj_splatter: its centre in room pixels (y down), which of the ten it is (0: spr_splatter1), and its alpha.
    public sealed class Splatter
    {
        private bool wasInside;

        internal Splatter(int x, int y, int kind)
        {
            X = x;
            Y = y;
            Kind = kind;
            Alpha = SplatterField.StartAlpha;
            FadePerTick = SplatterField.FadePerTick;
        }

        public int X { get; }
        public int Y { get; }
        public int Kind { get; }
        public double Alpha { get; private set; }
        public double FadePerTick { get; private set; }
        public bool Wiped => FadePerTick == SplatterField.WipedFadePerTick;

        // The pointer this frame: whether it is over the splatter (its bounding box), whether a button (left or right) is
        // held, and whether one went down this frame. A press on it, or entering it with a button held, wipes it.
        // Returns whether it is now being wiped.
        public bool Touch(bool inside, bool held, bool pressed)
        {
            if (inside && (pressed || (held && !wasInside)))
                FadePerTick = SplatterField.WipedFadePerTick;
            wasInside = inside;
            return Wiped;
        }

        // One step; false once it is gone.
        internal bool Step()
        {
            if (Alpha > 0)
            {
                Alpha = Math.Max(0, Alpha - FadePerTick);
                return true;
            }
            return false;
        }
    }
}
