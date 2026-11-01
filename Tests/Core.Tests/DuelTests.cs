using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    public class DuelTests
    {
        private static Duel StartedDuel(out double cue, int seed = 7)
        {
            var duel = new Duel(TestTuning.Profile.Duel.ToSettings(), new Random(seed));
            duel.Begin(0);
            cue = duel.CueAt;
            return duel;
        }

        [Test]
        public void A_local_round_waits_for_the_first_strike_with_the_count_running()
        {
            // gameclock_action has no timeout: the action counts until someone strikes (UQD-T68).
            var duel = new Duel(TestTuning.Profile.Duel.ToLocalSettings(), new Random(7));
            duel.Begin(0);
            var cue = duel.CueAt;
            duel.Tick(cue + 60);
            Assert.That(duel.Phase, Is.EqualTo(DuelPhase.Strike), "a minute on, still waiting");
            Assert.That(duel.ReactionCount(cue + 60), Is.EqualTo(ReactionCounter.Count(60)), "the count runs on uncapped");
            duel.Strike(Side.Right, cue + 61);
            duel.Tick(cue + 62);
            Assert.That(duel.Phase, Is.EqualTo(DuelPhase.Resolved));
            Assert.That(duel.Result.Winner, Is.EqualTo(Side.Right));
        }

        [Test]
        public void Cue_falls_after_countdown_inside_the_standoff_window()
        {
            for (var seed = 0; seed < 200; seed++)
            {
                StartedDuel(out var cue, seed);
                Assert.That(cue, Is.InRange(4.0 + 3.0, 4.0 + 6.5));
            }
        }

        [Test]
        public void Phases_advance_countdown_standoff_strike()
        {
            var duel = StartedDuel(out var cue);
            Assert.That(duel.Phase, Is.EqualTo(DuelPhase.Countdown));
            duel.Tick(4.0);
            Assert.That(duel.Phase, Is.EqualTo(DuelPhase.Standoff));
            duel.Tick(cue);
            Assert.That(duel.Phase, Is.EqualTo(DuelPhase.Strike));
        }

        [Test]
        public void The_countdown_is_Get_Ready_for_a_quarter_then_3_2_1_as_in_gameclock_countdown()
        {
            var duel = StartedDuel(out _);
            var shown = new[] { 0.0, 0.99, 1.0, 1.99, 2.0, 2.99, 3.0, 3.99 };
            Assert.That(Array.ConvertAll(shown, duel.CountdownNumber), Is.EqualTo(new[] { 0, 0, 3, 3, 2, 2, 1, 1 }));
        }

        [Test]
        public void Each_number_appears_at_full_alpha_and_fades_0_02_a_60_fps_step()
        {
            var duel = StartedDuel(out _);
            // Nothing numbered during Get Ready.
            Assert.That(duel.CountdownNumberAlpha(0.5), Is.EqualTo(0));
            foreach (var appears in new[] { 1.0, 2.0, 3.0 })
            {
                Assert.That(duel.CountdownNumberAlpha(appears), Is.EqualTo(1).Within(1e-9));
                // Ten steps on: 1 - 10 * 0.02.
                Assert.That(duel.CountdownNumberAlpha(appears + 10 / 60.0), Is.EqualTo(0.8).Within(1e-9));
                // Gone after the original's 50 steps, before the next number.
                Assert.That(duel.CountdownNumberAlpha(appears + 50 / 60.0), Is.EqualTo(0).Within(1e-9));
                Assert.That(duel.CountdownNumberAlpha(appears + 0.99), Is.EqualTo(0));
            }
            duel.Tick(4.0);
            Assert.That(duel.CountdownNumberAlpha(4.0), Is.EqualTo(0));
        }

        [Test]
        public void The_strike_counter_counts_milliseconds_from_the_cue_at_full_alpha()
        {
            var duel = StartedDuel(out var cue);
            duel.Tick(cue - 0.5);
            Assert.That(duel.ReactionCount(cue - 0.5), Is.EqualTo(0), "nothing before the cue");
            duel.Tick(cue);
            // The cue's own step draws 1 (count = 0; count += 1), then one a millisecond, read off the clock.
            Assert.That(duel.ReactionCount(cue), Is.EqualTo(1));
            Assert.That(duel.ReactionCount(cue + 0.0004), Is.EqualTo(1));
            Assert.That(duel.ReactionCount(cue + 0.0015), Is.EqualTo(2), "ceil(count)");
            Assert.That(duel.ReactionCount(cue + 0.25), Is.EqualTo(250));
            Assert.That(duel.ReactionCount(cue + 1.2345), Is.EqualTo(1235));
            Assert.That(duel.ReactionCountAlpha(cue + 1.2345), Is.EqualTo(1), "alpha = default_alpha while it counts");
            // Never past the strike timeout, even read before Tick ends the round.
            Assert.That(duel.ReactionCount(cue + 10), Is.EqualTo(3000));
        }

        [Test]
        public void The_first_strike_freezes_the_counter_which_then_fades_0_003_a_60_fps_step()
        {
            var duel = StartedDuel(out var cue);
            duel.Tick(cue);
            duel.Strike(Side.Right, cue + 0.212);
            Assert.That(duel.Phase, Is.EqualTo(DuelPhase.Strike));
            Assert.That(duel.ReactionCount(cue + 0.4), Is.EqualTo(212), "frozen at the first strike");
            Assert.That(duel.ReactionCountAlpha(cue + 0.212), Is.EqualTo(1).Within(1e-9));
            // Ten 60 fps steps on: 1 - 10 * 0.003.
            Assert.That(duel.ReactionCountAlpha(cue + 0.212 + 10 / 60.0), Is.EqualTo(0.97).Within(1e-9));

            duel.Strike(Side.Left, cue + 0.3);
            Assert.That(duel.Phase, Is.EqualTo(DuelPhase.Resolved));
            Assert.That(duel.ReactionCount(cue + 1), Is.EqualTo(212), "the verdict keeps the winner's count");
            Assert.That(duel.ReactionCountAlpha(cue + 0.212 + 1), Is.EqualTo(0.82).Within(1e-9));
            // Gone after the original's 333 steps (5.6 s).
            Assert.That(duel.ReactionCountAlpha(cue + 0.212 + 334 / 60.0), Is.EqualTo(0));
        }

        [Test]
        public void A_later_delivered_earlier_strike_refreezes_the_counter_at_the_earlier_time()
        {
            var duel = StartedDuel(out var cue);
            duel.Tick(cue);
            duel.Strike(Side.Left, cue + 0.3);
            Assert.That(duel.ReactionCount(cue + 0.31), Is.EqualTo(300));
            duel.Strike(Side.Right, cue + 0.25);
            Assert.That(duel.ReactionCount(cue + 0.31), Is.EqualTo(250));
        }

        [Test]
        public void No_counter_after_a_round_decided_without_a_reaction()
        {
            var early = StartedDuel(out var cue);
            early.Tick(4.0);
            early.Strike(Side.Left, cue - 0.5);
            Assert.That(early.ReactionCount(cue), Is.EqualTo(0), "an early strike");

            var nobody = StartedDuel(out cue);
            nobody.Tick(cue);
            nobody.Tick(cue + 3.0);
            Assert.That(nobody.Phase, Is.EqualTo(DuelPhase.Resolved));
            Assert.That(nobody.ReactionCount(cue + 3.0), Is.EqualTo(0), "nobody struck");
            Assert.That(nobody.ReactionCountAlpha(cue + 3.0), Is.EqualTo(0));
        }

        [Test]
        public void Counter_figures_hold_at_their_edges()
        {
            Assert.That(ReactionCounter.Count(double.NaN), Is.EqualTo(1));
            Assert.That(ReactionCounter.Count(-1), Is.EqualTo(1));
            Assert.That(ReactionCounter.FrozenAlpha(-0.1), Is.EqualTo(1));
            Assert.That(ReactionCounter.FrozenAlpha(double.NaN), Is.EqualTo(0));
            Assert.That(ReactionCounter.FrozenAlpha(60), Is.EqualTo(0));
        }

        [Test]
        public void Number_alpha_holds_at_zero_and_is_zero_before_it_appears()
        {
            Assert.That(CountdownBanner.NumberAlpha(-0.1), Is.EqualTo(0));
            Assert.That(CountdownBanner.NumberAlpha(double.NaN), Is.EqualTo(0));
            Assert.That(CountdownBanner.NumberAlpha(5), Is.EqualTo(0));
            Assert.That(CountdownBanner.NumberAlpha(0.5), Is.EqualTo(0.4).Within(1e-9));
        }

        [Test]
        public void Faster_strike_wins()
        {
            var duel = StartedDuel(out var cue);
            duel.Tick(cue);
            duel.Strike(Side.Right, cue + 0.21);
            duel.Strike(Side.Left, cue + 0.30);

            Assert.That(duel.Phase, Is.EqualTo(DuelPhase.Resolved));
            Assert.That(duel.Result.Outcome, Is.EqualTo(DuelOutcome.FasterStrike));
            Assert.That(duel.Result.Winner, Is.EqualTo(Side.Right));
            Assert.That(duel.Result.RightReaction, Is.EqualTo(0.21).Within(1e-9));
        }

        [Test]
        public void Lone_strike_wins_once_the_settle_window_passes()
        {
            var duel = StartedDuel(out var cue);
            duel.Tick(cue);
            duel.Strike(Side.Left, cue + 0.25);
            duel.Tick(cue + 0.26);
            Assert.That(duel.Phase, Is.EqualTo(DuelPhase.Strike));

            duel.Tick(cue + 0.31);
            Assert.That(duel.Result.Winner, Is.EqualTo(Side.Left));
            Assert.That(duel.Result.RightReaction, Is.Null);
        }

        [Test]
        public void Events_delivered_out_of_order_are_judged_by_timestamp()
        {
            var duel = StartedDuel(out var cue);
            duel.Tick(cue);
            duel.Strike(Side.Left, cue + 0.30);
            duel.Strike(Side.Right, cue + 0.29);
            Assert.That(duel.Result.Winner, Is.EqualTo(Side.Right));
        }

        [Test]
        public void Striking_before_the_cue_loses()
        {
            var duel = StartedDuel(out var cue);
            duel.Tick(4.0);
            duel.Strike(Side.Left, 4.0);

            Assert.That(duel.Result.Outcome, Is.EqualTo(DuelOutcome.EarlyStrike));
            Assert.That(duel.Result.Winner, Is.EqualTo(Side.Right));
            Assert.That(duel.Result.LeftReaction, Is.EqualTo(4.0 - cue).Within(1e-9));
        }

        [Test]
        public void Striking_during_the_countdown_is_ignored()
        {
            var duel = StartedDuel(out var cue);
            duel.Strike(Side.Right, 1.0);

            Assert.That(duel.Phase, Is.EqualTo(DuelPhase.Countdown));
            Assert.That(duel.Result, Is.Null);

            // The ignored press didn't consume Right's strike, so the round still plays out normally.
            duel.Tick(cue);
            duel.Strike(Side.Left, cue + 0.2);
            duel.Strike(Side.Right, cue + 0.3);
            Assert.That(duel.Result.Winner, Is.EqualTo(Side.Left));
            Assert.That(duel.Result.Outcome, Is.EqualTo(DuelOutcome.FasterStrike));
        }

        [Test]
        public void Strike_timestamped_after_the_cue_counts_even_before_the_tick_sees_it()
        {
            var duel = StartedDuel(out var cue);
            duel.Tick(cue - 0.01);
            duel.Strike(Side.Left, cue + 0.18);
            duel.Strike(Side.Right, cue + 0.40);
            Assert.That(duel.Result.Winner, Is.EqualTo(Side.Left));
        }

        [Test]
        public void Identical_reactions_tie()
        {
            var duel = StartedDuel(out var cue);
            duel.Tick(cue);
            duel.Strike(Side.Left, cue + 0.2);
            duel.Strike(Side.Right, cue + 0.2);
            Assert.That(duel.Result.Outcome, Is.EqualTo(DuelOutcome.Tie));
            Assert.That(duel.Result.Winner, Is.Null);
        }

        [Test]
        public void Nobody_striking_ends_with_no_winner()
        {
            var duel = StartedDuel(out var cue);
            duel.Tick(cue);
            duel.Tick(cue + 3.0);
            Assert.That(duel.Result.Outcome, Is.EqualTo(DuelOutcome.NoStrike));
            Assert.That(duel.Result.Winner, Is.Null);
        }

        [Test]
        public void Strikes_after_resolution_are_ignored()
        {
            var duel = StartedDuel(out var cue);
            duel.Tick(4.0);
            duel.Strike(Side.Left, 4.0);   // during the standoff, before the cue: an early strike resolves the round
            var result = duel.Result;
            duel.Strike(Side.Right, cue + 0.1);
            Assert.That(duel.Result, Is.SameAs(result));
        }

        [Test]
        public void Phase_changes_and_resolution_are_announced()
        {
            var duel = new Duel(TestTuning.Profile.Duel.ToSettings(), new Random(1));
            var phases = new List<DuelPhase>();
            DuelResult resolved = null;
            duel.PhaseChanged += phases.Add;
            duel.Resolved += r => resolved = r;

            duel.Begin(0);
            duel.Tick(duel.CueAt);
            duel.Strike(Side.Left, duel.CueAt + 0.2);
            duel.Strike(Side.Right, duel.CueAt + 0.3);

            Assert.That(phases, Is.EqualTo(new[]
            {
                DuelPhase.Countdown, DuelPhase.Standoff, DuelPhase.Strike, DuelPhase.Resolved
            }));
            Assert.That(resolved, Is.SameAs(duel.Result));
        }

        [Test]
        public void Begin_twice_throws()
        {
            var duel = StartedDuel(out _);
            Assert.Throws<InvalidOperationException>(() => duel.Begin(0));
        }
    }
}
