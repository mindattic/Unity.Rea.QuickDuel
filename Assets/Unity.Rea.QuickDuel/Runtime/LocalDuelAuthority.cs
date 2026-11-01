using System;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel
{
    // Local play: Core's MatchSession, both seats on this machine, each command issued on its seat's behalf.
    public sealed class LocalDuelAuthority : IDuelAuthority
    {
        private readonly Func<DuelSettings> settings;

        public LocalDuelAuthority(MatchSession session, Func<DuelSettings> settings)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Session.RoundStarted += OnRoundStarted;
            Session.PhaseChanged += OnPhaseChanged;
            Session.Resolved += OnResolved;
        }

        public MatchSession Session { get; }

        public Side? LocalSide => null;
        public int Round => Session.Round;
        public int ArenaRoll => Session.ArenaRoll;
        public int FighterRoll => Session.FighterRoll;
        public Duel Current => Session.Duel;
        public DuelPhase Phase => Session.Phase;
        public DuelResult Result => Session.Result;
        public IMatchView Match => Session.Match;
        public bool CanBeginRound => Session.CanBeginRound;
        public bool AwaitingVerdict => false;

        public bool BeginRound(double now) => Session.BeginRound(settings(), now);

        // Side maps straight onto MatchSession's two fixed local seats (left = player 0, right = player 1).
        public void Strike(Side side, double at) =>
            Session.Strike(side == Side.Left ? MatchSession.LeftPlayerId : MatchSession.RightPlayerId, at);

        public void Tick(double now) => Session.Tick(now);

        public event Action RoundStarted;
        public event Action<DuelPhase> PhaseChanged;
        public event Action<DuelResult> Resolved;

        private void OnRoundStarted() => RoundStarted?.Invoke();
        private void OnPhaseChanged(DuelPhase phase) => PhaseChanged?.Invoke(phase);
        private void OnResolved(DuelResult result) => Resolved?.Invoke(result);

        public void Dispose()
        {
            Session.RoundStarted -= OnRoundStarted;
            Session.PhaseChanged -= OnPhaseChanged;
            Session.Resolved -= OnResolved;
        }
    }
}
