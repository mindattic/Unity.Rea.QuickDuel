using System;
using Unity.Multiplayer;      // NetEntity
using Unity.Multiplayer.Core; // [ToServer], [ToClients], [Synced], NetSender
using Unity.Rea.QuickDuel.Core;
using UnityEngine;

namespace Unity.Rea.QuickDuel.Online
{
    // The online duel on the wire (UQD-T14), over Unity.Multiplayer: one server-spawned network object that
    // carries the protocol of OnlineDuelHost and OnlineDuelClient. The host draws each round's countdown and standoff
    // and sends them to every peer (RoundBegan); each of the two seated players runs that round as a local Duel on its
    // own clock and reports only its reaction time (ReportReaction); the host judges the pair with DuelJudge and sends
    // the verdict (RoundJudged), which every peer records in its own copy of the Match. Latency only shifts when a
    // player's round starts, never the reaction it reports, so it cannot decide a duel.
    //
    // It is also the Duel scene's IOnlineDuelSource (UQD-T21): set as DuelController.Online, it drives DuelView and
    // DuelSession in place of the local MatchSession.
    public partial class OnlineDuel : NetEntity, IOnlineDuelSource
    {
        // Client ids of the two duelists; NoClient until the server seats them.
        public const ulong NoClient = ulong.MaxValue;

        [Synced(OnChanged = nameof(OnSeatChanged))] private ulong _leftClient = ulong.MaxValue;
        [Synced(OnChanged = nameof(OnSeatChanged))] private ulong _rightClient = ulong.MaxValue;

        // Server only: the round lengths, match length, human floor and report grace; change them with Configure between rounds.
        public DuelSettings HostSettings { get; private set; }
        public double HumanFloorSeconds { get; private set; }
        public double ReportGraceSeconds { get; private set; }

        // Rounds a duelist needs to take the match (MatchTuning.WinsNeeded until the host says otherwise); every peer scores against it.
        [Synced] private int _winsNeeded;

        // Every peer's clock: the one Input System event timestamps use, so strikes can pass ctx.time straight in.
        public Func<double> Clock { get; set; } = () => Time.realtimeSinceStartupAsDouble;
        public double Now => Clock();

        // Server only: the authority that draws rounds and judges reports.
        public OnlineDuelHost Host { get; private set; }

        // The local player's half when this peer is one of the two duelists, else null (a spectator, a dedicated server).
        public OnlineDuelClient Local { get; private set; }
        // This peer's seat: the side its local duelist plays, or before its first round the seat its connection holds.
        public Side? LocalSide => Local?.Side ?? SeatOf(Session != null ? Session.LocalClientId : NoClient);

        private Side? SeatOf(ulong client) =>
            client == NoClient ? (Side?)null : client == LeftClient ? Side.Left : client == RightClient ? Side.Right : (Side?)null;

        // Every peer's copy of the match, built from the host's verdicts.
        public Match Match { get; private set; }
        public int LastJudgedRound { get; private set; }
        public DuelResult LastResult { get; private set; }

        // Every peer: a round the host judged, after it is recorded in Match.
        public event Action<int, DuelResult> Judged;

        // A duelist's peer: a new round began here, as this local Duel.
        public event Action<Duel> LocalRoundBegan;

        // Every peer: who sits in either seat changed.
        public event Action SeatsChanged;

        // Both seats are taken.
        public bool Seated => LeftClient != NoClient && RightClient != NoClient;

        // Server: whether the next round can begin now (both seats taken, no round open).
        public bool CanBeginRound => IsServer && Host != null && Seated && !Host.RoundOpen;

        bool IOnlineDuelSource.IsHost => IsServer;
        Duel IOnlineDuelSource.Round => Local?.Duel;
        int IOnlineDuelSource.RoundNumber => Local?.Round ?? 0;
        int IOnlineDuelSource.ArenaRoll => Local?.ArenaRoll ?? 0;
        int IOnlineDuelSource.FighterRoll => Local?.FighterRoll ?? 0;
        int IOnlineDuelSource.JudgedRound => LastJudgedRound;
        DuelResult IOnlineDuelSource.Verdict => LastResult;
        IMatchView IOnlineDuelSource.Match => Match;

        // The scene's Space / Start: refused (false) rather than thrown on a client, with a seat empty, or mid-round.
        bool IOnlineDuelSource.BeginRound() => CanBeginRound && BeginRound();

        private void OnSeatChanged(ulong previous, ulong current) => SeatsChanged?.Invoke();

        // host -> every peer: a new round, with the host's countdown and standoff lengths in seconds, its arena roll
        // (ArenaDraw, so every peer shows the same arena), the match's fighter roll (FighterDraw, so every peer shows
        // the same cosmetic fighters) and who duels in it (the seating travels with the round, so a re-seat just
        // before it can never race it).
        [ToClients]
        public partial void RoundBegan(int round, double countdownSeconds, double standoffSeconds, int arenaRoll, int fighterRoll, ulong leftClient, ulong rightClient);

        // duelist -> host: this side's reaction for `round`, seconds from its own cue (negative before it); struck is
        // false for no strike. Any connection may call it; the host keeps only the two seated duelists' reports.
        [ToServer(RequireOwnership = false, RateLimit = 10)]
        public partial void ReportReaction(int round, bool struck, double reaction);

        // host -> every peer: the verdict. winner is -1 for none, else (int)Side.
        [ToClients]
        public partial void RoundJudged(int round, int outcome, int winner, bool leftStruck, double left, bool rightStruck, double right);

