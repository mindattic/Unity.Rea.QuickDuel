using System;

namespace Unity.Rea.QuickDuel.Core
{
    // The host's half of an online match. Each round it draws the countdown and standoff lengths and hands them out
    // (RoundBegan); each client runs the round on its own clock, times its own reaction from its own cue and reports
    // only that time; the host judges the two reports with DuelJudge and records the round in the Match. Network
    // latency moves when a client's round starts, never the reaction it reports, so it cannot decide a duel.
    // Transport-free: the caller sends RoundBegan/RoundJudged out and passes reports in. Times are host-clock seconds.
    public sealed class OnlineDuelHost
    {
        private readonly DuelSettings settings;
        private readonly Random random;
        private readonly int winsNeeded;
        private double? left;
        private double? right;
        private bool leftReported;
        private bool rightReported;
        private double deadline;

        public OnlineDuelHost(
            DuelSettings settings,
            int winsNeeded,
            double humanFloorSeconds,
            double reportGraceSeconds,
            Random random = null)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (reportGraceSeconds < 0 || double.IsNaN(reportGraceSeconds))
                throw new ArgumentOutOfRangeException(nameof(reportGraceSeconds), "The report grace must not be negative.");
            ReportGraceSeconds = reportGraceSeconds;
            if (this.settings.MinStandoffSeconds > this.settings.MaxStandoffSeconds)
                throw new ArgumentException("MinStandoffSeconds must not exceed MaxStandoffSeconds.");
            if (humanFloorSeconds < 0 || double.IsNaN(humanFloorSeconds))
                throw new ArgumentOutOfRangeException(nameof(humanFloorSeconds), "The human floor must not be negative.");
            this.random = random ?? new Random();
            this.winsNeeded = winsNeeded;
            HumanFloorSeconds = humanFloorSeconds;
            NewMatch();
        }

        public double HumanFloorSeconds { get; }
        // Allowance past a round's latest possible end for a report still on its way (and for the client's round
        // starting late by one trip). A side still silent when it runs out is judged as not having struck.
        // (OnlineTuning.ReportGraceSeconds)
        public double ReportGraceSeconds { get; }
        public Match Match { get; private set; }

        // The current (or last) round's number, from 1; 0 before the first round.
        public int Round { get; private set; }
        public bool RoundOpen { get; private set; }
        public double CountdownSeconds => settings.CountdownSeconds;
        public double StandoffSeconds { get; private set; }

        // The current match's arena roll (ArenaDraw), drawn with every new match and kept for its rounds, as the original
        // kept one arena per game (UQD-T63); send it with RoundBegan so every peer shows the same arena.
        public int ArenaRoll { get; private set; }

        // The current match's fighter roll (FighterDraw): which cosmetic fighter stands in each seat. Drawn with every
        // new match; send it with RoundBegan so every peer shows the same pair.
        public int FighterRoll { get; private set; }

        // (round, countdownSeconds, standoffSeconds): send to every client, with ArenaRoll and FighterRoll.
        public event Action<int, double, double> RoundBegan;

        // (round, result): send to every client; the result is already recorded in Match.
        public event Action<int, DuelResult> RoundJudged;

        // Opens the next round; a finished match is replaced by a fresh one first.
        public int BeginRound(double now)
        {
            if (RoundOpen)
                throw new InvalidOperationException("The current round has not been judged yet.");
            if (Match.IsOver)
                NewMatch();

            Round++;
            RoundOpen = true;
            left = right = null;
            leftReported = rightReported = false;
            StandoffSeconds = settings.MinStandoffSeconds
                + random.NextDouble() * (settings.MaxStandoffSeconds - settings.MinStandoffSeconds);
            deadline = now + settings.CountdownSeconds + StandoffSeconds + settings.StrikeTimeoutSeconds
                + settings.SettleSeconds + ReportGraceSeconds;
            RoundBegan?.Invoke(Round, settings.CountdownSeconds, StandoffSeconds);
            return Round;
        }

        // One side's reaction for `round`: seconds from its own cue, negative before it, null for no strike.
        // Only the first report per side for the open round counts; anything else is ignored and returns false.
        public bool Report(Side side, int round, double? reaction)
        {
            if (!RoundOpen || round != Round)
                return false;
            if (side == Side.Left)
            {
                if (leftReported) return false;
                leftReported = true;
                left = reaction;
            }
            else
            {
                if (rightReported) return false;
                rightReported = true;
                right = reaction;
            }

            if (leftReported && rightReported)
                Judge();
            return true;
        }

        // Judges the round once its report window has closed, counting a side that never reported as no strike.
        public void Tick(double now)
        {
            if (RoundOpen && now >= deadline)
                Judge();
        }

        private void NewMatch()
        {
            Match = new Match(winsNeeded);
            ArenaRoll = ArenaDraw.Roll(random);
            FighterRoll = FighterDraw.Roll(random);
        }

        private void Judge()
        {
            RoundOpen = false;
            var result = DuelJudge.Judge(left, right, HumanFloorSeconds);
            Match.Record(result);
            RoundJudged?.Invoke(Round, result);
        }
    }
}
