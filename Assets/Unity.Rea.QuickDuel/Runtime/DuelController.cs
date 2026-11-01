using System;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;

namespace Unity.Rea.QuickDuel
{
    // The thin Unity-facing face of the duel's one authority (IDuelAuthority, UQD-T70): local play goes through Core's
    // MatchSession (LocalDuelAuthority, UQD-T30), online play through the online duel (OnlineDuelAuthority, UQD-T21), and
    // this reads either the same way, threads Unity's clock and tuning through to its commands and republishes its events.
    // The clock is Time.realtimeSinceStartupAsDouble, the one Input System event timestamps use, so strikes are judged by
    // when the key went down, not by which frame noticed it.
    public sealed class DuelController : MonoBehaviour
    {
        [SerializeField] private bool beginOnStart;

        // Replaced by tests to shorten a round; applies from the next local BeginRound.
        public DuelSettings Settings
        {
            get => settings ?? (settings = TuningStore.Profile.Duel.ToLocalSettings());
            set => settings = value;
        }

        private DuelSettings settings;

        // The clock for Tick and for Begin. Leave it alone outside tests.
        public Func<double> Clock { get; set; } = () => Time.realtimeSinceStartupAsDouble;

        // The local authority over the round, the mode and the match; DuelSession and DuelVerdict are its only other
        // readers (the mode and the AI are local play's alone), and `internal` (rather than `public`) keeps it that way --
        // nothing outside this assembly can reach MatchSession.Duel or .Match to Strike, Tick or Record behind the
        // player-id checks (UQD-T30). Built lazily rather than in Awake: DuelView and DuelSession read it from their own
        // Awake, and Unity does not guarantee this component's Awake runs first. It plays the mode the main menu chose
        // (DuelModeHandoff, UQD-T45), or two players when the scene was loaded without the menu.
        internal MatchSession Session => Local.Session;

        private LocalDuelAuthority local;

        private LocalDuelAuthority Local
        {
            get
            {
                if (local == null)
                {
                    local = new LocalDuelAuthority(new MatchSession(() => TuningStore.Profile.Ai,
                        TuningStore.Profile.Match.WinsNeeded, DuelModeHandoff.TakeOr(DuelMode.LocalPvp)), () => Settings);
                    if (onlineAuthority == null)
                        Listen(local);
                }
                return local;
            }
        }

        private OnlineDuelAuthority onlineAuthority;

        // The authority the scene plays through now: the online duel's while one is set, else the local session's.
        public IDuelAuthority Authority => (IDuelAuthority)onlineAuthority ?? Local;

        // The online duel driving the scene instead of the local MatchSession (UQD-T21), or null for local play.
        // While set, the round, its phase, the verdict and the match all come from it; the local session stands
        // idle (and its AI with it) and is picked up again, unchanged, once this is cleared.
        public IOnlineDuelSource Online
        {
            get => onlineAuthority?.Source;
            set
            {
                if (Online == value)
                    return;
                var before = Authority;
                if (onlineAuthority != null)
                {
                    onlineAuthority.Source.SeatsChanged -= OnOnlineSeatsChanged;
                    onlineAuthority.Dispose();
                    onlineAuthority = null;
                }
                if (value != null)
                {
                    onlineAuthority = new OnlineDuelAuthority(value);
                    value.SeatsChanged += OnOnlineSeatsChanged;
                }
                Forget(before);
                Listen(Authority);
                OnlineChanged?.Invoke();
            }
        }

        public bool IsOnline => onlineAuthority != null;

        // This machine's seat online; null in local play (both seats are on this machine) or for a spectator.
        public Side? LocalSide => Authority.LocalSide;

        public int Round => Authority.Round;

        public double Now => Clock();

        // The arena roll (ArenaDraw) of the match in progress: the local session's, or online the host's, so every
        // peer draws the same arena. Changes with every new match (UQD-T63).
        public int ArenaRoll => Authority.ArenaRoll;

        // The fighter roll (FighterDraw) of the match in progress: the local session's, or online the host's, so every
        // peer draws the same cosmetic fighters. Changes with every new match.
        public int FighterRoll => Authority.FighterRoll;

