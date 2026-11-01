using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    public class BloodSprayTests
    {
        [Test]
        public void The_emitter_stands_at_the_centre_of_the_fighters_line()
        {
            // room_width/2, vertical_position = room_height - room_height/3.
            Assert.That(BloodSpray.OriginX, Is.EqualTo(320));
            Assert.That(BloodSpray.OriginY, Is.EqualTo(320));
        }

        [Test]
        public void It_spurts_a_drop_every_2_steps_until_the_fade_destroys_it()
        {
            var spray = new BloodSpray(new Random(1));
            spray.AdvanceTo(1);
            Assert.That(spray.Spurted, Is.EqualTo(0), "alarm[0] = 2: nothing on the first step");
            spray.AdvanceTo(2);
            Assert.That(spray.Spurted, Is.EqualTo(1));
            spray.AdvanceTo(10);
            Assert.That(spray.Spurted, Is.EqualTo(5));
            spray.AdvanceTo(BloodSpray.EmitTicks + 100);
            Assert.That(spray.Spurted, Is.EqualTo(BloodSpray.EmitTicks / 2 - 1), "the 3 s aftermath plus the 200-step fade");
            Assert.That(spray.Emitting, Is.False);
            Assert.That(BloodSpray.EmitTicks, Is.EqualTo(380));
        }

        [Test]
        public void The_killing_blow_destroys_the_emitter_when_the_defeat_strip_ends()
        {
            // obj_player's Animation End: with(obj_blood_emitter) instance_destroy() for a loser with no HP left (UQD-T64).
            var spray = new BloodSpray(new Random(3));
            spray.StopAfter(FighterMotion.StripTicks);
            spray.AdvanceTo(FighterMotion.StripTicks);
            Assert.That(spray.Spurted, Is.EqualTo(FighterMotion.StripTicks / 2), "every 2 steps up to and including step 40");
            Assert.That(spray.Emitting, Is.True, "the spurt on the strip's last step still comes");
            spray.AdvanceTo(FighterMotion.StripTicks + 10);
            Assert.That(spray.Spurted, Is.EqualTo(FighterMotion.StripTicks / 2));
            Assert.That(spray.Emitting, Is.False);
        }

        [Test]
        public void Stopping_ends_the_spurts_but_the_drops_fall_on_and_off_the_screen()
        {
            var spray = new BloodSpray(new Random(2));
            spray.AdvanceTo(40);
            spray.Stop();
            var spurted = spray.Spurted;
            Assert.That(spray.Drops, Is.Not.Empty);
            spray.AdvanceTo(42);
            Assert.That(spray.Spurted, Is.EqualTo(spurted));
            spray.AdvanceTo(2000);
            Assert.That(spray.Drops, Is.Empty, "every drop falls off the bottom");
            Assert.That(spray.Done, Is.True);
        }

        [Test]
        public void Drops_leave_at_the_original_velocities_with_one_in_32_a_huge_spurt()
        {
            var spray = new BloodSpray(new Random(3));
            // Each drop as it leaves the emitter (its velocity changes as it falls).
            var seen = new List<(int VelocityX, int VelocityY, int Sprite)>();
            void Newest(BloodSpray s)
            {
                var newest = s.Drops[s.Drops.Count - 1];
                Assert.That((newest.X, newest.Y), Is.EqualTo((320, 320)), "each starts at the emitter");
                seen.Add((newest.VelocityX, newest.VelocityY, newest.Sprite));
            }
            for (var tick = 2; tick < BloodSpray.EmitTicks; tick += 2)
            {
                spray.AdvanceTo(tick);
                Newest(spray);
            }
            for (var run = 0; run < 40; run++)
            {
                var more = new BloodSpray(new Random(100 + run));
                for (var tick = 2; tick <= 60; tick += 2)
                {
                    more.AdvanceTo(tick);
                    Newest(more);
                }
            }
            Assert.That(seen.Min(d => d.VelocityX), Is.EqualTo(-5));
            Assert.That(seen.Max(d => d.VelocityX), Is.EqualTo(5));
            Assert.That(seen.Min(d => d.VelocityY), Is.EqualTo(2));
            Assert.That(seen.Max(d => d.VelocityY), Is.GreaterThan(12).And.LessThanOrEqualTo(24), "the huge spurt");
            var spurts = seen.Count(d => d.VelocityY > 12);
            Assert.That(spurts, Is.GreaterThan(0).And.LessThan(seen.Count / 10), "about one in 32");
            Assert.That(seen.Select(d => d.Sprite).Distinct().OrderBy(s => s), Is.EqualTo(new[] { 0, 1 }), "both drop sprites");
        }

        [Test]
        public void A_drop_moves_every_2_steps_under_gravity()
        {
            var spray = new BloodSpray(new Random(4));
            spray.AdvanceTo(2);
            var drop = spray.Drops[0];
            int vx = drop.VelocityX, vy = drop.VelocityY;
            spray.AdvanceTo(3);
            Assert.That(drop.X, Is.EqualTo(320), "its own alarm[0] = 2");
            spray.AdvanceTo(4);
            // x += vx; y -= vy; vy -= 1; y += 1
            Assert.That(drop.X, Is.EqualTo(320 + vx));
            Assert.That(drop.Y, Is.EqualTo(320 - vy + 1));
            Assert.That(drop.VelocityY, Is.EqualTo(vy - 1));
            spray.AdvanceTo(6);
            Assert.That(drop.X, Is.EqualTo(320 + 2 * vx));
            Assert.That(drop.Y, Is.EqualTo(320 - vy + 1 - (vy - 1) + 1));
        }

        [Test]
        public void Every_strike_spills_blood_but_a_round_nobody_struck_does_not()
        {
            Assert.That(BloodSpray.Spills(new DuelResult(DuelOutcome.FasterStrike, Side.Left, 0.2, 0.3)), Is.True);
            Assert.That(BloodSpray.Spills(new DuelResult(DuelOutcome.EarlyStrike, Side.Left, null, -0.1)), Is.True);
            Assert.That(BloodSpray.Spills(new DuelResult(DuelOutcome.Tie, null, 0.2, 0.2)), Is.True, "the original's clash");
            Assert.That(BloodSpray.Spills(new DuelResult(DuelOutcome.NoStrike, null, null, null)), Is.False);
            Assert.That(BloodSpray.Spills(new DuelResult(DuelOutcome.Forfeit, Side.Left, null, null)), Is.False);
            Assert.That(BloodSpray.Spills(null), Is.False);
        }
    }

    public class SplatterFieldTests
    {
        [Test]
        public void Six_to_twelve_splatters_of_ten_kinds_are_spread_over_the_screen()
        {
            var counts = new HashSet<int>();
            var all = new List<Splatter>();
            for (var seed = 0; seed < 200; seed++)
            {
                var field = new SplatterField(new Random(seed));
                var count = field.Spill();
                Assert.That(field.Splatters, Has.Count.EqualTo(count));
                counts.Add(count);
                all.AddRange(field.Splatters);
            }
            Assert.That(counts.Min(), Is.EqualTo(6));
            Assert.That(counts.Max(), Is.EqualTo(12));
            Assert.That(all.Select(s => s.Kind).Distinct().Count(), Is.EqualTo(10));
            Assert.That(all.Min(s => s.X), Is.GreaterThanOrEqualTo(-48));
            Assert.That(all.Max(s => s.X), Is.LessThanOrEqualTo(640 + 48));
            Assert.That(all.Min(s => s.Y), Is.GreaterThanOrEqualTo(-48));
            Assert.That(all.Max(s => s.Y), Is.LessThanOrEqualTo(480 + 48), "the room's height, not its width (fixed defect)");
            Assert.That(all.All(s => s.Alpha == 0.95));
        }

        [Test]
        public void A_splatter_fades_0_0003_a_step_and_is_gone_in_about_53_seconds()
        {
            var field = new SplatterField(new Random(5));
            field.Spill();
            var splatter = field.Splatters[0];
            field.Advance(100);
            Assert.That(splatter.Alpha, Is.EqualTo(0.95 - 100 * 0.0003).Within(1e-9));
            field.Advance(3000);
            Assert.That(field.Splatters, Is.Not.Empty);
            field.Advance(68);
            Assert.That(field.Splatters, Is.Empty, "0.95 / 0.0003 = 3167 steps");
        }

        [Test]
        public void A_click_on_it_or_entering_it_with_a_button_held_wipes_it_quickly()
        {
            var field = new SplatterField(new Random(6));
            field.Spill();
            var a = field.Splatters[0];
            Assert.That(a.Touch(inside: false, held: true, pressed: true), Is.False, "a click elsewhere");
            Assert.That(a.Touch(inside: true, held: false, pressed: false), Is.False, "hovering");
            Assert.That(a.Touch(inside: true, held: true, pressed: true), Is.True, "a click on it");
            Assert.That(a.FadePerTick, Is.EqualTo(0.01));

            var b = field.Splatters[1];
            b.Touch(inside: false, held: true, pressed: false);
            Assert.That(b.Touch(inside: true, held: true, pressed: false), Is.True, "dragged into it");

            var c = field.Splatters[2];
            c.Touch(inside: true, held: false, pressed: false);
            Assert.That(c.Touch(inside: true, held: true, pressed: false), Is.False, "held without entering or pressing");

            field.Advance(96);
            Assert.That(field.Splatters, Does.Not.Contain(a), "0.95 / 0.01: gone in about 1.6 s");
            Assert.That(field.Splatters, Does.Contain(c));
        }
    }

    public class WhiteFlashTests
    {
        [Test]
        public void It_blinks_white_at_0_6_ten_times_and_is_gone_on_step_20()
        {
            var alphas = Enumerable.Range(0, 22).Select(WhiteFlash.AlphaAt).ToArray();
            Assert.That(alphas.Take(4), Is.EqualTo(new[] { 0.6, 0.6, 0, 0.6 }));
            Assert.That(alphas[19], Is.EqualTo(0.6));
            Assert.That(alphas[20], Is.EqualTo(0));
            Assert.That(alphas[21], Is.EqualTo(0));
            Assert.That(WhiteFlash.AlphaAt(-1), Is.EqualTo(0));
            // Ten white runs: steps 0-1, then 3, 5, ..., 19.
            var runs = 0;
            for (var i = 0; i < alphas.Length; i++)
                if (alphas[i] > 0 && (i == 0 || alphas[i - 1] == 0))
                    runs++;
            Assert.That(runs, Is.EqualTo(10));
        }
    }
}
