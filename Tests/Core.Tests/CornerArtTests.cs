using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // obj_cornerart_left and obj_cornerart_right sliding in on start (UQD-T43).
    public class CornerArtTests
    {
        [Test]
        public void Both_arts_start_a_whole_sprite_width_off_their_edges()
        {
            Assert.That(CornerArt.SpriteWidth, Is.EqualTo(152));
            Assert.That(CornerArt.RightX, Is.EqualTo(488), "gamescreen.room places obj_cornerart_right at 488");
            Assert.That(CornerArt.Y, Is.EqualTo(24));
            Assert.That(CornerArt.DistanceAfter(0), Is.EqualTo(152));
            Assert.That(CornerArt.LeftXAt(-1), Is.EqualTo(-152), "x = -sprite_width");
            Assert.That(CornerArt.RightXAt(-1), Is.EqualTo(640), "x = room_width");
        }

        [Test]
        public void Each_step_closes_1_plus_0_05_of_the_distance_left()
        {
            for (var steps = 0; steps < CornerArt.InPositionSteps - 1; steps++)
            {
                var before = CornerArt.DistanceAfter(steps);
                Assert.That(before, Is.GreaterThan(0));
                Assert.That(CornerArt.DistanceAfter(steps + 1), Is.EqualTo(before - (1 + before * 0.05)).Within(1e-9));
            }
            Assert.That(CornerArt.DistanceAfter(1), Is.EqualTo(152 - 1 - 7.6).Within(1e-9));
        }

        [Test]
        public void The_first_frame_already_shows_one_step_since_the_room_steps_before_it_draws()
        {
            Assert.That(CornerArt.LeftXAt(0), Is.EqualTo(-143.4).Within(1e-9));
            Assert.That(CornerArt.RightXAt(0), Is.EqualTo(488 + 143.4).Within(1e-9));
        }

        [Test]
        public void The_left_and_right_arts_move_as_mirror_images()
        {
            for (var tick = 0; tick < 60; tick++)
                Assert.That(CornerArt.RightXAt(tick) - CornerArt.RightX, Is.EqualTo(-CornerArt.LeftXAt(tick)).Within(1e-12));
        }

        [Test]
        public void The_step_that_passes_its_place_draws_it_a_hair_over_and_the_next_snaps_it_home()
        {
            Assert.That(CornerArt.InPositionSteps, Is.EqualTo(43));
            Assert.That(CornerArt.DistanceAfter(41), Is.GreaterThan(0));
            var over = CornerArt.DistanceAfter(42);
            Assert.That(over, Is.LessThan(0).And.GreaterThan(-0.1), "x passes 0 by a fraction of a pixel");
            Assert.That(CornerArt.LeftXAt(41), Is.EqualTo(-over));
            Assert.That(CornerArt.InPosition(41), Is.False);
            for (var tick = 42; tick < 600; tick++)
            {
                Assert.That(CornerArt.LeftXAt(tick), Is.EqualTo(0), "x = 0; in_position = true");
                Assert.That(CornerArt.RightXAt(tick), Is.EqualTo(488));
                Assert.That(CornerArt.InPosition(tick), Is.True);
            }
        }

        [Test]
        public void The_slide_is_stepped_at_60_a_second()
        {
            Assert.That(CornerArt.TickAt(0), Is.EqualTo(0));
            Assert.That(CornerArt.TickAt(1.0 / 60 + 1e-9), Is.EqualTo(1));
            Assert.That(CornerArt.TickAt(-0.001), Is.EqualTo(-1));
            Assert.That(CornerArt.TickAt(double.NaN), Is.EqualTo(-1));
        }
    }
}