        // Online, a round shows Resolved only once the host's verdict for it is in; until then it shows the local
        // round's own phase, or Resolved with no Result yet (AwaitingVerdict) once this side's part of it is over.
        public DuelPhase Phase => Authority.Phase;

        public DuelResult Result => Authority.Result;

        // Online only: this side's round is over and its report is with the host, whose verdict has not come back.
        public bool AwaitingVerdict => Authority.AwaitingVerdict;

        // The match shown on the scoreboard: the local session's, or online the copy built from the host's verdicts.
        public IMatchView Match => Authority.Match;

        // The cue timestamp of the round in progress (or last played); 0 before the first round. The live
        // Duel itself stays inside its authority so nothing else can Strike or Tick it directly.
        public double CueAt => CurrentDuel?.CueAt ?? 0;
        public double CountdownRemaining => CurrentDuel?.CountdownRemaining(Now) ?? 0;
        public int CountdownNumber => CurrentDuel?.CountdownNumber(Now) ?? 0;
        public double CountdownNumberAlpha => CurrentDuel?.CountdownNumberAlpha(Now) ?? 0;
        public int ReactionCount => CurrentDuel?.ReactionCount(Now) ?? 0;
        public double ReactionCountAlpha => CurrentDuel?.ReactionCountAlpha(Now) ?? 0;

        // How far, in original pixels (x right), the winner's number has slid away from the loser (UQD-T39): the verdict
        // (the host's, online) decides the direction, this round's own first strike the start.
        public double ReactionCountSlide => VerdictBanner.Slide(Result, CurrentDuel?.FirstStrikeAt, Now);

        // When the round in progress (or last played) ended on this machine's clock, where its aftermath and the round
        // transition are timed from (UQD-T42); null while it runs or before the first round.
        public double? EndedAt => CurrentDuel?.EndedAt;

        // Between rounds. Online that means no round is being played here and the last one has its verdict, so a
        // client can still quit between rounds even though only the host may begin the next one.
        public bool CanBeginRound => Authority.CanBeginRound;

        // Online: whether this peer can begin the next round now (the host, both seats taken, between rounds).
        public bool CanBeginOnlineRound => IsOnline && Online.IsHost && Online.Seated && CanBeginRound;

        private Duel CurrentDuel => Authority.Current;

        public event Action RoundStarted;
        public event Action<DuelPhase> PhaseChanged;
        public event Action<DuelResult> Resolved;

        // Online play began or ended, or its seats changed: the labels and prompts need redrawing.
        public event Action OnlineChanged;

        private void Awake() => _ = Local;

        private void Start()
        {
            if (beginOnStart)
                BeginRound();
        }

        // The current authority's round clock (an online round is ticked by the online duel that owns it).
        private void Update() => Authority.Tick(Now);

        private void OnDestroy()
        {
            Online = null;
            Forget(Authority);
        }

        // Starts a fresh round on the local machine's behalf; false while one is still running. Online only the
        // host can, once both seats are taken; a client's begin does nothing.
        public bool BeginRound() => Authority.BeginRound(Now);

        // `at` is the strike's own timestamp on the controller's clock (an input event time).
        public void Strike(Side side, double at) => Authority.Strike(side, at);

        private void Listen(IDuelAuthority authority)
        {
            authority.RoundStarted += OnRoundStarted;
            authority.PhaseChanged += OnPhaseChanged;
            authority.Resolved += OnResolved;
        }

        private void Forget(IDuelAuthority authority)
        {
            authority.RoundStarted -= OnRoundStarted;
            authority.PhaseChanged -= OnPhaseChanged;
            authority.Resolved -= OnResolved;
        }

        private void OnRoundStarted() => RoundStarted?.Invoke();
        private void OnPhaseChanged(DuelPhase phase) => PhaseChanged?.Invoke(phase);
        private void OnResolved(DuelResult result) => Resolved?.Invoke(result);
        private void OnOnlineSeatsChanged() => OnlineChanged?.Invoke();
    }
}
