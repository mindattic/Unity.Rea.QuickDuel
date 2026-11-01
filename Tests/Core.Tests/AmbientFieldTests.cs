using System;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The arenas' ambient emitters (UQD-T49), figure by figure against BushidoCoda.gmx.
    public class AmbientFieldTests
    {
        private static AmbientField Run(AmbientKind kind, int steps, int seed = 1, bool frozen = false)
        {
            var field = new AmbientField(kind, new Random(seed));
            field.Advance(steps, frozen);
            return field;
        }

        [Test]
        public void The_names_and_emitters_are_the_originals()
        {
            Assert.That(AmbientKinds.Names, Is.EqualTo(new[] { "none", "sand", "butterfly", "snow", "fog", "fly", "rain", "leaf", "firefly" }));
            foreach (AmbientKind kind in Enum.GetValues(typeof(AmbientKind)))
            {
                Assert.That(AmbientKinds.TryParse(AmbientKinds.NameOf(kind), out var parsed), Is.True);
                Assert.That(parsed, Is.EqualTo(kind));
                if (kind != AmbientKind.None)
                    Assert.That(AmbientKinds.FromEmitter(AmbientKinds.EmitterOf(kind)), Is.EqualTo(kind));
            }
            Assert.That(AmbientKinds.TryParse("hail", out _), Is.False);
            Assert.That(AmbientKinds.FromEmitter("obj_blood_emitter"), Is.EqualTo(AmbientKind.None));
            Assert.That(AmbientKinds.FromEmitter("obj_sand_emitter"), Is.EqualTo(AmbientKind.Sand));
            Assert.That(Enum.GetValues(typeof(AmbientKind)).Cast<AmbientKind>().Where(AmbientKinds.Unused),
                Is.EqualTo(new[] { AmbientKind.Rain, AmbientKind.Leaf, AmbientKind.Firefly }));
        }

        [Test]
        public void Depth_minus_1_draws_over_the_fighters_and_depth_0_behind()
        {
            Assert.That(AmbientKinds.InFront(AmbientKind.Sand), Is.True);
            Assert.That(AmbientKinds.InFront(AmbientKind.Snow), Is.True);
            Assert.That(AmbientKinds.InFront(AmbientKind.Fog), Is.True);
            Assert.That(AmbientKinds.InFront(AmbientKind.Rain), Is.True);
            Assert.That(AmbientKinds.InFront(AmbientKind.Leaf), Is.True);
            Assert.That(AmbientKinds.InFront(AmbientKind.Butterfly), Is.False);
            Assert.That(AmbientKinds.InFront(AmbientKind.Fly), Is.False);
            Assert.That(AmbientKinds.InFront(AmbientKind.Firefly), Is.False);
        }

        [Test]
        public void Still_air_has_nothing()
        {
            var field = Run(AmbientKind.None, 600);
            Assert.That(field.Particles, Is.Empty);
            Assert.That(field.Created, Is.Zero);
        }

        [Test]
        public void The_flocks_are_made_at_once_and_the_streams_a_step_at_a_time()
        {
            Assert.That(new AmbientField(AmbientKind.Butterfly).Particles, Has.Count.EqualTo(12));
            Assert.That(new AmbientField(AmbientKind.Fog).Particles, Has.Count.EqualTo(32));
            Assert.That(new AmbientField(AmbientKind.Fly).Particles, Has.Count.EqualTo(32));
            Assert.That(new AmbientField(AmbientKind.Firefly).Particles, Has.Count.EqualTo(28));
            foreach (var stream in new[] { AmbientKind.Sand, AmbientKind.Snow, AmbientKind.Rain, AmbientKind.Leaf })
                Assert.That(new AmbientField(stream).Particles, Is.Empty, stream.ToString());

            Assert.That(Run(AmbientKind.Sand, 10).Created, Is.EqualTo(20), "two grains a step");
            Assert.That(Run(AmbientKind.Snow, 10).Created, Is.EqualTo(10));
            Assert.That(Run(AmbientKind.Rain, 10).Created, Is.EqualTo(10));
            Assert.That(Run(AmbientKind.Leaf, 10).Created, Is.EqualTo(10));
            Assert.That(Run(AmbientKind.Butterfly, 100).Particles, Has.Count.EqualTo(12), "a flock is never added to");
        }

        [Test]
        public void Sand_blows_in_from_the_left_spinning_and_leaves_off_the_bottom_or_the_right()
        {
            var field = new AmbientField(AmbientKind.Sand, new Random(3));
            field.Advance(1, false);
            foreach (var grain in field.Particles)
            {
                // Made at x -320..0, y -240..480, then one step on.
                Assert.That(grain.DX, Is.InRange(3, 6));
                Assert.That(grain.DY, Is.InRange(-1, 6));
                Assert.That(grain.DY, Is.Not.Zero, "a still grain is made to fall 2");
                Assert.That(grain.X - grain.DX, Is.InRange(-320, 0));
                Assert.That(grain.Y - grain.DY, Is.InRange(-240, 480));
                Assert.That(grain.Alpha, Is.EqualTo(0.8));
                Assert.That(grain.Angle, Is.EqualTo(-1), "image_angle -= 1 a step");
                Assert.That(grain.Sprite, Is.AnyOf(AmbientField.Sand1, AmbientField.Sand2));
            }
            field.Advance(2000, false);
            Assert.That(field.Particles.All(g => g.Y <= 480 && g.X <= 644), "every grain past an edge is gone");
            Assert.That(field.Particles.Count, Is.LessThan(2 * (644 + 320) / 3 + 2), "none outlives its crossing");
        }

        [Test]
        public void Snow_falls_from_above_1_or_2_a_step_and_melts_below_the_screen()
        {
            var field = Run(AmbientKind.Snow, 1, seed: 4);
            var flake = field.Particles.Single();
            Assert.That(flake.DY, Is.InRange(1, 2), "never a flake that hangs for ever");
            Assert.That(flake.DX, Is.InRange(-2, 2));
            Assert.That(flake.Y - flake.DY, Is.InRange(-480, -9), "made above the screen");
            Assert.That(flake.X - flake.DX, Is.InRange(0, 640));
            Assert.That(flake.Alpha, Is.EqualTo(0.8));
            Assert.That(flake.Sprite, Is.AnyOf(AmbientField.Snow1, AmbientField.Snow2));

            field.Advance(3000, false);
            Assert.That(field.Particles.All(f => f.Y <= 480));
            Assert.That(field.Particles.Count, Is.LessThanOrEqualTo(960), "a flake is gone within 960 steps");
            Assert.That(field.Particles.Count(f => f.Y >= 0), Is.GreaterThan(100), "a steady fall on screen");
        }

        [Test]
        public void Rain_streaks_down_15_to_20_a_step_slanting_left()
        {
            var field = Run(AmbientKind.Rain, 1, seed: 5);
            var drop = field.Particles.Single();
            Assert.That(drop.DY, Is.InRange(15, 20));
            Assert.That(drop.DX, Is.InRange(-2, -1));
            Assert.That(drop.Alpha, Is.EqualTo(1));
            Assert.That(drop.Angle, Is.Zero, "raindrops do not spin");
            field.Advance(200, false);
            Assert.That(field.Particles.Count, Is.LessThanOrEqualTo(64), "gone within 960 / 15 steps");
        }

        [Test]
        public void Leaves_fall_as_snow_does_with_the_leaf_sprites_not_rain()
        {
            var field = Run(AmbientKind.Leaf, 200, seed: 6);
            Assert.That(field.Particles.All(l => l.Sprite == AmbientField.Leaf1 || l.Sprite == AmbientField.Leaf2));
            Assert.That(field.Particles.Select(l => l.Sprite).Distinct().Count(), Is.EqualTo(2));
            Assert.That(field.Particles.All(l => l.DY >= 1 && l.DY <= 2 && l.Alpha == 0.8));
        }

        [Test]
        public void Fog_drifts_low_across_the_screen_and_comes_back_round()
        {
            var field = new AmbientField(AmbientKind.Fog, new Random(7));
            foreach (var cloud in field.Particles)
            {
                Assert.That(cloud.Y, Is.InRange(320, 456), "vertical_position down to room_height - sprite_height/2");
                Assert.That(cloud.X, Is.InRange(-320, 960));
                Assert.That(cloud.Alpha, Is.EqualTo(0.3));
                Assert.That(cloud.Sprite, Is.EqualTo(AmbientField.Cloud));
                Assert.That(cloud.Left ? cloud.DX : -cloud.DX, Is.InRange(-2, -1));
            }
            Assert.That(field.Particles.Any(c => c.Left) && field.Particles.Any(c => !c.Left));

            field.Advance(5000, false);
            Assert.That(field.Particles, Has.Count.EqualTo(32), "clouds wrap, never go");
            foreach (var cloud in field.Particles)
            {
                Assert.That(cloud.X, Is.InRange(-256 - 2, 640 + 256 + 2));
                Assert.That(cloud.Y, Is.InRange(320, 456), "fog keeps its height");
            }
        }

        [Test]
        public void Butterflies_flutter_in_their_band_flapping_and_facing_their_way()
        {
            var field = new AmbientField(AmbientKind.Butterfly, new Random(8));
            foreach (var b in field.Particles)
            {
                Assert.That(b.Y, Is.InRange(240.0, 360.0));
                Assert.That(b.X, Is.InRange(0.0, 640.0));
                Assert.That(b.DX, Is.EqualTo(1));
                Assert.That(b.DY, Is.EqualTo(1));
                Assert.That(b.Alarm, Is.InRange(5, 29), "alarm[0] = random_range(5, 30)");
            }
            field.Advance(1, false);
            Assert.That(field.Particles.All(b => b.Frame == 1), "image_speed 1: a frame a step");
            field.Advance(5, false);
            Assert.That(field.Particles.All(b => b.Frame == 0), "6 frames");

            for (var i = 0; i < 6000; i++)
            {
                field.Advance(1, false);
                foreach (var b in field.Particles)
                {
                    Assert.That(b.Y, Is.InRange(240 - 2 * 60 - 2, 360 + 2 * 60 + 2), "turned back at the band within an alarm");
                    Assert.That(b.X, Is.InRange(-2 * 60 - 2, 640 + 2 * 60 + 2));
                    if (b.DX != 1 || b.DY != 1)
                        Assert.That(b.Mirrored, Is.EqualTo(!(b.DX > 0)));
                }
            }
            Assert.That(field.Particles.Any(b => b.Mirrored) && field.Particles.Any(b => !b.Mirrored));
        }

        [Test]
        public void Flies_buzz_in_their_band_and_no_longer_drift_off_the_bottom()
        {
            var field = new AmbientField(AmbientKind.Fly, new Random(9));
            Assert.That(field.Particles.All(f => f.Y >= 240 && f.Y <= 440));
            Assert.That(field.Particles.Select(f => f.Sprite).Distinct(), Is.EquivalentTo(new[] { AmbientField.Fly1, AmbientField.Fly2 }),
                "sprite_index, not image_index, chooses spr_fly or spr_fly2");
            for (var i = 0; i < 20000; i++)
            {
                field.Advance(1, false);
                foreach (var f in field.Particles)
                {
                    Assert.That(f.Y, Is.InRange(240 - 2 * 30 - 2, 440 + 2 * 30 + 2));
                    Assert.That(f.X, Is.InRange(-4 * 30 - 4, 640 + 4 * 30 + 4));
                    Assert.That(Math.Abs(f.DX), Is.LessThanOrEqualTo(4));
                }
            }
        }

        [Test]
        public void Fireflies_wander_slower_and_blink()
        {
            var field = new AmbientField(AmbientKind.Firefly, new Random(10));
            Assert.That(field.Particles.All(f => f.Sprite == AmbientField.Firefly), "all lit at first");
            var blinked = false;
            for (var i = 0; i < 3000; i++)
            {
                field.Advance(1, false);
                blinked |= field.Particles.Any(f => f.Sprite == AmbientField.FireflyDark);
                foreach (var f in field.Particles)
                {
                    Assert.That(Math.Abs(f.DX), Is.LessThanOrEqualTo(3));
                    Assert.That(Math.Abs(f.DY), Is.LessThanOrEqualTo(1));
                    Assert.That(f.Y, Is.InRange(240 - 30 - 1, 440 + 30 + 1));
                }
            }
            Assert.That(blinked, Is.True);
        }

        [Test]
        public void Nothing_moves_or_is_made_during_the_action_but_alarms_run_on()
        {
            foreach (AmbientKind kind in Enum.GetValues(typeof(AmbientKind)))
            {
                var field = Run(kind, 120, seed: 11);
                var before = field.Particles.Select(p => (p.X, p.Y, p.Angle, p.Frame)).ToList();
                var created = field.Created;
                field.Advance(300, frozen: true);
                Assert.That(field.Created, Is.EqualTo(created), kind + ": no particle made");
                Assert.That(field.Particles.Select(p => (p.X, p.Y, p.Angle, p.Frame)), Is.EqualTo(before), kind + ": frozen");
                Assert.That(field.Tick, Is.EqualTo(420));
            }

            // A butterfly's heading changes while frozen: its alarm still fires.
            var flock = new AmbientField(AmbientKind.Butterfly, new Random(12));
            flock.Advance(60, frozen: true);
            Assert.That(flock.Particles.Any(b => b.DX != 1 || b.DY != 1));
        }

        [Test]
        public void The_same_seed_plays_the_same_weather()
        {
            var a = Run(AmbientKind.Snow, 500, seed: 13);
            var b = Run(AmbientKind.Snow, 500, seed: 13);
            Assert.That(a.Particles.Select(p => (p.X, p.Y)), Is.EqualTo(b.Particles.Select(p => (p.X, p.Y))));
        }
    }
}
