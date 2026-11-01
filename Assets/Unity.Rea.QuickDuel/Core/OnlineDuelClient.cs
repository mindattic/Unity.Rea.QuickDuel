using System;

namespace Unity.Rea.QuickDuel.Core
{
    // One player's half of an online match. On each round the host sends, it runs a local Duel with the host's
    // countdown and standoff on this machine's own clock, so the player sees the cue and strikes against it exactly
    // as in a local duel; once that round resolves it reports this side's reaction (ReactionReady) for the host to
    // judge. Times are this client's own clock seconds (input event timestamps can be used directly).
    public sealed class OnlineDuelClient
    {
        private readonly DuelSettings settings;
        private bool reported;

        public OnlineDuelClient(Side side, DuelSettings settings)
        {
            Side = side;
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public Side Side { get; }

        // The round being played (or last played), from 1; 0 before the first.
        public int Round { get; private set; }
        public double StandoffSeconds { get; private set; }

        // The host's arena roll (ArenaDraw) for the round being played; 0 before the first.
        public int ArenaRoll { get; private set; }

        // The host's fighter roll (FighterDraw) for the match being played; 0 before the first round.
        public int FighterRoll { get; private set; }

        // This round's local duel, for the view; null before the first round.
        public Duel Duel { get; private set; }

        // A new local duel has begun.
        public event Action<Duel> RoundBegan;

        // (round, reaction): send to the host. Seconds from the local cue, negative before it, null for no strike.
        public event Action<int, double?> ReactionReady;

        // Starts `round` with the host's lengths, arena roll and fighter roll. A round no newer than the current one is stale and ignored.
        public bool Begin(int round, double countdownSeconds, double standoffSeconds, double now, int arenaRoll = 0, int fighterRoll = 0)
        {
            if (round <= Round)
                return false;
            if (double.IsNaN(countdownSeconds) || double.IsInfinity(countdownSeconds) || countdownSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(countdownSeconds), "The countdown must be a finite, non-negative length.");

            var local = new DuelSettings
            {
                CountdownSeconds = countdownSeconds,
                MinStandoffSeconds = standoffSeconds,
                MaxStandoffSeconds = standoffSeconds,
                StrikeTimeoutSeconds = settings.StrikeTimeoutSeconds,
                SettleSeconds = settings.SettleSeconds
            };
            var duel = new Duel(local);
            duel.Begin(now, standoffSeconds);

            Round = round;
            StandoffSeconds = standoffSeconds;
            ArenaRoll = arenaRoll;
            FighterRoll = fighterRoll;
            Duel = duel;
            reported = false;
            RoundBegan?.Invoke(duel);
            ReportIfResolved();
            return true;
        }

        public void Strike(double at)
        {
            if (Duel == null)
                return;
            Duel.Strike(Side, at);
            ReportIfResolved();
        }

        public void Tick(double now)
        {
            if (Duel == null)
                return;
            Duel.Tick(now);
            ReportIfResolved();
        }

        private void ReportIfResolved()
        {
            if (reported || Duel.Phase != DuelPhase.Resolved)
                return;
            reported = true;
            ReactionReady?.Invoke(Round, Duel.Result.ReactionOf(Side));
        }
    }
}
