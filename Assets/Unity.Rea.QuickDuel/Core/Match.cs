using System;
using System.Collections.Generic;

namespace Unity.Rea.QuickDuel.Core
{
    // The read-only side of a Match: wins, the verdict and best reactions, with no way to Record a round.
    // MatchSession hands this out (DuelSession.Match) so a view can read the score without being able to
    // mutate it behind the one authority's back (UQD-T30).
    public interface IMatchView
    {
        int WinsNeeded { get; }
        bool IsOver { get; }
        Side? Winner { get; }
        int WinsOf(Side side);

        // The smallest non-negative reaction this side has struck with across the match; null if none yet.
        double? BestReactionOf(Side side);
    }

    // First to WinsNeeded rounds (MatchTuning.WinsNeeded) takes the match; ties and no-strike rounds award nobody.
    public sealed class Match : IMatchView
    {
        private readonly List<DuelResult> rounds = new List<DuelResult>();
        private int leftWins;
        private int rightWins;
        private double? leftBest;
        private double? rightBest;

        public Match(int winsNeeded)
        {
            if (winsNeeded < 1)
                throw new ArgumentOutOfRangeException(nameof(winsNeeded), "A match needs at least one win.");
            WinsNeeded = winsNeeded;
        }

        public int WinsNeeded { get; }
        public IReadOnlyList<DuelResult> Rounds => rounds;
        public bool IsOver => Winner.HasValue;
        public Side? Winner { get; private set; }

        public event Action<DuelResult> RoundRecorded;
        public event Action<Side> Won;

        public int WinsOf(Side side) => side == Side.Left ? leftWins : rightWins;

        // Smallest non-negative reaction this side has struck with across the match; null if none yet.
        public double? BestReactionOf(Side side) => side == Side.Left ? leftBest : rightBest;

        public void Record(DuelResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (IsOver)
                throw new InvalidOperationException("The match is already over.");

            rounds.Add(result);
            leftBest = Best(leftBest, result.LeftReaction);
            rightBest = Best(rightBest, result.RightReaction);

            if (result.Winner == Side.Left) leftWins++;
            else if (result.Winner == Side.Right) rightWins++;

            RoundRecorded?.Invoke(result);

            if (leftWins >= WinsNeeded) Winner = Side.Left;
            else if (rightWins >= WinsNeeded) Winner = Side.Right;

            if (Winner.HasValue)
                Won?.Invoke(Winner.Value);
        }

        private static double? Best(double? best, double? reaction)
        {
            if (!reaction.HasValue || reaction.Value < 0)
                return best;
            return !best.HasValue || reaction.Value < best.Value ? reaction : best;
        }
    }
}
