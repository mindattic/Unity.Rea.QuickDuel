using System;
using Unity.Rea.QuickDuel.Core;

namespace Unity.Rea.QuickDuel
{
    // What DuelController needs from an online duel to drive the Duel scene in place of its local MatchSession
    // (UQD-T21). Implemented by the Online assembly's OnlineDuel (the NetEntity), which Runtime cannot name:
    // assemblies reference one way, Online -> Runtime -> Core, so the seam is this interface.
    //
    // The authority stays on the host: this machine only plays its own seat's round on its own clock (Round), and
    // the result it shows is the host's verdict (Verdict), never its own local duel's, which saw one side only.
    public interface IOnlineDuelSource
    {
        // True on the host (the server), the only peer that may begin a round.
        bool IsHost { get; }

        // Both seats are taken: the host and a joined player.
        bool Seated { get; }

        // This machine's seat, or null for a peer that is not one of the two duelists.
        Side? LocalSide { get; }

        // The round this machine is playing (or last played), as a local Duel on its own clock; null before the first.
        Duel Round { get; }

        // That round's number, from 1; 0 before the first.
        int RoundNumber { get; }

        // The host's arena roll (ArenaDraw) for that round's match, so every peer shows the same arena; 0 before the first.
        int ArenaRoll { get; }

        // The host's fighter roll (FighterDraw) for that round's match, so every peer shows the same fighters; 0 before the first.
        int FighterRoll { get; }

        // The newest round the host has judged, and its verdict (null before the first).
        int JudgedRound { get; }
        DuelResult Verdict { get; }

        // This peer's copy of the match, built from the host's verdicts.
        IMatchView Match { get; }

        // Host only: opens the next round. False on a client, while a round is open, or with a seat still empty.
        bool BeginRound();

        // This machine's strike, at its input event's own timestamp.
        void Strike(double at);

        // A new round began on this machine (its local Duel is Round).
        event Action<Duel> LocalRoundBegan;

        // The host judged a round (round, verdict); already recorded in Match.
        event Action<int, DuelResult> Judged;

        // The seats changed (a player joined or left).
        event Action SeatsChanged;
    }
}
