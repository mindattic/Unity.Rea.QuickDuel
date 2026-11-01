using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // The on-screen session labels: the match score (standing in for the original's HP bars) and the verdict
    // banner, and online the line that says who hosts and who joined. The mode, the match and the AI's own
    // wiring belong to Core's MatchSession (DuelController.Session, UQD-T30) -- this only reads its state and
    // plays its events. The mode comes from the main menu's New Game or Versus, as the original's
    // is_singleplayer did; the duel has no key for it and draws no mode line (UQD-T67). SetMode and CycleMode
    // remain as the local machine's commands for code and tests; a mode change starts a fresh match.
    [RequireComponent(typeof(DuelController), typeof(DuelView))]
    public sealed class DuelSession : MonoBehaviour
    {
        private DuelController controller;
        private DuelInput input;
        private DuelView view;
        private OutlinedText modeText;
        private OutlinedText leftScore;
        private OutlinedText rightScore;
        private OutlinedText verdict;

        private MatchSession Session => controller.Session;

        public DuelMode Mode => Session.Mode;

        // Read-only: nothing outside MatchSession may Record a round (UQD-T30). Online it is this peer's copy
        // of the host's match (UQD-T21).
        public IMatchView Match => controller.Match;

        // Online the right seat is always the other player, never the AI, whatever local mode is set.
        public bool AiPlaysRight => !controller.IsOnline && Session.AiPlaysRight;
        public double? AiStrikeAt => Session.AiStrikeAt;

        // The online line ("Online PvP (hosting)" and so on); empty in local play, which draws none.
        public string ModeText => controller.IsOnline ? modeText.text : "";
        public string ScoreText(Side side) => (side == Side.Left ? leftScore : rightScore).text;
        public string VerdictText => verdict.style.display != DisplayStyle.None ? verdict.text : "";

        private void Awake()
        {
            controller = GetComponent<DuelController>();
            input = GetComponent<DuelInput>();

            controller.RoundStarted += OnRoundStarted;
            controller.Resolved += OnResolved;
            controller.OnlineChanged += Redraw;
        }

        // Stage-centre-relative y (up) of the online line: under the round clock, which owns the very top centre.
        public const float ModeY = 450f;

        private void Start()
        {
            view = GetComponent<DuelView>();
            // The HUD in the original's basic face; the match verdict as a banner (UQD-T36).
            // Below the round clock (UQD-T50, top centre, room y 5 down) rather than on it (UQD-T62): 450 puts the
            // line's centre 40 room pixels down the stage, clear of the clock's line at any window size.
            modeText = view.CreateLabel("Mode", OriginalType.Hud, new Vector2(0, ModeY));
            leftScore = view.CreateEdgeLabel("LeftScore", OriginalType.Hud, 0f, 490f);
            rightScore = view.CreateEdgeLabel("RightScore", OriginalType.Hud, 1f, 490f);
            verdict = view.CreateLabel("Verdict", OriginalType.Banner, new Vector2(0, -385f), new Color(0.62f, 0.12f, 0.10f));
            verdict.style.display = DisplayStyle.None;

            Session.ModeChanged += OnModeChanged;
            Redraw();
        }

        // Between rounds only: PvP -> AI Easy -> Normal -> Hard -> PvP. Issued on the local machine's behalf.
        // Refused online: the mode is the local game's, and an online duel is always player against player.
        // Refused under the verdict screen too (UQD-T48): its button decides what follows a finished match.
        public bool CycleMode() => !controller.IsOnline && !VerdictShown && Session.CycleMode(MatchSession.LeftPlayerId);

        private bool VerdictShown
        {
            get
            {
                var verdictScreen = GetComponent<DuelVerdict>();
                return verdictScreen != null && verdictScreen.Shown;
            }
        }

        // False (and no-op) while a round is running, between rounds only -- the caller can tell it apart
        // from a mode that was actually applied, unlike the old unconditional void SetMode. Refused online.
        public bool SetMode(DuelMode mode) => !controller.IsOnline && Session.SetMode(MatchSession.LeftPlayerId, mode);

        // Session already applied the mode (and reset the match, and wired up the AI) by the time this
        // fires; this only has to redraw the labels from its new state.
        private void OnModeChanged(DuelMode mode) => Redraw();

        // Redraws the labels from the session's state, or online from the online duel's (UQD-T21).
        private void Redraw()
        {
            if (input != null) input.RightIsHuman = !AiPlaysRight;
            if (view != null) view.RightName = AiPlaysRight ? "AI" : "Right";
            if (modeText == null) return;
            // The gamescreen drew no mode line; online (which has no original) says who hosts and who joined.
            modeText.text = controller.IsOnline ? OnlineModeText() : "";
            modeText.style.display = controller.IsOnline ? DisplayStyle.Flex : DisplayStyle.None;
            verdict.style.display = DisplayStyle.None;
            RefreshScore();
        }

        private string OnlineModeText()
        {
            var online = controller.Online;
            var role = online.IsHost ? "hosting" : "joined";
            return online.Seated ? $"Online PvP ({role})" : $"Online PvP ({role}) - waiting for a player";
        }

        private void OnRoundStarted()
        {
            if (verdict != null) verdict.style.display = DisplayStyle.None;
            RefreshScore();
        }

        private void OnResolved(DuelResult result)
        {
            RefreshScore();
            var match = Match;
            if (match.IsOver && verdict != null)
            {
                var winner = match.Winner == Side.Left ? "Left" : AiPlaysRight ? "The AI" : "Right";
                verdict.text = $"{winner} takes the match {match.WinsOf(match.Winner.Value)}-{match.WinsOf(Other(match.Winner.Value))}";
                verdict.style.display = DisplayStyle.Flex;
            }
        }

        private void RefreshScore()
        {
            if (leftScore == null) return;
            leftScore.text = Score(SeatName("Left", Side.Left), Side.Left);
            rightScore.text = Score(SeatName(AiPlaysRight ? "AI" : "Right", Side.Right), Side.Right);
        }

        // Online, the seat this machine plays is marked as the player's own.
        private string SeatName(string name, Side side) => controller.LocalSide == side ? name + " (you)" : name;

        private string Score(string name, Side side)
        {
            var match = Match;
            var best = match.BestReactionOf(side);
            return $"{name}  {match.WinsOf(side)}/{match.WinsNeeded}   best {(best.HasValue ? DuelView.FormatReaction(best) : "-")}";
        }

        private static Side Other(Side side) => side == Side.Left ? Side.Right : Side.Left;
    }
}
