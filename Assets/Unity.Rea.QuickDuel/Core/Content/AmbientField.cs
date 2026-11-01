using System;
using System.Collections.Generic;

namespace Unity.Rea.QuickDuel.Core
{
    // The ambient effect an arena plays (UQD-T49): which of the original's particle emitters setup_arena_properties.gml
    // created for it, or none. The first five are the ones the original placed; rain, leaf and firefly are the original's
    // emitters no arena ever created, kept as options an arena's data can name.
    public enum AmbientKind
    {
        None,
        Sand,
        Butterfly,
        Snow,
        Fog,
        Fly,
        Rain,
        Leaf,
        Firefly,
    }

    // The ambient effects by the names the asset manifest gives them, and what each draws.
    public static class AmbientKinds
    {
        // Indexed by AmbientKind.
        private static readonly string[] names = { "none", "sand", "butterfly", "snow", "fog", "fly", "rain", "leaf", "firefly" };

        // The emitter object each kind was in BushidoCoda.gmx, indexed by AmbientKind.
        private static readonly string[] emitters =
        {
            null, "obj_sand_emitter", "obj_butterfly_emitter", "obj_snow_emitter", "obj_fog_emitter", "obj_fly_emitter",
            "obj_rain_emitter", "obj_leaf_emitter", "obj_firefly_emitter",
        };

        // The sprites each kind draws, indexed by AmbientKind. Fog draws the intro's spr_cloud1 (manifest group "cloud");
        // every other one is in the "ambient" group.
        private static readonly string[][] sprites =
        {
            new string[0],
            new[] { AmbientField.Sand1, AmbientField.Sand2 },
            new[] { AmbientField.Butterfly },
            new[] { AmbientField.Snow1, AmbientField.Snow2 },
            new[] { AmbientField.Cloud },
            new[] { AmbientField.Fly1, AmbientField.Fly2 },
            new[] { AmbientField.Rain },
            new[] { AmbientField.Leaf1, AmbientField.Leaf2 },
            new[] { AmbientField.Firefly, AmbientField.FireflyDark },
        };

        // Every name, "none" first.
        public static IReadOnlyList<string> Names => names;

        public static string NameOf(AmbientKind kind) => names[(int)kind];

