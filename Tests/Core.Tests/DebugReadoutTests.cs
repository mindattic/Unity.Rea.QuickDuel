using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The original's F12 debug overlay, the developer's toggle (UQD-T50).
    public class DebugReadoutTests
    {
        [Test]
        public void It_draws_where_obj_game_and_obj_gameclock_did()
        {
            Assert.That((DebugReadout.RateX, DebugReadout.RateY), Is.EqualTo((320, 150)));
            Assert.That((DebugReadout.LeftReactionX, DebugReadout.ReactionY), Is.EqualTo((10, 240)));
            Assert.That((DebugReadout.RightReactionX, DebugReadout.ReactionY), Is.EqualTo((630, 240)));
            Assert.That((DebugReadout.PhaseX, DebugReadout.PhaseY, DebugReadout.CountY), Is.EqualTo((10, 10, 28)));
        }

        [Test]
        public void A_reaction_reads_in_whole_milliseconds_and_9999_before_a_strike()
        {
            Assert.That(DebugReadout.Reaction(null), Is.EqualTo("9999"), "gameclock_startup: reaction_time = 9999");
            Assert.That(DebugReadout.Reaction(0.2374), Is.EqualTo("237"));
            Assert.That(DebugReadout.Reaction(0.2375), Is.EqualTo("238"));
            Assert.That(DebugReadout.Reaction(-0.4), Is.EqualTo("-400"), "a strike before the cue");
        }

        [Test]
        public void The_frame_rate_stands_in_for_room_speed()
        {
            Assert.That(DebugReadout.Rate(59.7), Is.EqualTo("60"));
            Assert.That(DebugReadout.Rate(0), Is.EqualTo("0"));
            Assert.That(DebugReadout.Rate(double.PositiveInfinity), Is.EqualTo("0"));
            Assert.That(DebugReadout.Rate(double.NaN), Is.EqualTo("0"));
            Assert.That(DebugReadout.Milliseconds(1.25), Is.EqualTo("1250"));
            Assert.That(DebugReadout.Milliseconds(-1), Is.EqualTo("0"));
        }
    }
}
