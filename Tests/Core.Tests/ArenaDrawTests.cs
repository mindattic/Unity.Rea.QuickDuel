using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The arenas (UQD-T33): the authority draws an arena roll per round, every peer maps it onto the same arena, and
    // all 11 of load_arenas.gml's arenas are reachable (the original's id clash left Sozza's Point unreachable).
    public class ArenaDrawTests
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
        public void Pick_lands_every_roll_inside_the_list_and_reaches_every_arena()
        {
            var seen = new HashSet<int>();
            var random = new Random(3);
            for (var i = 0; i < 2000; i++)
            {
                var index = ArenaDraw.Pick(ArenaDraw.Roll(random), 11);
                Assert.That(index, Is.InRange(0, 10));
                seen.Add(index);
            }
            Assert.That(seen, Has.Count.EqualTo(11), "every one of the 11 arenas, Sozza's Point included");
        }

        [Test]
        public void Pick_is_the_same_on_every_peer_and_rejects_an_empty_list()
        {
            Assert.That(ArenaDraw.Pick(25, 11), Is.EqualTo(3));
            Assert.That(ArenaDraw.Pick(25, 11), Is.EqualTo(ArenaDraw.Pick(25, 11)));
            Assert.That(ArenaDraw.Pick(-1, 11), Is.EqualTo(10));
            Assert.That(ArenaDraw.Pick(0, 1), Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArenaDraw.Pick(5, 0));
            Assert.Throws<ArgumentNullException>(() => ArenaDraw.Roll(null));
        }

        [Test]
        public void A_local_session_keeps_one_arena_for_every_round_of_a_match()
        {
            // The original chose its arena once per game (choose_random_arena, from obj_location only) (UQD-T63).
            var session = new MatchSession(() => TestTuning.Profile.Ai, 99, DuelMode.LocalPvp, new Random(11));
            var arena = session.ArenaRoll;
            Assert.That(arena, Is.GreaterThanOrEqualTo(0));
            for (var round = 0; round < 20; round++)
            {
                var now = round * 10.0;
                Assert.That(session.BeginRound(Quick(), now), Is.True);
                Assert.That(session.ArenaRoll, Is.EqualTo(arena), "the same arena all match");
                session.Tick(now + 5.0);   // nobody strikes: the round times out
                Assert.That(session.Phase, Is.EqualTo(DuelPhase.Resolved));
            }
        }

        [Test]
        public void A_new_match_draws_a_fresh_arena()
        {
            var session = new MatchSession(() => TestTuning.Profile.Ai, 99, DuelMode.LocalPvp, new Random(11));
            var arenas = new HashSet<int> { ArenaDraw.Pick(session.ArenaRoll, 11) };
            for (var match = 0; match < 20; match++)
            {
                Assert.That(session.CycleMode(MatchSession.LeftPlayerId), Is.True);
                arenas.Add(ArenaDraw.Pick(session.ArenaRoll, 11));
            }
            Assert.That(arenas.Count, Is.GreaterThan(1), "the arena changes between matches");
        }

        [Test]
        public void The_host_keeps_one_arena_for_every_round_of_a_match()
        {
            var host = new OnlineDuelHost(Quick(), 99, TestTuning.Profile.Judge.HumanFloorSeconds,
                TestTuning.Profile.Online.ReportGraceSeconds, new Random(5));
            var arena = host.ArenaRoll;
            for (var round = 1; round <= 5; round++)
            {
                host.BeginRound(round * 100.0);
                Assert.That(host.ArenaRoll, Is.EqualTo(arena));
                host.Report(Side.Left, round, 0.3);
                host.Report(Side.Right, round, 0.4);
            }
        }

        [Test]
        public void The_host_hands_its_arena_roll_to_every_client_with_the_round()
        {
            var host = new OnlineDuelHost(Quick(), 2, TestTuning.Profile.Judge.HumanFloorSeconds,
                TestTuning.Profile.Online.ReportGraceSeconds, new Random(5));
            var left = new OnlineDuelClient(Side.Left, Quick());
            var right = new OnlineDuelClient(Side.Right, Quick());
            host.RoundBegan += (round, countdown, standoff) =>
            {
                left.Begin(round, countdown, standoff, 0, host.ArenaRoll);
                right.Begin(round, countdown, standoff, 0, host.ArenaRoll);
            };

            host.BeginRound(0);
            Assert.That(left.ArenaRoll, Is.EqualTo(host.ArenaRoll));
            Assert.That(right.ArenaRoll, Is.EqualTo(host.ArenaRoll));
        }
    }
}