        // Server: who duels. Usually the host's own client id on the left and the joining player's on the right.
        public void Seat(ulong left, ulong right)
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the server seats the duelists.");
            if (left == right)
                throw new ArgumentException("The two duelists must be different connections.");
            LeftClient = left;
            RightClient = right;
        }

        // Server: empties the seat a connection held (it left). A round it was playing is judged as it never struck.
        public void Unseat(ulong client)
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the server seats the duelists.");
            if (LeftClient == client) LeftClient = NoClient;
            if (RightClient == client) RightClient = NoClient;
        }

        // Server: opens the next round and sends its lengths out. False while a round is still being played.
        public bool BeginRound()
        {
            if (!IsServer || Host == null)
                throw new InvalidOperationException("Only the server begins rounds.");
            if (Host.RoundOpen)
                return false;
            Host.BeginRound(Now);
            return true;
        }

        // The local duelist's strike, at its own timestamp on Clock (an input event time).
        public void Strike(double at) => Local?.Strike(at);

        // Server, before the first round: the round lengths, match length and human floor.
        public void Configure(DuelSettings settings, int winsNeeded, double humanFloorSeconds, double reportGraceSeconds)
        {
            if (!IsServer)
                throw new InvalidOperationException("Only the server configures the duel.");
            if (Host != null && Host.Round > 0)
                throw new InvalidOperationException("Configure the duel before its first round.");
            HostSettings = settings;
            HumanFloorSeconds = humanFloorSeconds;
            ReportGraceSeconds = reportGraceSeconds;
            CreateHost(winsNeeded);
        }

        // Server, before the first round: the round lengths, match length and human floor all from one tuning profile.
        public void Configure(TuningProfile profile) =>
            Configure(profile.Duel.ToSettings(), profile.Match.WinsNeeded, profile.Judge.HumanFloorSeconds,
                profile.Online.ReportGraceSeconds);

        private static TuningProfile Tuning => TuningStore.Profile;

        // The match length: the host's once it has synced, else the tuning profile's.
        private int EffectiveWinsNeeded => WinsNeeded > 0 ? WinsNeeded : Tuning.Match.WinsNeeded;

        protected override void OnSpawned()
        {
            Match = new Match(EffectiveWinsNeeded);
            if (IsServer)
            {
                if (HostSettings == null)
                {
                    HostSettings = Tuning.Duel.ToSettings();
                    HumanFloorSeconds = Tuning.Judge.HumanFloorSeconds;
                    ReportGraceSeconds = Tuning.Online.ReportGraceSeconds;
                }
                CreateHost(EffectiveWinsNeeded);
            }
        }

        private void CreateHost(int winsNeeded)
        {
            var host = new OnlineDuelHost(HostSettings, winsNeeded, HumanFloorSeconds, ReportGraceSeconds);
            Host = host;
            Host.RoundBegan += (round, countdown, standoff) => RoundBegan(round, countdown, standoff, host.ArenaRoll, host.FighterRoll, LeftClient, RightClient);
            Host.RoundJudged += SendVerdict;
            WinsNeeded = winsNeeded;
            Match = new Match(winsNeeded);
        }

        protected override void OnDespawned()
        {
            Host = null;
            Local = null;
        }

        private void SendVerdict(int round, DuelResult result)
        {
            RoundJudged(
                round,
                (int)result.Outcome,
                result.Winner.HasValue ? (int)result.Winner.Value : -1,
                result.LeftReaction.HasValue, result.LeftReaction ?? 0,
                result.RightReaction.HasValue, result.RightReaction ?? 0);
        }

        private void OnRoundBegan(int round, double countdownSeconds, double standoffSeconds, int arenaRoll, int fighterRoll, ulong leftClient, ulong rightClient)
        {
            var me = Session != null ? Session.LocalClientId : NoClient;
            var side = me == leftClient ? Side.Left : me == rightClient ? Side.Right : (Side?)null;
            if (side == null)
            {
                Local = null;
                return;
            }
            if (Local == null || Local.Side != side.Value)
            {
                Local = new OnlineDuelClient(side.Value, Tuning.Duel.ToSettings());
                Local.ReactionReady += (r, reaction) => ReportReaction(r, reaction.HasValue, reaction ?? 0);
            }
            if (Local.Begin(round, countdownSeconds, standoffSeconds, Now, arenaRoll, fighterRoll))
                LocalRoundBegan?.Invoke(Local.Duel);
        }

        private void OnReportReaction(int round, bool struck, double reaction, NetSender sender)
        {
            if (Host == null)
                return;
            Side side;
            if (sender.ClientId == LeftClient) side = Side.Left;
            else if (sender.ClientId == RightClient) side = Side.Right;
            else return;
            Host.Report(side, round, struck ? reaction : (double?)null);
        }

        private void OnRoundJudged(int round, int outcome, int winner, bool leftStruck, double left, bool rightStruck, double right)
        {
            if (round <= LastJudgedRound)
                return;
            var result = new DuelResult(
                (DuelOutcome)outcome,
                winner < 0 ? (Side?)null : (Side)winner,
                leftStruck ? left : (double?)null,
                rightStruck ? right : (double?)null);
            if (Match.IsOver || Match.WinsNeeded != EffectiveWinsNeeded)
                Match = new Match(EffectiveWinsNeeded);
            Match.Record(result);
            LastJudgedRound = round;
            LastResult = result;
            Judged?.Invoke(round, result);
        }

        private void Update()
        {
            if (!IsSpawned)
                return;
            var now = Now;
            Local?.Tick(now);
            if (IsServer)
                Host?.Tick(now);
        }
    }
}