        public static bool TryParse(string name, out AmbientKind kind)
        {
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i] != name) continue;
                kind = (AmbientKind)i;
                return true;
            }
            kind = AmbientKind.None;
            return false;
        }

        // The kind whose emitter object is `emitter` ("obj_sand_emitter"); None for any other name.
        public static AmbientKind FromEmitter(string emitter)
        {
            for (var i = 1; i < emitters.Length; i++)
                if (emitters[i] == emitter)
                    return (AmbientKind)i;
            return AmbientKind.None;
        }

        public static string EmitterOf(AmbientKind kind) => emitters[(int)kind];

        public static IReadOnlyList<string> SpritesOf(AmbientKind kind) => sprites[(int)kind];

        // Whether the original had the emitter but no arena created it.
        public static bool Unused(AmbientKind kind) => kind == AmbientKind.Rain || kind == AmbientKind.Leaf || kind == AmbientKind.Firefly;

        // Whether its particles draw over the fighters: depth -1 (obj_sand, obj_snow, obj_fog, obj_raindrop) against the
        // fighters' 0. The butterflies, flies and fireflies share the fighters' depth 0 and are drawn behind them.
        public static bool InFront(AmbientKind kind) =>
            kind == AmbientKind.Sand || kind == AmbientKind.Snow || kind == AmbientKind.Fog || kind == AmbientKind.Rain
            || kind == AmbientKind.Leaf;
    }

    // An arena's ambient effect (UQD-T49), as BushidoCoda.gmx's emitters and their particles have it. Every figure is the
    // original's own:
    //
    //   obj_sand_emitter    every step: instance_create(0,0,obj_sand) twice
    //   obj_sand            x = irandom_range(-room_width/2, 0); y = irandom_range(-room_height/2, room_height)
    //                       image_alpha = 0.8; sprite_index = choose(spr_sand1, spr_sand2)
    //                       dY = irandom_range(-1, 6), 0 made 2; dX = irandom_range(3, 6)
    //                       step: x += dX; y += dY; image_angle -= 1; destroyed once y > room_height
    //   obj_butterfly_emitter  create: repeat(12) instance_create(0,0,obj_butterfly)
    //   obj_butterfly       x = random_range(0, room_width); y = random_range(room_height - 120, room_height/2)
    //                       image_speed = 1 (spr_butterfly's 6 frames, one a step); dX = dY = 1
    //                       alarm[0] = random_range(5, 30); alarm 0: ai_butterfly, alarm[0] = random_range(5, 60)
    //                       ai_butterfly: dX, dY = random_range(-2, 2); y < room_height/2: dY = 2;
    //                       y > room_height - 120: dY = -2; x < 10: dX = 2; x > room_width - 10: dX = -2;
    //                       image_xscale = dX > 0 ? 1 : -1
    //                       step: x += dX; y += dY; image_speed = 1 (0 during the action: the wings stop too)
    //   obj_snow_emitter    every step: instance_create(0,0,obj_snow)
    //   obj_snow            x = irandom_range(0, room_width); y = irandom_range(-sprite_height, -room_height)
    //                       image_alpha = 0.8; sprite_index = choose(spr_snow1, spr_snow2)
    //                       dY = irandom_range(0.5, 2); dX = irandom_range(-2, 2)
    //                       step: x += dX; y += dY; image_angle -= 1; destroyed once y > room_height
    //   obj_fog_emitter     create: repeat(32) instance_create(0,0,obj_fog)
    //   obj_fog             dir left or right (irandom_range(1,2)); dX = irandom_range(-1,-2) left, irandom_range(1,2) right
    //                       x = irandom_range(-room_width/2, room_width + room_width/2)
    //                       y = irandom_range(vertical_position, room_height - sprite_height/2); image_alpha = 0.3
    //                       step: x += dX; a left cloud past x < -sprite_width comes back at room_width + irandom_range(1,
    //                       room_width/2) with a new dX, a right one past room_width + sprite_width at -irandom_range(1,
    //                       room_width/2)
    //   obj_fly_emitter     create: repeat(32) instance_create(0,0,obj_fly)
    //   obj_fly             x = random_range(0, room_width); y = random_range(room_height - 40, room_height/2); dX = dY = 1
    //                       alarm[0] = random_range(5, 30); alarm 0: ai_fly, alarm[0] = random_range(5, 30)
    //                       ai_fly: dX = random_range(-4, 4); dY = random_range(-2, 2); y < room_height/2: dY = 1;
    //                       x < 10: dX = 4; x > room_width - 10: dX = -4
    //                       step: x += dX; y += dY
    //   obj_rain_emitter    every step: instance_create(0,0,obj_raindrop)                         (no arena created it)
    //   obj_raindrop        x = irandom_range(0, room_width); y = irandom_range(-sprite_height, -room_height); alpha 1
    //                       dY = irandom_range(15, 20); dX = irandom_range(-1, -2)
    //                       step: x += dX; y += dY; destroyed once y > room_height
    //   obj_leaf_emitter    every step: instance_create(0,0,obj_raindrop)                         (no arena created it)
    //   obj_firefly_emitter create: repeat(28) instance_create(0,0,obj_firefly)                   (no arena created it)
    //   obj_firefly         as obj_fly; ai_firefly: dX = random_range(-3, 3); dY = random_range(-1, 1); y < room_height/2:
    //                       dY = 1; x < 10: dX = 3; x > room_width - 10: dX = -3; one alarm in five (irandom(4) == 4)
    //                       swaps spr_firefly and spr_firefly_dark (the blink)
    //
    // Every particle's step is inside if (gameclock.mode != "action"): nothing moves while the duel waits on the first
    // strike (DuelPhase.Strike), though the alarms run on. Fixed defects, each in the BIBLE: the emitters themselves had no
    // such check and, stepping at the action's 1000 fps, piled up frozen particles off screen (2 sand or 1 snowflake a
    // millisecond), so here they wait with their particles; irandom_range(0.5, 2) could give a snowflake no fall at all,
    // to hang above the screen for ever, so a flake falls 1 or 2; obj_fly set image_index, not sprite_index, to
    // choose(spr_fly, spr_fly2), so every fly was spr_fly, and here half are spr_fly2; ai_fly and ai_firefly bound a
    // fly above (room_height/2) but not below, so the swarm drifted off the bottom of the screen in time, and here a fly
    // below its spawn band (room_height - 40) turns back up as one above it turns down; sand blown off the right edge
    // was never destroyed (only y > room_height was), and here it is; obj_leaf_emitter, its sprite spr_leaf1, created
    // raindrops, and here it lets spr_leaf1 and spr_leaf2 fall as obj_snow's flakes do (there was no obj_leaf).
    //
    // Steps are the original's 60 a second (room_speed = normalFPS); the caller counts them off the clock. Positions are
    // the original's room pixels, x right and y down, 640 x 480; angles are GameMaker's degrees, counter-clockwise.
    public sealed class AmbientField
    {
        public const int TicksPerSecond = 60;
        public const int RoomWidth = 640;
        public const int RoomHeight = 480;

        // vertical_position (globals.gml): two thirds of the way down, the fighters' line.
        public const int VerticalPosition = RoomHeight - RoomHeight / 3;

        public const string Sand1 = "spr_sand1";
        public const string Sand2 = "spr_sand2";
        public const string Butterfly = "spr_butterfly";
        public const string Snow1 = "spr_snow1";
        public const string Snow2 = "spr_snow2";
        public const string Cloud = "spr_cloud1";
        public const string Fly1 = "spr_fly";
        public const string Fly2 = "spr_fly2";
        public const string Rain = "spr_rain";
        public const string Leaf1 = "spr_leaf1";
        public const string Leaf2 = "spr_leaf2";
        public const string Firefly = "spr_firefly";
        public const string FireflyDark = "spr_firefly_dark";

        // Sprite sizes the motion reads (each .sprite.gmx): spr_sand1/2 4 x 4, spr_snow1/2 9 x 9, spr_cloud1 256 x 48,
        // spr_rain 1 x 4; spr_butterfly has 6 frames.
        public const int SandSize = 4;
        public const int SnowSize = 9;
        public const int CloudWidth = 256;
        public const int CloudHeight = 48;
        public const int RainHeight = 4;
        public const int ButterflyFrames = 6;

        public const int SandPerStep = 2;
        public const double SandAlpha = 0.8;
        public const int Butterflies = 12;
        public const double SnowAlpha = 0.8;
        public const int FogClouds = 32;
        public const double FogAlpha = 0.3;
        public const int Flies = 32;
        public const int Fireflies = 28;
        public const double RainAlpha = 1;

        // ai_butterfly's band and the flies' (the band they spawn in).
        public const int ButterflyTop = RoomHeight / 2;
        public const int ButterflyBottom = RoomHeight - 120;
        public const int FlyTop = RoomHeight / 2;
        public const int FlyBottom = RoomHeight - 40;
        public const int EdgeMargin = 10;

        private readonly Random random;
        private readonly List<AmbientParticle> particles = new List<AmbientParticle>();

        public AmbientField(AmbientKind kind, Random random = null)
        {
            Kind = kind;
            this.random = random ?? new Random();
            // The emitters that create their whole flock at once.
            switch (kind)
            {
                case AmbientKind.Butterfly:
                    for (var i = 0; i < Butterflies; i++) Add(NewButterfly());
                    break;
                case AmbientKind.Fog:
                    for (var i = 0; i < FogClouds; i++) Add(NewFog());
                    break;
                case AmbientKind.Fly:
                    for (var i = 0; i < Flies; i++) Add(NewFly(Fly1));
                    break;
                case AmbientKind.Firefly:
                    for (var i = 0; i < Fireflies; i++) Add(NewFly(Firefly));
                    break;
            }
        }

        public AmbientKind Kind { get; }

        // Steps taken, frozen or not.
        public int Tick { get; private set; }

        // Every particle, oldest first.
        public IReadOnlyList<AmbientParticle> Particles => particles;

        // Every particle an emitter has created since the field began, the first flock included.
        public int Created => created;

        private int created;

        // Runs `steps` steps; `frozen` is the original's action mode, when nothing moves or is made but alarms run.
        public void Advance(int steps, bool frozen)
        {
            for (var i = 0; i < steps; i++)
                Step(frozen);
        }

        private void Step(bool frozen)
        {
            Tick++;
            // Alarms first, as GameMaker runs them before the step event.
            foreach (var particle in particles)
                if (particle.Alarm > 0 && --particle.Alarm == 0)
                    Alarm(particle);
            if (frozen)
                return;
            Emit();
            for (var i = particles.Count - 1; i >= 0; i--)
            {
                var particle = particles[i];
                particle.X += particle.DX;
                particle.Y += particle.DY;
                if (Spins(Kind))
                    particle.Angle -= 1;
                if (Kind == AmbientKind.Butterfly)
                    particle.Frame = (particle.Frame + 1) % ButterflyFrames;
                if (Kind == AmbientKind.Fog)
                    Wrap(particle);
                if (Gone(particle))
                    particles.RemoveAt(i);
            }
        }

        private static bool Spins(AmbientKind kind) => kind == AmbientKind.Sand || kind == AmbientKind.Snow || kind == AmbientKind.Leaf;

        private void Emit()
        {
            switch (Kind)
            {
                case AmbientKind.Sand:
                    for (var i = 0; i < SandPerStep; i++) Add(NewSand());
                    break;
                case AmbientKind.Snow:
                    Add(NewFlake(Snow1, Snow2));
                    break;
                case AmbientKind.Leaf:
                    Add(NewFlake(Leaf1, Leaf2));
                    break;
                case AmbientKind.Rain:
                    Add(NewRaindrop());
                    break;
            }
        }

        private void Add(AmbientParticle particle)
        {
            particles.Add(particle);
            created++;
        }

        private bool Gone(AmbientParticle particle)
        {
            switch (Kind)
            {
                case AmbientKind.Sand:
                    return particle.Y > RoomHeight || particle.X > RoomWidth + SandSize;
                case AmbientKind.Snow:
                case AmbientKind.Leaf:
                case AmbientKind.Rain:
                    return particle.Y > RoomHeight;
                default:
                    return false;
            }
        }

        private AmbientParticle NewSand()
        {
            var dy = IRange(-1, 6);
            if (dy == 0) dy = 2;
            return new AmbientParticle(IRange(-RoomWidth / 2, 0), IRange(-RoomHeight / 2, RoomHeight), IRange(3, 6), dy,
                SandAlpha, Choose(Sand1, Sand2));
        }

        private AmbientParticle NewFlake(string a, string b) =>
            new AmbientParticle(IRange(0, RoomWidth), IRange(-SnowSize, -RoomHeight), IRange(-2, 2), IRange(1, 2), SnowAlpha,
                Choose(a, b));

        private AmbientParticle NewRaindrop() =>
            new AmbientParticle(IRange(0, RoomWidth), IRange(-RainHeight, -RoomHeight), IRange(-1, -2), IRange(15, 20), RainAlpha,
                Rain);

        private AmbientParticle NewFog()
        {
            var left = IRange(1, 2) == 1;
            var dx = left ? IRange(-1, -2) : IRange(1, 2);
            return new AmbientParticle(IRange(-RoomWidth / 2, RoomWidth + RoomWidth / 2),
                IRange(VerticalPosition, RoomHeight - CloudHeight / 2), dx, 0, FogAlpha, Cloud) { Left = left };
        }

        private void Wrap(AmbientParticle cloud)
        {
            if (cloud.Left && cloud.X < -CloudWidth)
            {
                cloud.X = RoomWidth + IRange(1, RoomWidth / 2);
                cloud.DX = IRange(-1, -2);
            }
            else if (!cloud.Left && cloud.X > RoomWidth + CloudWidth)
            {
                cloud.X = -IRange(1, RoomWidth / 2);
                cloud.DX = IRange(1, 2);
            }
        }

        private AmbientParticle NewButterfly() =>
            new AmbientParticle(Range(0, RoomWidth), Range(ButterflyBottom, ButterflyTop), 1, 1, 1, Butterfly)
            {
                Alarm = Alarm(Range(5, 30)),
            };

        // A fly or a firefly: spr_fly or spr_fly2 for a fly, spr_firefly lit for a firefly.
        private AmbientParticle NewFly(string sprite) =>
            new AmbientParticle(Range(0, RoomWidth), Range(FlyBottom, FlyTop), 1, 1, 1, sprite == Fly1 ? Choose(Fly1, Fly2) : sprite)
            {
                Alarm = Alarm(Range(5, 30)),
            };

        // The alarm event: the particle's ai script, then the alarm set again.
        private void Alarm(AmbientParticle particle)
        {
            switch (Kind)
            {
                case AmbientKind.Butterfly:
                    particle.DX = Range(-2, 2);
                    particle.DY = Range(-2, 2);
                    if (particle.Y < ButterflyTop) particle.DY = 2;
                    else if (particle.Y > ButterflyBottom) particle.DY = -2;
                    if (particle.X < EdgeMargin) particle.DX = 2;
                    else if (particle.X > RoomWidth - EdgeMargin) particle.DX = -2;
                    particle.Mirrored = !(particle.DX > 0);
                    particle.Alarm = Alarm(Range(5, 60));
                    break;
                case AmbientKind.Fly:
                    Wander(particle, 4, 2, 1);
                    particle.Alarm = Alarm(Range(5, 30));
                    break;
                case AmbientKind.Firefly:
                    Wander(particle, 3, 1, 1);
                    if (random.Next(5) == 4)
                        particle.Sprite = particle.Sprite == Firefly ? FireflyDark : Firefly;
                    particle.Alarm = Alarm(Range(5, 30));
                    break;
            }
        }

        // ai_fly and ai_firefly: a random heading, turned back at the band's edges and the screen's sides.
        private void Wander(AmbientParticle particle, double speedX, double speedY, double turn)
        {
            particle.DX = Range(-speedX, speedX);
            particle.DY = Range(-speedY, speedY);
            if (particle.Y < FlyTop) particle.DY = turn;
            else if (particle.Y > FlyBottom) particle.DY = -turn;
            if (particle.X < EdgeMargin) particle.DX = speedX;
            else if (particle.X > RoomWidth - EdgeMargin) particle.DX = -speedX;
        }

        // An alarm set to a real number counts down whole steps (GameMaker keeps alarms as whole numbers).
        private static int Alarm(double steps) => Math.Max(1, (int)steps);

        // GameMaker's irandom_range: a whole number between a and b, both included, in either order.
        private int IRange(int a, int b) => random.Next(Math.Min(a, b), Math.Max(a, b) + 1);

        // GameMaker's random_range: a real number from a towards b.
        private double Range(double a, double b) => a + random.NextDouble() * (b - a);

        private string Choose(string a, string b) => random.Next(2) == 0 ? a : b;
    }

    // One particle: where it is (room pixels, y down), its velocity per step, its angle (degrees, counter-clockwise), its
    // alpha, its sprite and frame, and whether it is drawn mirrored (image_xscale -1).
    public sealed class AmbientParticle
    {
        internal AmbientParticle(double x, double y, double dx, double dy, double alpha, string sprite)
        {
            X = x;
            Y = y;
            DX = dx;
            DY = dy;
            Alpha = alpha;
            Sprite = sprite;
        }

        public double X { get; internal set; }
        public double Y { get; internal set; }
        public double DX { get; internal set; }
        public double DY { get; internal set; }
        public double Angle { get; internal set; }
        public double Alpha { get; }
        public string Sprite { get; internal set; }
        public int Frame { get; internal set; }
        public bool Mirrored { get; internal set; }

        // Steps until its alarm 0 fires; 0 when it has none.
        public int Alarm { get; internal set; }

        // A fog cloud's dir: drifting left (else right).
        public bool Left { get; internal set; }
    }
}
