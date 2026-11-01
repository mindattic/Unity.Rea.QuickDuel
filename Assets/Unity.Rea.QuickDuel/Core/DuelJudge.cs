using System;

namespace Unity.Rea.QuickDuel.Core
{
    // Judges a round from reaction times alone: seconds from the cue, negative for a strike before it,
    // null for no strike. An online host uses it on the times each client measured from its own local cue.
    public static class DuelJudge
    {
        // A reported reaction faster than humanFloorSeconds (or not a finite number) is rejected as
        // suspicious and forfeits the round. Pass 0 to disable the check; online play passes JudgeTuning.HumanFloorSeconds.
        public static DuelResult Judge(double? left, double? right, double humanFloorSeconds)
        {
            if (humanFloorSeconds < 0 || double.IsNaN(humanFloorSeconds))
                throw new ArgumentOutOfRangeException(nameof(humanFloorSeconds), "The human floor must not be negative.");

            var leftSuspicious = IsSuspicious(left, humanFloorSeconds);
            var rightSuspicious = IsSuspicious(right, humanFloorSeconds);
            if (leftSuspicious || rightSuspicious)
            {
                Side? winner = leftSuspicious && rightSuspicious ? (Side?)null : leftSuspicious ? Side.Right : Side.Left;
                return new DuelResult(DuelOutcome.Forfeit, winner, left, right);
            }

            var leftEarly = left < 0;
            var rightEarly = right < 0;
            if (leftEarly || rightEarly)
            {
                Side loser;
                if (leftEarly && rightEarly)
                {
                    if (left.Value == right.Value)
                        return new DuelResult(DuelOutcome.EarlyStrike, null, left, right);
                    loser = left.Value < right.Value ? Side.Left : Side.Right;
                }
                else
                {
                    loser = leftEarly ? Side.Left : Side.Right;
                }
                return new DuelResult(DuelOutcome.EarlyStrike, Other(loser), left, right);
            }

            if (!left.HasValue && !right.HasValue)
                return new DuelResult(DuelOutcome.NoStrike, null, null, null);

            if (left.HasValue && right.HasValue && left.Value == right.Value)
                return new DuelResult(DuelOutcome.Tie, null, left, right);

            Side faster;
            if (!right.HasValue) faster = Side.Left;
            else if (!left.HasValue) faster = Side.Right;
            else faster = left.Value < right.Value ? Side.Left : Side.Right;

            return new DuelResult(DuelOutcome.FasterStrike, faster, left, right);
        }

        private static bool IsSuspicious(double? reaction, double humanFloorSeconds) =>
            reaction.HasValue
            && (double.IsNaN(reaction.Value) || double.IsInfinity(reaction.Value)
                || (reaction.Value >= 0 && reaction.Value < humanFloorSeconds));

        private static Side Other(Side side) => side == Side.Left ? Side.Right : Side.Left;
    }
}
