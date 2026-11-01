using System;
using System.Collections.Generic;

namespace Unity.Rea.QuickDuel.Core
{
    // The loser's weapon thrown from the cut (UQD-T41), as gameclock_aftermath.gml, spawn_weapons.gml and obj_weapon have
    // it. Every figure is the original's own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   aftermath:  determine_winner(); spawn_weapons()
    //   spawn:      Myrmidon: weapon2 = instance_create(room_width/2, vertical_position, obj_weapon), the shield
    //               Ronin:    weapon2 the same way, a second sword
    //               then weapon = instance_create(room_width/2, vertical_position, obj_weapon), the loser's own weapon
    //               each: if (last_loser == 1) image_xscale = -1
    //   create:     velocityX = irandom_range(5,6); if (last_winner == 0) { velocityX = -velocityX; spin = 15 } else spin = -15
    //               velocityY = irandom_range(12,20); rate = 2; alarm[0] = rate
    //               landY = irandom_range(ground_level - 10, ground_level)     ground_level = vertical_position + 128/2
    //   alarm[0]:   if (y < landY) { image_angle += spin; x += velocityX; y -= velocityY; velocityY -= 1; y += 1; alarm[0] = rate }
    //               else { play one of sfx_hitground1 to 4; sprite_index = weapon_asset_ground[weapon_id]; image_angle = 0 }
    //   step:       if (y < landY) image_angle += spin
    //   obj_fade_gamescreen / obj_fade: with (obj_weapon) instance_destroy()   (the next round's loading fade)
    //
    // So each piece spins 15 degrees every step and 15 more on every second step as it moves, rises and falls under a
    // gravity of 1, lands at the ground line less 0 to 10 pixels, and lies there as its ground sprite.
    //
    // Steps are the aftermath's 60 a second (room_speed = normalFPS); the caller counts them off the clock. Positions are
    // the original's room pixels, x right and y down, 640 x 480; angles are GameMaker's degrees, counter-clockwise.
    public sealed class WeaponToss
    {
        public const int TicksPerSecond = 60;

        // room_width/2, vertical_position (room_height - room_height/3).
        public const int OriginX = BloodSpray.OriginX;
        public const int OriginY = BloodSpray.OriginY;

        // ground_level: vertical_position + sprite_get_height(spr_character_myrmidon_idle)/2, the fighters' feet.
        public const int FighterHeight = 128;
        public const int GroundLevel = OriginY + FighterHeight / 2;
        public const int MaxLandAbove = 10;

        public const int MoveEveryTicks = 2;
        public const int MinVelocityX = 5;
        public const int MaxVelocityX = 6;
        public const int MinVelocityY = 12;
        public const int MaxVelocityY = 20;
        public const int Spin = 15;
        public const int Gravity = 1;

        // sfx_hitground1 to 4.
        public const int HitGroundSounds = 4;

        // The fighters spawn_weapons.gml throws a second piece for (load_characters.gml's ids, Data/assets.json's keys).
        public const string Myrmidon = "myrmidon";
        public const string Ronin = "ronin";

        private readonly List<ThrownWeapon> pieces = new List<ThrownWeapon>();

        // The pieces `loser` (a fighter id from Data/assets.json) drops when it loses on `loserSide`, back to front: the
        // Myrmidon's shield or the Ronin's second sword first, then the weapon in hand.
        public WeaponToss(string loser, Side loserSide, Random random = null)
        {
            random = random ?? new Random();
            Loser = loser;
            LoserSide = loserSide;
            foreach (var kind in PiecesOf(loser))
                pieces.Add(new ThrownWeapon(kind, loserSide, random));
        }

        public string Loser { get; }
        public Side LoserSide { get; }

        // Steps since the pieces were thrown.
        public int Tick { get; private set; }

        // Every piece, back to front (the order spawn_weapons.gml creates them).
        public IReadOnlyList<ThrownWeapon> Pieces => pieces;

        // Whether every piece lies on the ground.
        public bool Landed
        {
            get
            {
                foreach (var piece in pieces)
                    if (!piece.Landed) return false;
                return true;
            }
        }

