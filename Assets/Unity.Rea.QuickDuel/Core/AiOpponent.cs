using System;

namespace Unity.Rea.QuickDuel.Core
{
    public enum AiDifficulty { Easy, Normal, Hard }

    // A computer duelist: it reacts to the cue after a reaction time drawn from its difficulty's range.
    public sealed class AiOpponent
    {
        private readonly Random random;

        // The reaction range of each difficulty is tuning data (AiTuning, Settings/tuning.json).
        public AiOpponent(AiDifficulty difficulty, AiTuning tuning, Random random = null)
        {
            if (tuning == null)
                throw new ArgumentNullException(nameof(tuning));
            Difficulty = difficulty;
            Range = tuning.RangeOf(difficulty);
            this.random = random ?? new Random();
        }

        public AiDifficulty Difficulty { get; }

        // The seconds range this opponent draws its reaction from.
        public ReactionRange Range { get; }

        public double NextReaction() => Range.Min + random.NextDouble() * (Range.Max - Range.Min);

        // Draws a fresh reaction for this round; strike with duel.Strike(side, that time) once the clock reaches it.
        public double StrikeTime(Duel duel)
        {
            if (duel == null)
                throw new ArgumentNullException(nameof(duel));
            if (duel.Phase == DuelPhase.Ready)
                throw new InvalidOperationException("The duel has not begun, so it has no cue time yet.");
            return duel.CueAt + NextReaction();
        }
    }
}
