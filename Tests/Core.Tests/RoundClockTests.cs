using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // obj_time, the match clock at the top of the duel (UQD-T50).
    public class RoundClockTests
    {
        [Test]
        public void It_is_drawn_either_side_of_the_rooms_middle()
        {
            Assert.That(RoundClock.ColonX, Is.EqualTo(320), "\":\" centred at room_width / 2");
            Assert.That(RoundClock.MinutesRight, Is.EqualTo(317), "strMinutes right-aligned at room_width / 2 - 3");
            Assert.That(RoundClock.SecondsLeft, Is.EqualTo(323), "strSeconds left-aligned at room_width / 2 + 3");
        }

        [Test]
        public void It_drops_in_from_100_pixels_above_the_room_5_a_step()
        {
            Assert.That(RoundClock.YAt(-1), Is.EqualTo(-100), "timeY = -100");
            Assert.That(RoundClock.YAfter(0), Is.EqualTo(-100));
            for (var steps = 1; steps <= 21; steps++)
                Assert.That(RoundClock.YAfter(steps), Is.EqualTo(-100 + 5 * steps), $"step {steps}: timeY += 5");
            Assert.That(RoundClock.YAt(0), Is.EqualTo(-95), "the room steps once before it draws");
            Assert.That(RoundClock.YAfter(400), Is.EqualTo(5), "home at y 5");
        }

        [Test]
        public void It_takes_its_place_on_step_22_and_counts_from_the_next()
        {
            Assert.That(RoundClock.HomeSteps, Is.EqualTo(21));
            Assert.That(RoundClock.InPositionSteps, Is.EqualTo(22));
            Assert.That(RoundClock.InPositionAfter(21), Is.False, "step 21 only brings timeY to 5");
            Assert.That(RoundClock.InPositionAfter(22), Is.True, "step 22 finds it there: in_position = true");
            Assert.That(RoundClock.ElapsedStepsAfter(22), Is.Zero);
            Assert.That(RoundClock.ElapsedStepsAfter(23), Is.EqualTo(1), "elapsed += 1");
            Assert.That(RoundClock.InPosition(20), Is.False);
            Assert.That(RoundClock.InPosition(21), Is.True);
        }

        [Test]
        public void It_reads_minutes_and_seconds_since_it_settled_padded_to_two_digits()
        {
            var settled = RoundClock.InPositionSteps - 1; // the tick whose frame shows in_position set
            Assert.That(RoundClock.Text(-1), Is.EqualTo("00:00"));
            Assert.That(RoundClock.Text(settled), Is.EqualTo("00:00"));
            Assert.That(RoundClock.Text(settled + 59), Is.EqualTo("00:00"));
            Assert.That(RoundClock.Text(settled + 60), Is.EqualTo("00:01"));
            Assert.That(RoundClock.Text(settled + 60 * 9), Is.EqualTo("00:09"));
            Assert.That(RoundClock.Text(settled + 60 * 10), Is.EqualTo("00:10"));
            Assert.That(RoundClock.Text(settled + 60 * 59), Is.EqualTo("00:59"));
            Assert.That(RoundClock.Text(settled + 60 * 125), Is.EqualTo("02:05"));
            Assert.That(RoundClock.Text(settled + 60 * 60 * 100), Is.EqualTo("100:00"), "the minutes run on");
            Assert.That(RoundClock.MinutesText(settled + 60 * 61), Is.EqualTo("01"));
            Assert.That(RoundClock.SecondsText(settled + 60 * 61), Is.EqualTo("01"));
        }

        [Test]
        public void A_minute_turns_to_00_never_60()
        {
            var settled = RoundClock.InPositionSteps - 1;
            for (var tick = settled + 60 * 60 - 2; tick < settled + 60 * 60 + 62; tick++)
                Assert.That(RoundClock.SecondsText(tick), Is.Not.EqualTo("60"), $"tick {tick}");
            Assert.That(RoundClock.Text(settled + 60 * 60 - 1), Is.EqualTo("00:59"));
            Assert.That(RoundClock.Text(settled + 60 * 60), Is.EqualTo("01:00"));
        }

        [Test]
        public void It_is_stepped_at_60_a_second()
        {
            Assert.That(RoundClock.TickAt(0), Is.EqualTo(0));
            Assert.That(RoundClock.TickAt(1.0 / 60 + 1e-9), Is.EqualTo(1));
            Assert.That(RoundClock.TickAt(-0.001), Is.EqualTo(-1));
            Assert.That(RoundClock.TickAt(double.NaN), Is.EqualTo(-1));
            // A minute and a half of real time after it settled reads 01:30, whatever the duel was doing meanwhile.
            var settledAt = (RoundClock.InPositionSteps - 1) / 60.0;
            Assert.That(RoundClock.Text(RoundClock.TickAt(settledAt + 90.001)), Is.EqualTo("01:30"));
        }
    }
}
