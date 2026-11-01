using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The fighters' motion through a round (UQD-T35), against the original's own figures: the cue puts both on their run
    // marks at 40% and 60% of the 640-pixel room creeping 0.01 pixel a 1000 fps step; the winner jumps to the middle and
    // dashes 1 + 0.1 of the gap a 60 fps step on to the loser's idle mark, playing its attack strip; the loser plays its
    // defeat strip at the middle; three seconds on the winner shows its idle sprite again.
    public class FighterMotionTests
    {
        [Test]
        public void A_fighter_starts_idle_on_its_idle_mark()
        {
            var left = new FighterMotion(Side.Left);
            var right = new FighterMotion(Side.Right);
            Assert.That(left.Pose, Is.EqualTo(FighterPose.Idle));
            Assert.That(left.Sprite, Is.EqualTo(FighterSprite.Idle));
            Assert.That(left.X, Is.EqualTo(160));
            Assert.That(right.X, Is.EqualTo(480));
        }

        [Test]
        public void The_cue_puts_both_on_their_run_marks_in_the_run_sprite()
        {
            var left = new FighterMotion(Side.Left);
            var right = new FighterMotion(Side.Right);
            left.Run();
            right.Run();
            Assert.That(left.X, Is.EqualTo(640 / 2 - 640 / 10), "left_position_run");
            Assert.That(right.X, Is.EqualTo(640 / 2 + 640 / 10), "right_position_run");
            Assert.That(left.XFraction, Is.EqualTo(0.4).Within(1e-12));
            Assert.That(right.XFraction, Is.EqualTo(0.6).Within(1e-12));
            Assert.That(left.Sprite, Is.EqualTo(FighterSprite.Run));
            Assert.That(left.Frame, Is.Zero);
        }

        [Test]
        public void Running_fighters_creep_together_a_hundredth_of_a_pixel_a_1000_fps_step()
        {
            var left = new FighterMotion(Side.Left);
            var right = new FighterMotion(Side.Right);
            left.Run();
            right.Run();
            left.Creep(0.25);
            right.Creep(0.25);
            Assert.That(left.X, Is.EqualTo(256 + 250 * 0.01).Within(1e-9));
            Assert.That(right.X, Is.EqualTo(384 - 250 * 0.01).Within(1e-9));

            // Only a running fighter creeps.
            var idle = new FighterMotion(Side.Left);
            idle.Creep(1);
            Assert.That(idle.X, Is.EqualTo(160));
        }

        [TestCase(Side.Left)]
        [TestCase(Side.Right)]
        public void The_winner_jumps_to_the_middle_and_dashes_on_to_the_losers_mark(Side side)
        {
            var winner = new FighterMotion(side);
            winner.Run();
            winner.Attack();
            Assert.That(winner.X, Is.EqualTo(320));
            Assert.That(winner.Sprite, Is.EqualTo(FighterSprite.Attack));

            // One 60 fps step: 1 + 0.1 of the 160-pixel gap.
            var facing = FighterLineup.Facing(side);
            winner.Advance(1 / 60.0);
            Assert.That(winner.X, Is.EqualTo(320 + facing * 17).Within(1e-9));
            winner.Advance(1 / 60.0);
            Assert.That(winner.X, Is.EqualTo(320 + facing * (17 + 1 + 0.1 * 143)).Within(1e-9));

            // It eases to a stop on the loser's idle mark (1 + 0.1 of the gap always reaches it) and stays there.
            winner.Advance(2);
            var mark = side == Side.Left ? 480 : 160;
            Assert.That(facing * (winner.X - mark), Is.InRange(0, 1.0));
            var arrived = winner.X;
            winner.Advance(5);
            Assert.That(winner.X, Is.EqualTo(arrived));
        }

        [Test]
        public void The_dash_matches_the_originals_step_loop_exactly()
        {
            double x = 320;
            for (var step = 1; step <= 60; step++)
            {
                if (x < 480) x += 1 + 0.1 * (480 - x);
                Assert.That(FighterMotion.DashX(Side.Left, step / 60.0), Is.EqualTo(x), $"step {step}");
                Assert.That(FighterMotion.DashX(Side.Right, step / 60.0), Is.EqualTo(640 - x).Within(1e-9), $"step {step}");
            }
        }

        [Test]
        public void The_killing_blows_loser_stands_at_the_middle_playing_the_whole_defeat_strip()
        {
            var loser = new FighterMotion(Side.Right);
            loser.Run();
            loser.Defeat(killingBlow: true);
            Assert.That(loser.X, Is.EqualTo(320));
            Assert.That(loser.Sprite, Is.EqualTo(FighterSprite.Defeat));
            loser.Advance(20 / 60.0);
            Assert.That(loser.Frame, Is.EqualTo(2));
            loser.Advance(10);
            Assert.That(loser.X, Is.EqualTo(320));
            Assert.That(loser.Sprite, Is.EqualTo(FighterSprite.Defeat), "the loser never gets up");
            Assert.That(loser.Frame, Is.EqualTo(3));
        }

        [Test]
        public void A_loser_with_the_match_still_open_is_held_on_the_first_defeat_frame()
        {
            // obj_player: if(HP > 0) image_index = 0 every step while defeated (UQD-T64).
            var loser = new FighterMotion(Side.Left);
            loser.Defeat(killingBlow: false);
            Assert.That(loser.X, Is.EqualTo(320));
            Assert.That(loser.Sprite, Is.EqualTo(FighterSprite.Defeat));
            loser.Advance(10);
            Assert.That(loser.Frame, Is.Zero);
            loser.Idle();
            Assert.That(loser.KillingBlow, Is.False);
        }

        [Test]
        public void The_strips_show_a_frame_every_ten_60_fps_steps_and_hold_the_last()
        {
            Assert.That(FighterMotion.StripFrame(0), Is.Zero);
            Assert.That(FighterMotion.StripFrame(9 / 60.0), Is.Zero);
            Assert.That(FighterMotion.StripFrame(10 / 60.0), Is.EqualTo(1));
            Assert.That(FighterMotion.StripFrame(20 / 60.0), Is.EqualTo(2));
            Assert.That(FighterMotion.StripFrame(30 / 60.0), Is.EqualTo(3));
            Assert.That(FighterMotion.StripFrame(100), Is.EqualTo(3));
            Assert.That(FighterMotion.StripFrame(double.NaN), Is.Zero);
        }

        [Test]
        public void Three_seconds_after_the_strike_the_winner_shows_its_idle_sprite_where_it_stopped()
        {
            var winner = new FighterMotion(Side.Left);
            winner.Attack();
            winner.Advance(2.99);
            Assert.That(winner.Sprite, Is.EqualTo(FighterSprite.Attack));
            Assert.That(winner.Frame, Is.EqualTo(3));
            var x = winner.X;
            winner.Advance(0.02);
            Assert.That(winner.Sprite, Is.EqualTo(FighterSprite.Idle));
            Assert.That(winner.Frame, Is.Zero);
            Assert.That(winner.X, Is.EqualTo(x), "set_state_idle only comes with the next round");
        }

        [Test]
        public void A_new_round_puts_the_fighter_back_on_its_idle_mark()
        {
            var winner = new FighterMotion(Side.Right);
            winner.Attack();
            winner.Advance(4);
            winner.Idle();
            Assert.That(winner.Pose, Is.EqualTo(FighterPose.Idle));
            Assert.That(winner.X, Is.EqualTo(480));
            Assert.That(winner.PoseSeconds, Is.Zero);
        }
    }
}
