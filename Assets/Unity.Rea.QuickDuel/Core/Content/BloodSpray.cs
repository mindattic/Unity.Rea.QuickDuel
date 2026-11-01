using System;
using System.Collections.Generic;

namespace Unity.Rea.QuickDuel.Core
{
    // The blood the aftermath spills (UQD-T40), as gameclock_aftermath.gml, obj_blood_emitter and obj_blood_drop have it.
    // Every figure is the original's own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   aftermath:  blood_emitter = instance_create(room_width/2, vertical_position, obj_blood_emitter)
    //   emitter:    rate = 2; alarm[0] = rate                     every 2 steps:
    //               blood_drop = instance_create(x, y, obj_blood_drop), its sprite spr_blood_drop1 or 2 (irandom_range(1,2))
    //   drop:       velocityX = irandom_range(-5,5); velocityY = irandom_range(2,12)
    //               if (irandom_range(1,32) == 1) velocityY = irandom_range(12,24)     the "huge spurt"
    //               rate = 2; alarm[0] = rate                     every 2 steps:
    //               x += velocityX; y -= velocityY; velocityY -= 1; y += 1   (gravity)
    //               if (y > room_height + sprite_height) instance_destroy()
    //   the emitter is destroyed when the loading fade that follows the 3 s aftermath has covered the screen
    //   (obj_fade_gamescreen, 0.005 alpha a step); drops already in the air fall on.
    //   the killing blow ends it sooner: obj_player's Animation End for a defeated fighter with no HP left destroys every
    //   blood emitter (with(obj_blood_emitter) instance_destroy()) once its defeat strip has played, 40 steps on (UQD-T64).
    //
    // Steps are the aftermath's 60 a second (room_speed = normalFPS); the caller counts them off the clock. Positions are
    // the original's room pixels, x right and y down, 640 x 480.
    public sealed class BloodSpray
    {
        public const int TicksPerSecond = 60;
        public const int RoomWidth = 640;
        public const int RoomHeight = 480;

        // room_width/2, vertical_position (room_height - room_height/3).
        public const int OriginX = RoomWidth / 2;
        public const int OriginY = RoomHeight - RoomHeight / 3;

        public const int EmitEveryTicks = 2;
        public const int MoveEveryTicks = 2;
        public const int MinVelocityX = -5;
        public const int MaxVelocityX = 5;
        public const int MinVelocityY = 2;
        public const int MaxVelocityY = 12;
        public const int SpurtOneIn = 32;
        public const int MinSpurtY = 12;
        public const int MaxSpurtY = 24;
        public const int Gravity = 1;

        // How long the emitter runs: the aftermath's 3 s (aftermath_time = 3 * room_speed), then the loading fade's
        // 1 / 0.005 = 200 steps until it has covered the screen and destroys the emitter.
        public const int AftermathTicks = 3 * TicksPerSecond;
        public const int FadeTicks = 200;
        public const int EmitTicks = AftermathTicks + FadeTicks;

        // spr_blood_drop1 is 6 x 6, spr_blood_drop2 4 x 4.
        public static readonly IReadOnlyList<int> DropHeights = new[] { 6, 4 };

        private readonly Random random;
        private readonly List<BloodDrop> drops = new List<BloodDrop>();
        private bool stopped;
        private int? lastSpurtTick;

        public BloodSpray(Random random = null)
        {
            this.random = random ?? new Random();
        }

        // Steps since the emitter was created.
        public int Tick { get; private set; }

        // Every drop still on screen (or above it), oldest first.
        public IReadOnlyList<BloodDrop> Drops => drops;

        // Whether the emitter still spurts.
        public bool Emitting => !stopped && Tick < EmitTicks && (!lastSpurtTick.HasValue || Tick <= lastSpurtTick.Value);

        // Every drop it has spurted.
        public int Spurted { get; private set; }

        // Whether anything is left to draw.
        public bool Done => !Emitting && drops.Count == 0;

        // The emitter is destroyed (the next round has begun); drops in the air fall on.
        public void Stop() => stopped = true;

        // The emitter is destroyed at the end of step `tick`, after that step's spurt: the killing blow's defeat strip
        // reaching its Animation End (FighterMotion.StripTicks).
        public void StopAfter(int tick) => lastSpurtTick = tick;

        // Runs the spray on to `tick` steps after it began; earlier ticks do nothing.
        public void AdvanceTo(int tick)
        {
            while (Tick < tick)
                Step();
        }

        private void Step()
        {
            Tick++;
            // Each drop's own alarm: every 2 steps from its creation.
            for (var i = drops.Count - 1; i >= 0; i--)
            {
                var drop = drops[i];
                if ((Tick - drop.Born) % MoveEveryTicks != 0)
                    continue;
                drop.Move();
                if (drop.Y > RoomHeight + DropHeights[drop.Sprite])
                    drops.RemoveAt(i);
            }
            // The emitter's alarm: every 2 steps from its creation.
            if (Emitting && Tick % EmitEveryTicks == 0)
                Spurt();
        }

        private void Spurt()
        {
            var velocityX = Range(MinVelocityX, MaxVelocityX);
            var velocityY = Range(MinVelocityY, MaxVelocityY);
            if (Range(1, SpurtOneIn) == 1)
                velocityY = Range(MinSpurtY, MaxSpurtY);
            var sprite = Range(1, 2) - 1;
            drops.Add(new BloodDrop(OriginX, OriginY, velocityX, velocityY, sprite, Tick));
            Spurted++;
        }

        // GameMaker's irandom_range: a whole number from min to max, both included.
        private int Range(int min, int max) => random.Next(min, max + 1);

        // Whether a verdict spills blood: every round the original sent to its aftermath, which a strike always did (a
        // fair win, an early strike, and the clash a near-tie became). The rebuild's own endings, nobody striking and a
        // forfeit, have no cut and spill none.
        public static bool Spills(DuelResult verdict) =>
            verdict != null && (verdict.Outcome == DuelOutcome.FasterStrike || verdict.Outcome == DuelOutcome.EarlyStrike
                                || verdict.Outcome == DuelOutcome.Tie);
    }

    // One obj_blood_drop: where it is (room pixels, y down), its velocity and its sprite (0: spr_blood_drop1, 1: 2).
    public sealed class BloodDrop
    {
        internal BloodDrop(int x, int y, int velocityX, int velocityY, int sprite, int born)
        {
            X = x;
            Y = y;
            VelocityX = velocityX;
            VelocityY = velocityY;
            Sprite = sprite;
            Born = born;
        }

        public int X { get; private set; }
        public int Y { get; private set; }
        public int VelocityX { get; }
        public int VelocityY { get; private set; }
        public int Sprite { get; }
        public int Born { get; }

        internal void Move()
        {
            X += VelocityX;
            Y -= VelocityY;
            VelocityY -= BloodSpray.Gravity;
            Y += BloodSpray.Gravity;
        }
    }
}