        // What `fighter` drops, back to front: spawn_weapons.gml's shield for the Myrmidon and second sword for the Ronin,
        // then its own weapon.
        public static IReadOnlyList<WeaponPiece> PiecesOf(string fighter)
        {
            if (string.Equals(fighter, Myrmidon, StringComparison.OrdinalIgnoreCase))
                return new[] { WeaponPiece.Shield, WeaponPiece.Weapon };
            if (string.Equals(fighter, Ronin, StringComparison.OrdinalIgnoreCase))
                return new[] { WeaponPiece.Weapon, WeaponPiece.Weapon };
            return new[] { WeaponPiece.Weapon };
        }

        // Whether a verdict throws a weapon: a strike that cut down one side (a fair win or an early strike), as the
        // original's aftermath did. A tie throws nothing: the original's determine_winner left last_loser as the previous
        // round had it, so spawn_weapons threw the last round's loser's weapon (or, in the first round, the right
        // fighter's) though nobody fell, a fixed defect. Nobody striking and a forfeit have no cut and throw nothing.
        public static bool Throws(DuelResult verdict) =>
            verdict != null && verdict.Winner.HasValue
                            && (verdict.Outcome == DuelOutcome.FasterStrike || verdict.Outcome == DuelOutcome.EarlyStrike);

        // Runs the pieces on to `tick` steps after they were thrown; earlier ticks do nothing.
        public void AdvanceTo(int tick)
        {
            while (Tick < tick)
            {
                Tick++;
                foreach (var piece in pieces)
                    piece.Step(Tick);
            }
        }
    }

    // What a thrown piece is: the fighter's own weapon (weapon_asset[character_id]) or the Myrmidon's shield (id_shield).
    public enum WeaponPiece
    {
        Weapon,
        Shield
    }

    // One obj_weapon: where it is (room pixels, y down), its angle (degrees, counter-clockwise), its velocity, where it
    // lands, and once it has, the step it landed on and the hit-ground sound it played (0 to 3: sfx_hitground1 to 4).
    public sealed class ThrownWeapon
    {
        private readonly Random random;

        internal ThrownWeapon(WeaponPiece kind, Side loserSide, Random random)
        {
            this.random = random;
            Kind = kind;
            X = WeaponToss.OriginX;
            Y = WeaponToss.OriginY;
            // last_winner == 0 (the left won, the right lost): thrown to the left, spinning counter-clockwise.
            var leftWon = loserSide == Side.Right;
            var velocityX = Range(WeaponToss.MinVelocityX, WeaponToss.MaxVelocityX);
            VelocityX = leftWon ? -velocityX : velocityX;
            SpinPerStep = leftWon ? WeaponToss.Spin : -WeaponToss.Spin;
            VelocityY = Range(WeaponToss.MinVelocityY, WeaponToss.MaxVelocityY);
            LandY = Range(WeaponToss.GroundLevel - WeaponToss.MaxLandAbove, WeaponToss.GroundLevel);
            // if (last_loser == 1) image_xscale = -1
            XScale = loserSide == Side.Right ? -1 : 1;
        }

        public WeaponPiece Kind { get; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Angle { get; private set; }
        public int VelocityX { get; }
        public int VelocityY { get; private set; }
        public int SpinPerStep { get; }
        public int LandY { get; }
        public int XScale { get; }

        // Whether it lies on the ground (drawn as its ground sprite, unrotated).
        public bool Landed { get; private set; }

        // The step it landed on, and the sound it played then; -1 while it flies.
        public int LandedAt { get; private set; } = -1;
        public int Sound { get; private set; } = -1;

        // One step: the alarm on every second step from its creation (it is not re-armed once landed), then the step event.
        internal void Step(int tick)
        {
            if (!Landed && tick % WeaponToss.MoveEveryTicks == 0)
            {
                if (Y < LandY)
                {
                    Angle += SpinPerStep;
                    X += VelocityX;
                    Y -= VelocityY;
                    VelocityY -= WeaponToss.Gravity;
                    Y += WeaponToss.Gravity;
                }
                else
                {
                    // choose(sfx_hitground1, sfx_hitground2, sfx_hitground3, sfx_hitground4)
                    Sound = random.Next(WeaponToss.HitGroundSounds);
                    Landed = true;
                    LandedAt = tick;
                    Angle = 0;
                }
            }
            if (Y < LandY)
                Angle += SpinPerStep;
        }

        // GameMaker's irandom_range: a whole number from min to max, both included.
        private int Range(int min, int max) => random.Next(min, max + 1);
    }
}
