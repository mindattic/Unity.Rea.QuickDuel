using System;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel
{
    // Online play: this machine plays its own seat's round on its own clock, and the result it shows is the host's
    // verdict, never its own local round's, which saw one side only (UQD-T21).
    public sealed class OnlineDuelAuthority : IDuelAuthority
    {
        private Duel round;

        public OnlineDuelAuthority(IOnlineDuelSource source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Source.LocalRoundBegan += OnRoundBegan;
            Source.Judged += OnJudged;
            if (Source.Round != null)
                Hook(Source.Round);
        }

        public IOnlineDuelSource Source { get; }

        public Side? LocalSide => Source.LocalSide;
        public int Round => Source.RoundNumber;
        public int ArenaRoll => Source.ArenaRoll;
        public int FighterRoll => Source.FighterRoll;
        public Duel Current => Source.Round;

        public DuelPhase Phase
        {
            get
            {
                if (Source.Round == null)
                    return DuelPhase.Ready;
                return HasVerdict ? DuelPhase.Resolved : Source.Round.Phase;
            }
        }

        public DuelResult Result => HasVerdict ? Source.Verdict : null;
        public IMatchView Match => Source.Match;

        // A client can still leave between rounds, though only the host may begin the next one.
        public bool CanBeginRound => Source.Round == null || HasVerdict;

        public bool AwaitingVerdict => Source.Round != null && !HasVerdict && Source.Round.Phase == DuelPhase.Resolved;

        // Only the host, once both seats are taken; a client's begin does nothing. The host's own clock is its source's.
        public bool BeginRound(double now) => Source.BeginRound();

        // This machine holds one seat only, so every strike key strikes for it, whichever side it names.
        public void Strike(Side side, double at) => Source.Strike(at);

        // The online duel ticks its own round on the same clock.
        public void Tick(double now) { }

        public event Action RoundStarted;
        public event Action<DuelPhase> PhaseChanged;
        public event Action<DuelResult> Resolved;

        private bool HasVerdict => Source.RoundNumber > 0 && Source.JudgedRound >= Source.RoundNumber;

        private void OnRoundBegan(Duel duel)
        {
            Hook(duel);
            RoundStarted?.Invoke();
            PhaseChanged?.Invoke(duel.Phase);
        }

        private void Hook(Duel duel)
        {
            Unhook();
            round = duel;
            round.PhaseChanged += OnRoundPhaseChanged;
        }

        private void Unhook()
        {
            if (round != null)
                round.PhaseChanged -= OnRoundPhaseChanged;
            round = null;
        }

        // The local round's own Resolved is not the verdict (it saw only this side's strike), so it is held back until
        // the host's arrives (OnJudged).
        private void OnRoundPhaseChanged(DuelPhase phase)
        {
            if (phase != DuelPhase.Resolved && !HasVerdict)
                PhaseChanged?.Invoke(phase);
        }

        private void OnJudged(int judgedRound, DuelResult verdict)
        {
            if (judgedRound != Source.RoundNumber)
                return;
            PhaseChanged?.Invoke(DuelPhase.Resolved);
            Resolved?.Invoke(verdict);
        }

        public void Dispose()
        {
            Source.LocalRoundBegan -= OnRoundBegan;
            Source.Judged -= OnJudged;
            Unhook();
        }
    }
}
