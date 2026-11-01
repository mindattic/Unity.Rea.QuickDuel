using System;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    public class WeaponTossTests
    {
        [Test]
        public void Pieces_are_thrown_from_the_centre_of_the_fighters_line_and_land_by_the_ground_line()
        {
            // room_width/2, vertical_position; ground_level = vertical_position + 128/2.
            Assert.That(WeaponToss.OriginX, Is.EqualTo(320));
            Assert.That(WeaponToss.OriginY, Is.EqualTo(320));
            Assert.That(WeaponToss.GroundLevel, Is.EqualTo(384));
            for (var seed = 0; seed < 200; seed++)
            {
                var piece = new WeaponToss("exile", Side.Right, new Random(seed)).Pieces.Single();
                Assert.That(piece.X, Is.EqualTo(320));
                Assert.That(piece.Y, Is.EqualTo(320));
                Assert.That(piece.LandY, Is.InRange(374, 384));
                Assert.That(Math.Abs(piece.VelocityX), Is.InRange(5, 6));
                Assert.That(piece.VelocityY, Is.InRange(12, 20));
            }
        }

        [Test]
        public void The_Myrmidon_drops_its_shield_and_the_Ronin_a_second_sword_behind_the_weapon_in_hand()
        {
            Assert.That(WeaponToss.PiecesOf("myrmidon"), Is.EqualTo(new[] { WeaponPiece.Shield, WeaponPiece.Weapon }));
            Assert.That(WeaponToss.PiecesOf("ronin"), Is.EqualTo(new[] { WeaponPiece.Weapon, WeaponPiece.Weapon }));
            foreach (var id in new[] { "gypsy", "vagabond", "exile" })
                Assert.That(WeaponToss.PiecesOf(id), Is.EqualTo(new[] { WeaponPiece.Weapon }));
            Assert.That(new WeaponToss("myrmidon", Side.Left, new Random(1)).Pieces, Has.Count.EqualTo(2));
        }

        [Test]
        public void A_right_loser_is_mirrored_and_its_weapon_flies_left_spinning_counter_clockwise()
        {
            // last_winner == 0: velocityX = -velocityX, spin = 15; last_loser == 1: image_xscale = -1.
            var right = new WeaponToss("gypsy", Side.Right, new Random(3)).Pieces.Single();
            Assert.That(right.VelocityX, Is.LessThan(0));
            Assert.That(right.SpinPerStep, Is.EqualTo(15));
            Assert.That(right.XScale, Is.EqualTo(-1));

            var left = new WeaponToss("gypsy", Side.Left, new Random(3)).Pieces.Single();
            Assert.That(left.VelocityX, Is.GreaterThan(0));
            Assert.That(left.SpinPerStep, Is.EqualTo(-15));
            Assert.That(left.XScale, Is.EqualTo(1));
        }

        [Test]
        public void It_spins_every_step_and_moves_under_gravity_every_second_step()
        {
            var toss = new WeaponToss("vagabond", Side.Right, new Random(5));
            var piece = toss.Pieces.Single();
            var vx = piece.VelocityX;
            var vy = piece.VelocityY;

            toss.AdvanceTo(1);
            Assert.That(piece.Angle, Is.EqualTo(15), "the step event alone");
            Assert.That(piece.X, Is.EqualTo(320));
            Assert.That(piece.Y, Is.EqualTo(320));

            toss.AdvanceTo(2);
            Assert.That(piece.Angle, Is.EqualTo(45), "the alarm's spin and the step's");
            Assert.That(piece.X, Is.EqualTo(320 + vx));
            Assert.That(piece.Y, Is.EqualTo(320 - vy + 1));
            Assert.That(piece.VelocityY, Is.EqualTo(vy - 1));

            toss.AdvanceTo(4);
            Assert.That(piece.X, Is.EqualTo(320 + 2 * vx));
            Assert.That(piece.Y, Is.EqualTo(320 - vy + 1 - (vy - 1) + 1));
            Assert.That(piece.Angle, Is.EqualTo(90));
        }

        [Test]
        public void It_lands_below_its_mark_once_with_one_of_four_sounds_and_lies_unrotated()
        {
            var sounds = new bool[WeaponToss.HitGroundSounds];
            for (var seed = 0; seed < 200; seed++)
            {
                var toss = new WeaponToss("exile", Side.Left, new Random(seed));
                var piece = toss.Pieces.Single();
                var lastY = piece.Y;
                var tick = 0;
                while (!piece.Landed)
                {
                    tick++;
                    toss.AdvanceTo(tick);
                    Assert.That(tick, Is.LessThan(200), "it comes down");
                    if (!piece.Landed) lastY = piece.Y;
                }
                Assert.That(piece.LandedAt, Is.EqualTo(tick));
                Assert.That(tick % 2, Is.EqualTo(0), "landing is the alarm's");
                Assert.That(piece.Y, Is.GreaterThanOrEqualTo(piece.LandY));
                Assert.That(piece.Y, Is.EqualTo(lastY), "the landing step does not move it");
                Assert.That(piece.Angle, Is.EqualTo(0));
                Assert.That(piece.Sound, Is.InRange(0, 3));
                sounds[piece.Sound] = true;

                var x = piece.X;
                toss.AdvanceTo(tick + 100);
                Assert.That(piece.X, Is.EqualTo(x));
                Assert.That(piece.Angle, Is.EqualTo(0));
                Assert.That(piece.LandedAt, Is.EqualTo(tick), "it lands once");
                Assert.That(toss.Landed, Is.True);
            }
            Assert.That(sounds, Is.All.True, "choose() picks any of the four");
        }

        [Test]
        public void Only_a_strike_that_cut_someone_down_throws_a_weapon()
        {
            Assert.That(WeaponToss.Throws(new DuelResult(DuelOutcome.FasterStrike, Side.Left, 0.2, 0.3)), Is.True);
            Assert.That(WeaponToss.Throws(new DuelResult(DuelOutcome.EarlyStrike, Side.Right, -0.1, null)), Is.True);
            Assert.That(WeaponToss.Throws(new DuelResult(DuelOutcome.Tie, null, 0.2, 0.2)), Is.False);
            Assert.That(WeaponToss.Throws(new DuelResult(DuelOutcome.NoStrike, null, null, null)), Is.False);
            Assert.That(WeaponToss.Throws(new DuelResult(DuelOutcome.Forfeit, Side.Left, null, null)), Is.False);
            Assert.That(WeaponToss.Throws(null), Is.False);
        }
    }
}
