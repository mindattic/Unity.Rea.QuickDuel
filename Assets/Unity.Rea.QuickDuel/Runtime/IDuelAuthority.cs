using System;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel
{
    // The one path a Duel scene plays through, local or online (UQD-T70, rebuild standard rule 9): whichever authority
    // holds the round, the mode and the match, DuelController reads its state and issues its commands through this seam
    // alone, and republishes its events to the presentation. LocalDuelAuthority wraps Core's MatchSession (both seats on
    // this machine); OnlineDuelAuthority wraps the online duel (IOnlineDuelSource), whose host draws and judges each
    // round. Nothing that draws the duel needs to know which one it is.
    public interface IDuelAuthority : IDisposable
    {
        // This machine's seat; null when both seats are on this machine (local play) or for a spectator.
        Side? LocalSide { get; }

        // The round in progress (or last played), from 1; 0 before the first.
        int Round { get; }

        // The arena roll (ArenaDraw) of the match in progress, and its fighter roll (FighterDraw).
        int ArenaRoll { get; }
        int FighterRoll { get; }

        // The round being played here, read-only to its readers (its commands go through this authority); null before
        // the first.
        Duel Current { get; }

        // The round's phase as the scene shows it: online, Resolved only once the host's verdict is in.
        DuelPhase Phase { get; }

        // The verdict the scene shows; null until there is one.
        DuelResult Result { get; }

        // The match on the scoreboard.
        IMatchView Match { get; }

        // Between rounds: no round is being played here and the last one has its verdict.
        bool CanBeginRound { get; }

        // Online only: this side's round is over and its report is with the host, whose verdict has not come back.
        bool AwaitingVerdict { get; }

        // Begins the next round on this machine's behalf; false when it may not now.
        bool BeginRound(double now);

        // A strike by `side` at its input event's timestamp; online this machine's own seat strikes, whichever key.
        void Strike(Side side, double at);

        // Advances the round clock to `now` (online the online duel ticks its own round).
        void Tick(double now);

        event Action RoundStarted;
        event Action<DuelPhase> PhaseChanged;
        event Action<DuelResult> Resolved;
    }
}
