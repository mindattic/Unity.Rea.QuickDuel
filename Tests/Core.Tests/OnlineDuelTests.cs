using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The online protocol (UQD-T14) without a network: the host draws each round's lengths, every client runs the
    // round on its own clock and reports only its reaction, and the host judges the reports with DuelJudge.
    public class OnlineDuelTests
    {
        private static DuelSettings Quick() => new DuelSettings
        {
            CountdownSeconds = 1.0,
            MinStandoffSeconds = 2.0,
            MaxStandoffSeconds = 4.0,
            StrikeTimeoutSeconds = 3.0,
            SettleSeconds = 0.05
        };

        private static OnlineDuelHost NewHost(DuelSettings settings, int winsNeeded = 2, Random random = null) =>
            new OnlineDuelHost(settings, winsNeeded, TestTuning.Profile.Judge.HumanFloorSeconds,
                TestTuning.Profile.Online.ReportGraceSeconds, random);

        [Test]
        public void Duel_begun_with_a_given_standoff_cues_after_countdown_plus_that_standoff()
        {
            var duel = new Duel(Quick());
            duel.Begin(10.0, 2.5);
            Assert.That(duel.CueAt, Is.EqualTo(13.5).Within(1e-9));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Duel(Quick()).Begin(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Duel(Quick()).Begin(0, double.NaN));
        }

        [Test]
        public void Host_draws_each_rounds_standoff_within_the_settings_and_announces_it()
        {
            var host = NewHost(Quick(), random: new Random(7));
            var announced = new List<(int Round, double Countdown, double Standoff)>();
            host.RoundBegan += (r, c, s) => announced.Add((r, c, s));

            Assert.That(host.BeginRound(0), Is.EqualTo(1));
            Assert.That(host.RoundOpen, Is.True);
            Assert.That(announced, Has.Count.EqualTo(1));
            Assert.That(announced[0].Round, Is.EqualTo(1));
            Assert.That(announced[0].Countdown, Is.EqualTo(1.0));
            Assert.That(announced[0].Standoff, Is.InRange(2.0, 4.0));
            Assert.That(announced[0].Standoff, Is.EqualTo(host.StandoffSeconds));
            Assert.Throws<InvalidOperationException>(() => host.BeginRound(0), "a round must be judged before the next");
        }

        [Test]
        public void Clients_that_start_at_different_times_judge_by_their_own_reactions_not_arrival()
        {
            var host = NewHost(Quick(), random: new Random(1));
            var left = new OnlineDuelClient(Side.Left, TestTuning.Profile.Duel.ToSettings());
            var right = new OnlineDuelClient(Side.Right, TestTuning.Profile.Duel.ToSettings());
            Wire(host, left, right, leftNow: 100.0, rightNow: 5000.0);   // unrelated clocks, the right one far behind
            DuelResult judged = null;
            host.RoundJudged += (_, r) => judged = r;

            host.BeginRound(0);
            Assert.That(left.StandoffSeconds, Is.EqualTo(host.StandoffSeconds));
            Assert.That(right.StandoffSeconds, Is.EqualTo(host.StandoffSeconds));
            Assert.That(left.Duel.CueAt - 100.0, Is.EqualTo(right.Duel.CueAt - 5000.0).Within(1e-9), "same countdown + standoff");

            // The right player is faster, though its report reaches the host second.
            left.Strike(left.Duel.CueAt + 0.30);
            right.Strike(right.Duel.CueAt + 0.22);
            left.Tick(left.Duel.CueAt + 1.0);
            Assert.That(judged, Is.Null, "the host waits for both reports");
            right.Tick(right.Duel.CueAt + 1.0);

            Assert.That(judged, Is.Not.Null);
            Assert.That(judged.Outcome, Is.EqualTo(DuelOutcome.FasterStrike));
            Assert.That(judged.Winner, Is.EqualTo(Side.Right));
            Assert.That(judged.LeftReaction, Is.EqualTo(0.30).Within(1e-9));
            Assert.That(judged.RightReaction, Is.EqualTo(0.22).Within(1e-9));
            Assert.That(host.Match.WinsOf(Side.Right), Is.EqualTo(1));
            Assert.That(host.RoundOpen, Is.False);
        }

        [Test]
        public void An_early_strike_is_reported_at_once_and_loses()
        {
            var host = NewHost(Quick(), random: new Random(2));
            var left = new OnlineDuelClient(Side.Left, TestTuning.Profile.Duel.ToSettings());
            var right = new OnlineDuelClient(Side.Right, TestTuning.Profile.Duel.ToSettings());
            Wire(host, left, right, leftNow: 0, rightNow: 0);
            var reports = new List<double?>();
            left.ReactionReady += (_, r) => reports.Add(r);
            DuelResult judged = null;
            host.RoundJudged += (_, r) => judged = r;

            host.BeginRound(0);
            left.Strike(left.Duel.CueAt - 0.5);
            Assert.That(reports, Is.EqualTo(new double?[] { -0.5 }).Within(1e-9));
            left.Strike(left.Duel.CueAt + 0.2);
            left.Tick(left.Duel.CueAt + 5);
            Assert.That(reports, Has.Count.EqualTo(1), "one report per round");

            right.Tick(right.Duel.CueAt + 5);   // never struck
            Assert.That(judged.Outcome, Is.EqualTo(DuelOutcome.EarlyStrike));
            Assert.That(judged.Winner, Is.EqualTo(Side.Right));
        }

        [Test]
        public void Client_ignores_a_strike_during_the_countdown()
        {
            var client = new OnlineDuelClient(Side.Left, TestTuning.Profile.Duel.ToSettings());
            var reports = new List<double?>();
            client.ReactionReady += (_, r) => reports.Add(r);
            client.Begin(1, 1.0, 2.0, 0);

            client.Strike(0.5);   // still inside the 1.0s countdown: too early to mean anything, not a loss
            Assert.That(reports, Is.Empty);
            Assert.That(client.Duel.Phase, Is.EqualTo(DuelPhase.Countdown));

            client.Strike(client.Duel.CueAt + 0.2);
            client.Tick(client.Duel.CueAt + 0.3);
            Assert.That(reports, Is.EqualTo(new double?[] { 0.2 }).Within(1e-9));
        }

        [Test]
        public void A_client_that_never_strikes_reports_no_strike_after_the_timeout()
        {
            var client = new OnlineDuelClient(Side.Right, new DuelSettings { StrikeTimeoutSeconds = 3.0, SettleSeconds = 0.05 });
            var reports = new List<(int, double?)>();
            client.ReactionReady += (r, t) => reports.Add((r, t));
            client.Begin(1, 1.0, 2.0, 0);
            client.Tick(5.9);
            Assert.That(reports, Is.Empty);
            client.Tick(6.0);
            Assert.That(reports, Is.EqualTo(new[] { (1, (double?)null) }));
        }

        [Test]
        public void Client_ignores_a_stale_or_repeated_round()
        {
            var client = new OnlineDuelClient(Side.Left, TestTuning.Profile.Duel.ToSettings());
            Assert.That(client.Begin(2, 1, 2, 0), Is.True);
            var duel = client.Duel;
            Assert.That(client.Begin(2, 1, 3, 0), Is.False);
            Assert.That(client.Begin(1, 1, 3, 0), Is.False);
            Assert.That(client.Duel, Is.SameAs(duel));
            Assert.That(client.Begin(3, 1, 3, 0), Is.True);
            Assert.That(client.Round, Is.EqualTo(3));
        }

        [Test]
        public void Host_ignores_reports_for_another_round_and_repeats()
        {
            var host = NewHost(Quick());
            Assert.That(host.Report(Side.Left, 1, 0.3), Is.False, "no round open");
            host.BeginRound(0);
            Assert.That(host.Report(Side.Left, 2, 0.3), Is.False, "wrong round");
            Assert.That(host.Report(Side.Left, 1, 0.3), Is.True);
            Assert.That(host.Report(Side.Left, 1, 0.2), Is.False, "a side reports once");
            Assert.That(host.Report(Side.Right, 1, 0.4), Is.True);
            Assert.That(host.Match.Rounds[0].LeftReaction, Is.EqualTo(0.3));
            Assert.That(host.Report(Side.Right, 1, 0.1), Is.False, "the round is judged");
        }

        [Test]
        public void A_superhuman_report_forfeits_the_round()
        {
            var host = NewHost(Quick());
            DuelResult judged = null;
            host.RoundJudged += (_, r) => judged = r;
            host.BeginRound(0);
            host.Report(Side.Left, 1, 0.02);
            host.Report(Side.Right, 1, 0.35);
            Assert.That(judged.Outcome, Is.EqualTo(DuelOutcome.Forfeit));
            Assert.That(judged.Winner, Is.EqualTo(Side.Right));
        }

        [Test]
        public void A_silent_side_is_judged_as_no_strike_once_the_report_window_closes()
        {
            var settings = Quick();
            var host = NewHost(settings);
            Assert.That(host.ReportGraceSeconds, Is.EqualTo(2.0));
            DuelResult judged = null;
            host.RoundJudged += (_, r) => judged = r;
            host.BeginRound(10);
            host.Report(Side.Left, 1, 0.4);

            var closes = 10 + settings.CountdownSeconds + host.StandoffSeconds + settings.StrikeTimeoutSeconds
                + settings.SettleSeconds + 2.0;
            host.Tick(closes - 0.01);
            Assert.That(judged, Is.Null);
            host.Tick(closes);
            Assert.That(judged.Outcome, Is.EqualTo(DuelOutcome.FasterStrike));
            Assert.That(judged.Winner, Is.EqualTo(Side.Left));
            Assert.That(judged.RightReaction, Is.Null);
        }

        [Test]
        public void A_finished_match_is_replaced_by_a_fresh_one_on_the_next_round()
        {
            var host = NewHost(Quick(), winsNeeded: 1);
            host.BeginRound(0);
            host.Report(Side.Left, 1, 0.3);
            host.Report(Side.Right, 1, 0.4);
            Assert.That(host.Match.Winner, Is.EqualTo(Side.Left));

            host.BeginRound(10);
            Assert.That(host.Round, Is.EqualTo(2));
            Assert.That(host.Match.IsOver, Is.False);
            Assert.That(host.Match.Rounds, Is.Empty);
        }

        // Stands in for the transport: the host's announcement reaches each client at that client's own clock time,
        // and each client's report goes straight back to the host.
        private static void Wire(OnlineDuelHost host, OnlineDuelClient left, OnlineDuelClient right, double leftNow, double rightNow)
        {
            host.RoundBegan += (round, countdown, standoff) =>
            {
                left.Begin(round, countdown, standoff, leftNow);
                right.Begin(round, countdown, standoff, rightNow);
            };
            left.ReactionReady += (round, reaction) => host.Report(Side.Left, round, reaction);
            right.ReactionReady += (round, reaction) => host.Report(Side.Right, round, reaction);
        }
    }
}
