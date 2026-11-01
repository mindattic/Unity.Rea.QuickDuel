using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The cosmetic fighters (UQD-T34): the authority draws one fighter roll per match, every peer maps it onto the same
    // pair, every pair (the same fighter twice included) is reachable, and the line-up is the original's own numbers.
    public class FighterDrawTests
    {
        private static DuelSettings Quick() => new DuelSettings
        {
            CountdownSeconds = 0.04,
            MinStandoffSeconds = 0.02,
            MaxStandoffSeconds = 0.02,
            StrikeTimeoutSeconds = 0.5,
            SettleSeconds = 0.01,
        };

        [Test]
        public void Pick_reaches_every_pair_of_the_five_fighters_including_both_the_same()
        {
            var pairs = new HashSet<(int, int)>();
            var random = new Random(4);
            for (var i = 0; i < 5000; i++)
            {
                var roll = FighterDraw.Roll(random);
                var left = FighterDraw.Pick(roll, Side.Left, 5);
                var right = FighterDraw.Pick(roll, Side.Right, 5);
                Assert.That(left, Is.InRange(0, 4));
                Assert.That(right, Is.InRange(0, 4));
                Assert.That(FighterDraw.SameFighter(roll, 5), Is.EqualTo(left == right));
                pairs.Add((left, right));
            }
            Assert.That(pairs, Has.Count.EqualTo(25));
        }

        [Test]
        public void Pick_is_the_same_on_every_peer_and_rejects_an_empty_list()
        {
            // 13 = 2 * 5 + 3: the left seat is the low base-5 digit, the right seat the next.
            Assert.That(FighterDraw.Pick(13, Side.Left, 5), Is.EqualTo(3));
            Assert.That(FighterDraw.Pick(13, Side.Right, 5), Is.EqualTo(2));
            Assert.That(FighterDraw.SameFighter(6, 5), Is.True);
            Assert.That(FighterDraw.Pick(-1, Side.Left, 5), Is.InRange(0, 4), "a negative roll still lands in the list");
            Assert.That(FighterDraw.Pick(-1, Side.Right, 5), Is.InRange(0, 4));
            Assert.That(FighterDraw.Pick(7, Side.Right, 1), Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => FighterDraw.Pick(5, Side.Left, 0));
            Assert.Throws<ArgumentNullException>(() => FighterDraw.Roll(null));
        }

        [Test]
        public void A_local_session_keeps_its_fighters_for_a_match_and_draws_again_for_the_next()
        {
            var session = new MatchSession(() => TestTuning.Profile.Ai, 1, DuelMode.LocalPvp, new Random(11));
            var first = session.FighterRoll;
            Assert.That(first, Is.GreaterThanOrEqualTo(0));

            Assert.That(session.BeginRound(Quick(), 0), Is.True);
            Assert.That(session.FighterRoll, Is.EqualTo(first), "the first round keeps the pair already on the field");
            session.Strike(MatchSession.LeftPlayerId, session.Duel.CueAt + 0.2);
            session.Tick(10);
            Assert.That(session.Match.IsOver, Is.True);
            Assert.That(session.FighterRoll, Is.EqualTo(first), "a finished match keeps its fighters until the next");

            var rolls = new HashSet<int> { first };
            for (var match = 1; match <= 10; match++)
            {
                var now = match * 20.0;
                Assert.That(session.BeginRound(Quick(), now), Is.True);
                rolls.Add(session.FighterRoll);
                session.Strike(MatchSession.LeftPlayerId, session.Duel.CueAt + 0.2);
                session.Tick(now + 10);
            }
            Assert.That(rolls.Count, Is.GreaterThan(1), "every new match draws its fighters afresh");

            var before = session.FighterRoll;
            Assert.That(session.SetMode(MatchSession.LeftPlayerId, DuelMode.VsAiHard), Is.True);
            Assert.That(session.FighterRoll, Is.Not.EqualTo(before), "a mode change starts a new match with a new pair");
        }

        [Test]
        public void The_host_hands_its_fighter_roll_to_every_client_and_keeps_it_for_the_match()
        {
            var host = new OnlineDuelHost(Quick(), 2, TestTuning.Profile.Judge.HumanFloorSeconds,
                TestTuning.Profile.Online.ReportGraceSeconds, new Random(5));
            var left = new OnlineDuelClient(Side.Left, Quick());
            var right = new OnlineDuelClient(Side.Right, Quick());
            host.RoundBegan += (round, countdown, standoff) =>
            {
                left.Begin(round, countdown, standoff, 0, host.ArenaRoll, host.FighterRoll);
                right.Begin(round, countdown, standoff, 0, host.ArenaRoll, host.FighterRoll);
            };

            var first = host.FighterRoll;
            host.BeginRound(0);
            Assert.That(left.FighterRoll, Is.EqualTo(first));
            Assert.That(right.FighterRoll, Is.EqualTo(first));

            host.Report(Side.Left, 1, 0.2);
            host.Report(Side.Right, 1, 0.3);
            host.BeginRound(10);
            Assert.That(host.FighterRoll, Is.EqualTo(first), "the second round of the same match");

            host.Report(Side.Left, 2, 0.2);
            host.Report(Side.Right, 2, 0.3);
            Assert.That(host.Match.IsOver, Is.True);
            host.BeginRound(20);
            Assert.That(host.FighterRoll, Is.Not.EqualTo(first), "a new match, a new pair");
            Assert.That(left.FighterRoll, Is.EqualTo(host.FighterRoll));
        }

        [Test]
        public void The_line_up_is_the_originals_own()
        {
            Assert.That(FighterLineup.X(Side.Left), Is.EqualTo(0.25), "left_position_idle = room_width / 4");
            Assert.That(FighterLineup.X(Side.Right), Is.EqualTo(0.75), "right_position_idle = room_width - room_width / 4");
            Assert.That(FighterLineup.Y * FighterLineup.OriginalHeight, Is.EqualTo(320).Within(1e-9), "vertical_position = 480 - 480 / 3");
            Assert.That(FighterLineup.Facing(Side.Left), Is.EqualTo(1));
            Assert.That(FighterLineup.Facing(Side.Right), Is.EqualTo(-1), "image_xscale = -1: faces left");
        }

        [Test]
        public void Fighters_fade_in_at_a_hundredth_per_sixtieth_of_a_second()
        {
            Assert.That(FighterLineup.Alpha(0), Is.EqualTo(0));
            Assert.That(FighterLineup.Alpha(-1), Is.EqualTo(0));
            Assert.That(FighterLineup.Alpha(double.NaN), Is.EqualTo(0));
            Assert.That(FighterLineup.Alpha(1.0 / 60), Is.EqualTo(0.01).Within(1e-12), "one original step");
            Assert.That(FighterLineup.Alpha(0.5), Is.EqualTo(0.3).Within(1e-12));
            Assert.That(FighterLineup.Alpha(100.0 / 60 - 1e-6), Is.LessThan(1));
            Assert.That(FighterLineup.Alpha(100.0 / 60), Is.EqualTo(1).Within(1e-12), "full after the original's 100 steps");
            Assert.That(FighterLineup.Alpha(30), Is.EqualTo(1));
        }

        [Test]
        public void The_same_fighter_on_both_sides_is_tinted_red_and_blue()
        {
            Assert.That(FighterLineup.Tint(Side.Left, sameFighter: true), Is.EqualTo(new Rgb(196, 64, 64)), "color_red");
            Assert.That(FighterLineup.Tint(Side.Right, sameFighter: true), Is.EqualTo(new Rgb(64, 64, 196)), "color_blue");
            Assert.That(FighterLineup.Tint(Side.Left, sameFighter: false), Is.Null);
            Assert.That(FighterLineup.Tint(Side.Right, sameFighter: false), Is.Null);
        }
    }
}
