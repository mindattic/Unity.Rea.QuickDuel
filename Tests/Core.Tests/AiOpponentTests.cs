using System;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    public class AiOpponentTests
    {
        [TestCase(AiDifficulty.Easy, 0.45, 0.70)]
        [TestCase(AiDifficulty.Normal, 0.30, 0.45)]
        [TestCase(AiDifficulty.Hard, 0.20, 0.30)]
        public void Reactions_stay_inside_the_difficulty_range(AiDifficulty difficulty, double min, double max)
        {
            var ai = new AiOpponent(difficulty, TestTuning.Profile.Ai, new Random(3));
            for (var i = 0; i < 500; i++)
                Assert.That(ai.NextReaction(), Is.InRange(min, max));
        }

        [Test]
        public void Harder_opponents_react_faster_on_average()
        {
            double Average(AiDifficulty difficulty)
            {
                var ai = new AiOpponent(difficulty, TestTuning.Profile.Ai, new Random(5));
                var sum = 0.0;
                for (var i = 0; i < 200; i++)
                    sum += ai.NextReaction();
                return sum / 200;
            }

            Assert.That(Average(AiDifficulty.Hard), Is.LessThan(Average(AiDifficulty.Normal)));
            Assert.That(Average(AiDifficulty.Normal), Is.LessThan(Average(AiDifficulty.Easy)));
        }

        [Test]
        public void Same_seed_gives_the_same_reactions()
        {
            var a = new AiOpponent(AiDifficulty.Normal, TestTuning.Profile.Ai, new Random(9));
            var b = new AiOpponent(AiDifficulty.Normal, TestTuning.Profile.Ai, new Random(9));
            Assert.That(a.NextReaction(), Is.EqualTo(b.NextReaction()));
        }

        [Test]
        public void Strike_time_is_the_cue_plus_a_reaction()
        {
            var duel = new Duel(TestTuning.Profile.Duel.ToSettings(), new Random(7));
            duel.Begin(0);
            var ai = new AiOpponent(AiDifficulty.Hard, TestTuning.Profile.Ai, new Random(1));

            Assert.That(ai.StrikeTime(duel) - duel.CueAt, Is.InRange(0.20, 0.30));
        }

        [Test]
        public void Ai_strike_beats_a_slower_player()
        {
            var duel = new Duel(TestTuning.Profile.Duel.ToSettings(), new Random(7));
            duel.Begin(0);
            var ai = new AiOpponent(AiDifficulty.Hard, TestTuning.Profile.Ai, new Random(1));
            var aiAt = ai.StrikeTime(duel);

            duel.Tick(duel.CueAt);
            duel.Strike(Side.Left, duel.CueAt + 0.5);
            duel.Strike(Side.Right, aiAt);

            Assert.That(duel.Result.Winner, Is.EqualTo(Side.Right));
        }

        [Test]
        public void Strike_time_before_the_duel_begins_throws()
        {
            var ai = new AiOpponent(AiDifficulty.Easy, TestTuning.Profile.Ai, new Random(1));
            Assert.Throws<InvalidOperationException>(() => ai.StrikeTime(new Duel(TestTuning.Profile.Duel.ToSettings())));
        }
    }
}
