using System;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    public class MatchSessionTests
    {
        private static DuelSettings FastSettings() => new DuelSettings
        {
            CountdownSeconds = 0.04,
            MinStandoffSeconds = 0.02,
            MaxStandoffSeconds = 0.02,
            StrikeTimeoutSeconds = 0.5,
            SettleSeconds = 0.01,
        };

        private static MatchSession NewSession(DuelMode mode = DuelMode.LocalPvp, int winsNeeded = 2) =>
            new MatchSession(() => TestTuning.Profile.Ai, winsNeeded, mode);

        [Test]
        public void A_player_id_with_no_seat_cannot_issue_any_command()
        {
            var session = NewSession();
            Assert.That(session.BeginRound(FastSettings(), 0, playerId: 7), Is.False);
            Assert.That(session.SetMode(7, DuelMode.VsAiEasy), Is.False);
            Assert.That(session.Mode, Is.EqualTo(DuelMode.LocalPvp));

            session.BeginRound(FastSettings(), 0);
            session.Strike(7, 0.05);   // no seat: ignored, not attributed to either side
            Assert.That(session.Phase, Is.Not.EqualTo(DuelPhase.Resolved));
        }

        [Test]
        public void Side_of_the_player_id_matches_the_fixed_seats()
        {
            Assert.That(MatchSession.SideOf(MatchSession.LeftPlayerId), Is.EqualTo(Side.Left));
            Assert.That(MatchSession.SideOf(MatchSession.RightPlayerId), Is.EqualTo(Side.Right));
            Assert.That(MatchSession.SideOf(99), Is.Null);
        }

        [Test]
        public void BeginRound_starts_the_round_and_announces_it()
        {
            var session = NewSession();
            var started = 0;
            session.RoundStarted += () => started++;

            Assert.That(session.BeginRound(FastSettings(), 0), Is.True);
            Assert.That(started, Is.EqualTo(1));
            Assert.That(session.Round, Is.EqualTo(1));
            Assert.That(session.Duel, Is.Not.Null);
            Assert.That(session.Phase, Is.EqualTo(DuelPhase.Countdown));
        }

        [Test]
        public void BeginRound_refuses_a_second_round_while_one_is_open()
        {
            var session = NewSession();
            Assert.That(session.BeginRound(FastSettings(), 0), Is.True);
            Assert.That(session.BeginRound(FastSettings(), 0), Is.False);
            Assert.That(session.Round, Is.EqualTo(1));
        }

        [Test]
        public void Strike_is_attributed_by_player_id_in_local_pvp()
        {
            // A strike timestamped at or after the cue puts the round in the Strike phase by itself
            // (Duel.Strike), so no Tick is needed to see the two seats resolve a round.
            var session = NewSession();
            session.BeginRound(FastSettings(), 0);
            var cue = session.Duel.CueAt;

            session.Strike(MatchSession.RightPlayerId, cue + 0.05);
            session.Strike(MatchSession.LeftPlayerId, cue + 0.02);

            Assert.That(session.Result.Winner, Is.EqualTo(Side.Left));
        }

        [Test]
        public void A_strike_for_the_ai_s_seat_is_refused_in_a_vs_ai_mode()
        {
            var session = NewSession(DuelMode.VsAiHard);
            session.BeginRound(FastSettings(), 0);
            var cue = session.Duel.CueAt;

            session.Strike(MatchSession.RightPlayerId, cue + 0.05);   // a human cannot play the AI's seat
            Assert.That(session.Phase, Is.Not.EqualTo(DuelPhase.Resolved));

            session.Strike(MatchSession.LeftPlayerId, cue + 0.02);
            session.Tick(cue + 0.04);   // past the settle window; the real AI never got there either
            Assert.That(session.Result.Winner, Is.EqualTo(Side.Left));
        }

        [Test]
        public void SetMode_is_refused_mid_round_but_cycling_resumes_once_it_resolves()
        {
            var session = NewSession();
            session.BeginRound(FastSettings(), 0);

            Assert.That(session.CycleMode(MatchSession.LeftPlayerId), Is.False);
            Assert.That(session.Mode, Is.EqualTo(DuelMode.LocalPvp));

            var cue = session.Duel.CueAt;
            session.Strike(MatchSession.LeftPlayerId, cue + 0.05);
            session.Strike(MatchSession.RightPlayerId, cue + 0.10);   // both struck: the round resolves at once
            Assert.That(session.CanBeginRound, Is.True);

            Assert.That(session.CycleMode(MatchSession.LeftPlayerId), Is.True);
            Assert.That(session.Mode, Is.EqualTo(DuelMode.VsAiEasy));
        }

        [Test]
        public void Changing_mode_always_starts_a_fresh_match_and_wires_up_the_ai()
        {
            var session = NewSession();
            session.BeginRound(FastSettings(), 0);
            var cue = session.Duel.CueAt;
            session.Strike(MatchSession.LeftPlayerId, cue - 0.01);   // before the cue: an early strike, Left loses
            Assert.That(session.Match.WinsOf(Side.Right), Is.EqualTo(1));

            session.SetMode(MatchSession.LeftPlayerId, DuelMode.VsAiNormal);

            Assert.That(session.Match.WinsOf(Side.Right), Is.Zero, "SetMode must reset the match");
            Assert.That(session.AiPlaysRight, Is.True);
        }

        [Test]
        public void Cycle_mode_runs_pvp_easy_normal_hard_and_back()
        {
            var session = NewSession();
            var seen = new System.Collections.Generic.List<DuelMode> { session.Mode };
            for (var i = 0; i < 4; i++)
            {
                session.CycleMode(MatchSession.LeftPlayerId);
                seen.Add(session.Mode);
            }

            Assert.That(seen, Is.EqualTo(new[]
            {
                DuelMode.LocalPvp, DuelMode.VsAiEasy, DuelMode.VsAiNormal, DuelMode.VsAiHard, DuelMode.LocalPvp
            }));
        }

        [Test]
        public void A_finished_match_is_replaced_by_the_next_BeginRound()
        {
            var session = NewSession(winsNeeded: 1);
            session.BeginRound(FastSettings(), 0);
            var cue = session.Duel.CueAt;
            session.Strike(MatchSession.LeftPlayerId, cue - 0.01);   // early strike: Right takes the round and the match
            Assert.That(session.Match.IsOver, Is.True);

            session.BeginRound(FastSettings(), 10);
            Assert.That(session.Match.IsOver, Is.False);
            Assert.That(session.Match.WinsOf(Side.Right), Is.Zero);
        }

        [Test]
        public void The_ai_strikes_itself_once_its_drawn_time_arrives()
        {
            var session = NewSession(DuelMode.VsAiHard);
            session.BeginRound(FastSettings(), 0);
            Assert.That(session.AiStrikeAt, Is.Not.Null);

            var cue = session.Duel.CueAt;
            session.Tick(cue);   // a real frame loop would already have reached Strike well before the AI's drawn time
            var aiAt = session.AiStrikeAt.Value;
            session.Tick(aiAt);
            Assert.That(session.AiStrikeAt, Is.Null, "the AI's drawn time is consumed once it strikes");

            session.Tick(aiAt + 0.02);   // past the settle window: the AI's lone strike wins
            Assert.That(session.Result, Is.Not.Null);
            Assert.That(session.Result.Winner, Is.EqualTo(Side.Right));
        }

        [Test]
        public void A_tick_due_for_both_the_ai_and_the_settle_window_still_counts_the_ai_s_strike()
        {
            // Regression for the ordering DuelSession used to guarantee by running its own Update ahead of
            // DuelController's: the AI's strike must be registered before the same Tick call can settle the
            // round on the human's strike alone.
            var session = NewSession(DuelMode.VsAiHard);
            session.BeginRound(FastSettings(), 0);
            var cue = session.Duel.CueAt;
            var aiAt = session.AiStrikeAt.Value;   // always after cue + 0.20s (Hard's range), so after the settle window too

            session.Tick(cue);
            session.Strike(MatchSession.LeftPlayerId, cue + 0.01);   // sets firstStrikeAt; its settle window closes at +0.01

            session.Tick(aiAt);   // also past the human's settle deadline, in the very same call

            Assert.That(session.Result, Is.Not.Null);
            Assert.That(session.Result.LeftReaction, Is.Not.Null);
            Assert.That(session.Result.RightReaction, Is.Not.Null);
        }

        [Test]
        public void Constructor_rejects_a_null_ai_tuning_provider()
        {
            Assert.Throws<ArgumentNullException>(() => new MatchSession(null, 2));
        }

        [Test]
        public void The_ai_tuning_is_read_fresh_on_every_mode_change_not_captured_once()
        {
            // DuelSession used to call `new AiOpponent(Difficulty(mode), TuningStore.Profile.Ai)` inside
            // SetMode itself, so a tuning profile swapped later still reached the next mode change.
            // MatchSession must keep that: the AI's tuning is a provider, not a one-time snapshot.
            var calls = 0;
            AiTuning Provider()
            {
                calls++;
                return TestTuning.Profile.Ai;
            }

            var session = new MatchSession(Provider, 2);   // LocalPvp: no AI yet, so the provider isn't called yet
            Assert.That(calls, Is.Zero);

            session.SetMode(MatchSession.LeftPlayerId, DuelMode.VsAiEasy);
            Assert.That(calls, Is.EqualTo(1));

            session.SetMode(MatchSession.LeftPlayerId, DuelMode.VsAiHard);
            Assert.That(calls, Is.EqualTo(2), "a later mode change must read the tuning again, not reuse the first AiOpponent's");
        }
    }
}
