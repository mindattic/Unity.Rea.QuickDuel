using System;
using System.Collections.Generic;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // Draws the duel from the controller's phase and result; it never decides anything. The whole UI is
    // built in code (the original fighters over the original arena art) on UI Toolkit (UQD-T31): a UIDocument reads
    // Resources/UI/DuelPanelSettings.asset (scale-with-screen-size fitting 1440 x 1080, built by DuelPanelBuilder) and
    // Resources/UI/Duel.uss, and every label is the original's outlined text (OutlinedText, UQD-T36) in one of its five
    // bitmap fonts (Resources/UI/Fonts/fnt_*.asset, OriginalType says which). The panel renders as a screen-space overlay
    // (UI Toolkit has no "Screen Space - Camera" mode); the main camera behind it only clears to black. Root is the
    // original's 640 x 480 room as a 1440 x 1080 frame letterboxed into the window (OriginalFrame, UQD-T50), so the
    // room's edges are Root's at any window size; centre-anchored content (the fighters, every CreateLabel) lives on the
    // "stage", the same 1440 x 1080 centred in Root, and edge content (the arena backdrop, letterbox, the edge score
    // labels) is positioned by percentage straight off Root.
    //
    // The backdrop is one of the original's 11 arenas (UQD-T33), the one the match's arena roll lands on
    // (ArenaDraw, drawn by the authority each match, UQD-T63), scaled to cover the screen. On the cue the whole backdrop's
    // colours invert, as obj_arena did by drawing white over the view with an inverting blend
    // (draw_set_blend_mode_ext(bm_inv_dest_colour, bm_zero)): a real per-pixel invert, here a UI Toolkit invert
    // filter on the backdrop, not a recolouring of the boxes drawn over it.
    //
    // The two duelists are the original's cosmetic fighters (UQD-T34, BIBLE section 4.5): the pair the match's fighter
    // roll lands on (FighterDraw, drawn by the authority each match), standing in their idle sprite at a quarter and
    // three quarters of the stage width, two thirds of the way down, facing each other (the right one mirrored), drawn
    // at the original's 640 x 480 scale, fading in at obj_player's 0.01 alpha per 60 fps tick whenever a new pair takes
    // the field, and tinted red (left) and blue (right) when both are the same fighter (FighterLineup).
    //
    // They move through the round as obj_player did (UQD-T35, FighterMotion): on the cue both switch to their run sprite
    // on their run marks at 40% and 60% of the width and creep together 10 original pixels a second; the verdict sends the
    // winner from the middle dashing on to the loser's mark playing its attack strip, drawn in front, and the loser plays
    // its defeat strip at the middle; three seconds on the winner shows its idle sprite again, and the next round puts
    // both back on their idle marks.
    [RequireComponent(typeof(DuelController))]
    public sealed class DuelView : MonoBehaviour, IKatanaCursorHost
    {
        internal const float StageWidth = OriginalFrame.Width;
        internal const float StageHeight = OriginalFrame.Height;
        private const float LetterboxHeight = 150f;

        // The original's 640 x 480 room drawn on the 1080-high stage: every original pixel is 2.25 stage pixels.
        internal const float PixelScale = StageHeight / FighterLineup.OriginalHeight;

        // Where the fighters stand, y up from the stage centre: vertical_position, two thirds of the way down.
        private static readonly float FighterY = StageHeight / 2f - (float)FighterLineup.Y * StageHeight;

        // The countdown's number, y up from the stage centre.
        private const float CountdownY = 120f;

        // Get Ready's line, 44 original pixels above the number, where the original also drew "Wins!" and
        // "Premature Strike!" (DuelTaunt draws its line there).
        public static readonly float BannerY = CountdownY + VerdictBanner.BannerAboveNumber * PixelScale;

        // The winner's name, 20 original pixels above "Wins!".
        private static readonly float WinnerNameY = BannerY + VerdictBanner.NameAboveBanner * PixelScale;


        // Shown behind the arena art, and in its place in a scene that carries no arenas.
        private static readonly Color Paper = new Color(0.93f, 0.88f, 0.78f);

        // The original arenas, in load_arenas.gml's order; written by DuelSceneBuilder from the asset manifest.
        [SerializeField] private List<ArenaBackdrop> arenas = new List<ArenaBackdrop>();

        // The original fighters, in load_characters.gml's id order; written by DuelSceneBuilder from the asset manifest.
        [SerializeField] private List<FighterLook> fighters = new List<FighterLook>();
        private int? shownFighterRoll;

        private DuelController controller;
        private UIDocument document;
        private VisualElement stage;
        private VisualElement backdrop;
        private int? shownRoll;
        private VisualElement letterboxTop;
        private VisualElement letterboxBottom;
        private Fighter left;
        private Fighter right;
        private OutlinedText getReady;
        private OutlinedText countdown;
        private OutlinedText counter;
        private OutlinedText headline;
        private OutlinedText winnerName;
        private OutlinedText wins;
        private OutlinedText prompt;
        private float letterbox;
        private bool inverted;
        private DuelFade fade;

        // The loading fade (UQD-T42) has reset the battle under its full image: the aftermath's text is gone and both
        // fighters stand idle until the next round begins.
        private bool battleReset;

        public bool Inverted => inverted;

        public IReadOnlyList<ArenaBackdrop> Arenas => arenas;

        // The arena on screen; null in a scene that carries none.
        public ArenaBackdrop Arena { get; private set; }

        // The backdrop element, whose filter carries the cue's invert.
        public VisualElement Backdrop => backdrop;

        // The scene builder's way in: the arenas this view draws from.
        public void SetArenas(IEnumerable<ArenaBackdrop> backdrops)
        {
            arenas = new List<ArenaBackdrop>(backdrops ?? throw new ArgumentNullException(nameof(backdrops)));
            shownRoll = null;
        }

        public IReadOnlyList<FighterLook> Fighters => fighters;

        // The scene builder's way in: the fighters this view draws from.
        public void SetFighters(IEnumerable<FighterLook> looks)
        {
            fighters = new List<FighterLook>(looks ?? throw new ArgumentNullException(nameof(looks)));
            shownFighterRoll = null;
        }

        // The fighter standing in `side`'s seat; null in a scene that carries none.
        public FighterLook FighterIn(Side side) => (side == Side.Left ? left : right).Look;

        // That fighter's element (its sprite, opacity, tint and facing), for tests and screenshots.
        public VisualElement FighterElement(Side side) => (side == Side.Left ? left : right).Image;

        // How far that fighter has faded in, 0 to 1.
        public float FighterAlpha(Side side) => (side == Side.Left ? left : right).Alpha;
        public float LetterboxAmount => letterbox;
        public string CountdownText => IsVisible(countdown) ? countdown.text : "";

        // obj_gameclock's "Get Ready", drawn above the number for the whole countdown (UQD-T37); empty otherwise.
        public string GetReadyText => IsVisible(getReady) ? getReady.text : "";

        // The alpha the countdown's number is drawn at: 1 as each of 3, 2, 1 appears, fading 0.02 a 60 fps step.
        public float CountdownAlpha => IsVisible(countdown) ? countdown.style.opacity.value : 0f;

        // gameclock_action's counting-up number in the centre while the cue is up (UQD-T38): milliseconds since the cue,
        // frozen at the first strike; empty when none is drawn.
        public string CounterText => IsVisible(counter) ? counter.text : "";

        // The alpha it is drawn at: 1 while it counts, fading 0.003 a 60 fps step from the first strike.
        public float CounterAlpha => IsVisible(counter) ? counter.style.opacity.value : 0f;

        // How far the counter has slid from the centre, in stage pixels (x right): the winner's number moving away from
        // the loser one original pixel a 60 fps step through the aftermath (UQD-T39).
        public float CounterOffset => IsVisible(counter) ? counter.style.translate.value.x.value : 0f;

        // The aftermath's banner on the loser's side (UQD-T39): the winner's name over "Wins!"; empty when none is drawn.
        public string WinnerNameText => IsVisible(winnerName) ? winnerName.text : "";
        public string WinsText => IsVisible(wins) ? wins.text : "";

        // The side that banner stands on: the loser's; null when none is drawn.
        public Side? WinsSide { get; private set; }
        public string HeadlineText => IsVisible(headline) ? headline.text : "";
        public string PromptText => IsVisible(prompt) ? prompt.text : "";

        // What that fighter is doing, and where it stands as a fraction of the stage width.
        public FighterMotion Motion(Side side) => (side == Side.Left ? left : right).Motion;

        // The sprite that fighter shows (idle, run, or a frame of its attack or defeat strip); null without fighters.
        public Sprite FighterShows(Side side) => (side == Side.Left ? left : right).Sprite;

        // Other components (DuelSession, DuelTaunt, DuelBlood) hang their own labels off this same tree.
        public VisualElement Root { get; private set; }

        // The fixed 1440 x 1080 stage (the room) the fighters and centred labels stand on (DuelBlood draws its drops on it).
        public VisualElement Stage => stage;

        // The top letterbox bar (obj_letterbox, depth -10000): whatever must draw under it and over the rest goes before it.
        public VisualElement LetterboxTop => letterboxTop;

        // obj_mouse (depth -5000) draws under the letterbox and the loading fade (-10000) and over the rest (UQD-T46).
        // Over the verdict screen (UQD-T48), which covers everything, the katana is drawn over everything.
        public VisualElement CursorCeiling => verdictScreen != null && verdictScreen.Shown ? null : letterboxTop;

        private DuelVerdict verdictScreen;

        private void Awake()
        {
            controller = GetComponent<DuelController>();
            // obj_game's F1: fullscreen (UQD-T50).
            GameWindow.On(gameObject);
            Build();
            controller.RoundStarted += OnRoundStarted;
            controller.Resolved += OnResolved;
            Refresh(0f);
        }

        private void Start()
        {
            fade = GetComponent<DuelFade>();
            if (fade != null) fade.BattleReset += OnBattleReset;
            verdictScreen = GetComponent<DuelVerdict>();
        }

        private void OnDestroy()
        {
            if (fade != null) fade.BattleReset -= OnBattleReset;
        }

        private void Update() => Refresh(Time.unscaledDeltaTime);

        // obj_fade_gamescreen's reset and gameclock_startup's first pass: both fighters back to idle on their marks, the
        // reaction times cleared and the aftermath's text gone (alpha = 0).
        private void OnBattleReset()
        {
            ResetBattle();
            battleReset = true;
        }

        private void OnRoundStarted()
        {
            ResetBattle();
            battleReset = false;
        }

        private void ResetBattle()
        {
            left.Motion.Idle();
            right.Motion.Idle();
            left.Draw();
            right.Draw();
        }

        // Whether the round just judged decided the match: the stand-in for the original's HP reaching 0 (UQD-T64). The
        // match has recorded the round by the time it is announced, locally and online.
        public bool IsKillingBlow => controller.Match != null && controller.Match.IsOver;

        private void OnResolved(DuelResult result)
        {
            // determine_winner: the winner attacks, drawn in front (depth -1), and the loser is defeated; a draw moves no one.
            // The loser's defeat strip plays through only on the killing blow, the round that decides the match.
            if (result.Winner.HasValue)
            {
                var winner = result.Winner.Value == Side.Left ? left : right;
                var loser = winner == left ? right : left;
                winner.Motion.Attack();
                loser.Motion.Defeat(IsKillingBlow);
                winner.Root.BringToFront();
                winner.Draw();
                loser.Draw();
            }
        }

        public static string FormatReaction(double? seconds)
        {
            if (!seconds.HasValue) return "no strike";
            if (seconds.Value < 0) return "too early";
            return $"{Mathf.RoundToInt((float)(seconds.Value * 1000))} ms";
        }

        // Who the right fighter is called in the headline; the session sets "AI" in vs-AI modes.
        public string RightName { get; set; } = "Right";

        public static string Headline(DuelResult result, string rightName = "Right")
        {
            switch (result.Outcome)
            {
                case DuelOutcome.Tie: return "Draw";
                case DuelOutcome.NoStrike: return "Nobody struck";
                default: return result.Winner == Side.Left ? "Left wins" : rightName + " wins";
            }
        }

        private void Refresh(float dt)
        {
            var phase = controller.Phase;
            ShowArena(controller.ArenaRoll);
            ShowFighters(controller.FighterRoll);

            // gameclock_countdown (UQD-T37): Get Ready at full alpha throughout, and each of 3, 2, 1 at full alpha as it
            // appears, fading at 0.02 a 60 fps step.
            var countingDown = phase == DuelPhase.Countdown;
            SetVisible(getReady, countingDown);
            SetVisible(countdown, countingDown);
            if (countingDown)
            {
                var number = controller.CountdownNumber;
                countdown.text = number > 0 ? number.ToString() : "";
                countdown.style.opacity = (float)controller.CountdownNumberAlpha;
            }

            var tense = phase == DuelPhase.Standoff || phase == DuelPhase.Strike;
            letterbox = Mathf.MoveTowards(letterbox, tense ? 1f : 0f, dt * 3f);
            if (dt == 0f) letterbox = tense ? 1f : 0f;
            letterboxTop.style.height = LetterboxHeight * letterbox;
            letterboxBottom.style.height = LetterboxHeight * letterbox;

            SetInverted(phase == DuelPhase.Strike);

            // gameclock_action (UQD-T38): ceil(count) at 1 ms a tick from the cue, read off the clock; the first strike
            // freezes it, and it fades from there at 0.003 a 60 fps step.
            var count = controller.ReactionCount;
            var counterAlpha = (float)controller.ReactionCountAlpha;
            SetVisible(counter, count > 0 && counterAlpha > 0f && !battleReset);
            if (count > 0)
            {
                counter.text = count.ToString();
                counter.style.opacity = counterAlpha;
            }
            // gameclock_aftermath (UQD-T39): a round won by reaction slides the winner's number away from the loser.
            counter.style.translate = new Translate((float)controller.ReactionCountSlide * PixelScale, 0f);

            var resolved = phase == DuelPhase.Resolved;
            var aftermath = resolved && !battleReset;
            var verdict = aftermath ? controller.Result : null;
            ShowWinner(verdict);
            // The original's own aftermath text says who won, or that someone struck early (DuelTaunt); the headline is
            // kept only for the rebuild's outcomes the original never drew (a tie, nobody striking, a forfeit) and while
            // an online verdict is on its way.
            var headlined = aftermath && !VerdictBanner.ShowsWinner(verdict) && !VerdictBanner.ShowsPremature(verdict);
            SetVisible(headline, headlined);
            if (headlined) headline.text = verdict != null ? Headline(verdict, RightName) : AwaitingVerdictText;
            // Online only (it has no original): the next round is offered once the loading fade has reset the battle
            // (UQD-T42), not over the aftermath. A local round's startup runs by itself and says nothing (UQD-T66).
            SetVisible(prompt, controller.IsOnline && (phase == DuelPhase.Ready || (resolved && !(fade != null && fade.Pending))));
            if (controller.IsOnline) prompt.text = OnlinePrompt(phase);

            // gameclock_action's first pass: the cue sends both idle fighters to their run marks, and they creep while it
            // is up.
            var cueUp = phase == DuelPhase.Strike;
            if (cueUp)
            {
                if (left.Motion.Pose == FighterPose.Idle) left.Motion.Run();
                if (right.Motion.Pose == FighterPose.Idle) right.Motion.Run();
            }
            left.Animate(dt, cueUp);
            right.Animate(dt, cueUp);
        }

        // obj_gameclock's aftermath (UQD-T39): the winner's name over "Wins!" at full alpha above the loser's idle mark.
        private void ShowWinner(DuelResult verdict)
        {
            var show = VerdictBanner.ShowsWinner(verdict);
            SetVisible(winnerName, show);
            SetVisible(wins, show);
            if (!show)
            {
                WinsSide = null;
                return;
            }
            var winner = verdict.Winner.Value;
            var side = VerdictBanner.BannerSide(winner);
            if (WinsSide != side)
            {
                WinsSide = side;
                Reposition(winnerName, StageX(side), WinnerNameY);
                Reposition(wins, StageX(side), BannerY);
            }
            winnerName.text = WinnerName(winner);
        }

        // The winner's character_name: the fighter in its seat, or its seat's name in a scene that carries none.
        private string WinnerName(Side winner)
        {
            var look = FighterIn(winner);
            if (look != null && !string.IsNullOrEmpty(look.Name)) return look.Name;
            return winner == Side.Left ? "Left" : RightName;
        }

        // The headline while this side's round is over but the host's verdict is still on its way (online only).
        public const string AwaitingVerdictText = "...";

        // Online, one seat is this machine's and only the host starts rounds, so the prompt says whose move it is.
        private string OnlinePrompt(DuelPhase phase)
        {
            var side = controller.LocalSide;
            var seat = side.HasValue ? $"You are {(side == Side.Left ? "Left" : "Right")}" : "Watching";
            const string strike = "Strike: Space / Ctrl / gamepad / click";
            string next;
            if (controller.AwaitingVerdict)
                next = "Waiting for the verdict";
            else if (controller.CanBeginOnlineRound)
                next = phase == DuelPhase.Ready ? "Press Space or Start to duel" : "Press Space or Start for the next round";
            else if (controller.Online.IsHost && !controller.Online.Seated)
                next = "Waiting for a player to join";
            else
                next = "Waiting for the host to start the round";
            return $"{seat}     {strike}\n{next}\nEsc / Select+Start: quit";
        }

        private static bool IsVisible(VisualElement element) => element.style.display != DisplayStyle.None;

        private static void SetVisible(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        // The arena the roll lands on (ArenaDraw.Pick), redrawn only when the roll changes.
        private void ShowArena(int roll)
        {
            if (shownRoll == roll) return;
            shownRoll = roll;
            Arena = arenas.Count > 0 ? arenas[ArenaDraw.Pick(roll, arenas.Count)] : null;
            backdrop.style.backgroundImage = Arena?.Sprite != null
                ? new StyleBackground(Arena.Sprite)
                : new StyleBackground(StyleKeyword.None);
        }

        // The pair the match's fighter roll lands on (FighterDraw.Pick), put on the field only when the roll changes (a
        // new match), each fading in from nothing, tinted red and blue when both seats hold the same fighter.
        private void ShowFighters(int roll)
        {
            if (shownFighterRoll == roll) return;
            shownFighterRoll = roll;
            if (fighters.Count == 0)
            {
                left.Show(null, null);
                right.Show(null, null);
                return;
            }
            var same = FighterDraw.SameFighter(roll, fighters.Count);
            left.Show(fighters[FighterDraw.Pick(roll, Side.Left, fighters.Count)], FighterLineup.Tint(Side.Left, same));
            right.Show(fighters[FighterDraw.Pick(roll, Side.Right, fighters.Count)], FighterLineup.Tint(Side.Right, same));
        }

        // A real colour invert of the whole backdrop (its art and the colour behind it), as obj_arena's
        // inverting blend over the view: every pixel becomes 1 - itself.
        private void SetInverted(bool value)
        {
            if (inverted == value) return;
            inverted = value;
            if (value)
            {
                var invert = new FilterFunction(FilterFunctionType.Invert);
                invert.AddParameter(new FilterParameter(1f));
                backdrop.style.filter = new List<FilterFunction> { invert };
            }
            else
            {
                backdrop.style.filter = StyleKeyword.Null;
            }
        }

        private void Build()
        {
            var documentObject = new GameObject("DuelDocument", typeof(UIDocument));
            documentObject.transform.SetParent(transform, false);
            document = documentObject.GetComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/DuelPanelSettings");

            Root = document.rootVisualElement;
            Root.AddToClassList("duel-root");
            OriginalFrame.Fit(Root);
            // Anything not drawn as OutlinedText (the online menu's controls) inherits the original's basic face.
            var basic = OriginalFonts.Load(OriginalType.Basic);
            if (basic != null) Root.style.unityFontDefinition = FontDefinition.FromSDFFont(basic);
            var styleSheet = Resources.Load<StyleSheet>("UI/Duel");
            if (styleSheet != null) Root.styleSheets.Add(styleSheet);

            // Root is the room's 1440 x 1080 frame (OriginalFrame); the "stage" is the same frame as a fixed-size
            // coordinate space for centre-anchored content (the fighters, every CreateLabel). Things that reach the
            // room's edges (the backdrop, letterbox, the edge score labels) stay on Root itself, sized and positioned
            // by percentage. The backdrop goes first so everything else draws over it.
            backdrop = Box("Backdrop", Root, Paper);
            backdrop.AddToClassList("backdrop");
            backdrop.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            backdrop.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            backdrop.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);

            stage = new VisualElement { name = "Stage", pickingMode = PickingMode.Ignore };
            stage.AddToClassList("stage");
            Root.Add(stage);

            left = new Fighter(this, Side.Left);
            right = new Fighter(this, Side.Right);

            // The gamescreen drew no per-side reaction lines: obj_gameclock drew only the winner's sliding number, and both
            // sides' reaction_time showed on F12's debug overlay alone (DuelDebugOverlay) (UQD-T73).

            letterboxTop = Box("LetterboxTop", Root, Color.black);
            letterboxTop.AddToClassList("letterbox");
            letterboxTop.style.top = 0;
            letterboxTop.style.height = 0;
            letterboxBottom = Box("LetterboxBottom", Root, Color.black);
            letterboxBottom.AddToClassList("letterbox");
            letterboxBottom.style.bottom = 0;
            letterboxBottom.style.height = 0;

            // The original's type (UQD-T36): banners and the countdown in Old English, the online prompt in the basic
            // face the original's buttons and HUD used. The gamescreen drew no title and no prompt: a local round's
            // startup runs by itself (UQD-T66).
            countdown = CreateLabel("Countdown", OriginalType.Countdown, new Vector2(0, CountdownY));
            // obj_gameclock draws Get Ready 44 original pixels above the number (room_height/2 - 60 against - 16).
            getReady = CreateLabel("GetReady", OriginalType.Countdown, new Vector2(0, CountdownY + CountdownBanner.BannerAboveNumber * PixelScale));
            getReady.text = CountdownBanner.Text;
            getReady.style.opacity = 1f; // draw_text_outline_alpha(..., default_alpha), and default_alpha is 1
            // obj_gameclock draws the action's count where the countdown's number was (room_height/2 - 16), in the same
            // Old English outlined 1.
            counter = CreateLabel("Counter", OriginalType.Countdown, new Vector2(0, CountdownY));
            // The cue is the backdrop's invert and the unsheath alone, as the original's was: no mark is drawn (UQD-T73).
            headline = CreateLabel("Headline", OriginalType.Banner, new Vector2(0, 370f));
            // The aftermath's winner banner (UQD-T39), moved to the loser's side when shown, drawn at default_alpha.
            winnerName = CreateLabel("WinnerName", OriginalType.Name, new Vector2(0, WinnerNameY));
            wins = CreateLabel("Wins", OriginalType.Banner, new Vector2(0, BannerY));
            wins.text = VerdictBanner.Wins;
            SetVisible(winnerName, false);
            SetVisible(wins, false);
            prompt = CreateLabel("Prompt", OriginalType.Hud, new Vector2(0, -440f));
        }

        // A box filled with its own colour (an untextured placeholder shape); position and size are set by
        // the caller, same as the original RectTransform-based helper.
        internal VisualElement Box(string name, VisualElement parent, Color color)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.style.position = Position.Absolute;
            element.style.backgroundColor = color;
            parent.Add(element);
            return element;
        }

        // Outlined text (UQD-T36) in `style`'s original font and outline, centred on `position` (the stage-centre-relative
        // position, y up); 1600 wide so long text never clips. White unless `fill` says otherwise, as draw_text_outline;
        // `scale` multiplies the original's pixel size.
        public OutlinedText CreateLabel(string name, TypeStyle style, Vector2 position, Color? fill = null, float scale = 1f)
        {
            var label = Outlined(name, style, fill, scale);
            PlaceOnStage(label, position.x, position.y, 1600f, label.LineHeight * 1.2f);
            stage.Add(label);
            return label;
        }

        private static OutlinedText Outlined(string name, TypeStyle style, Color? fill, float scale)
        {
            var label = new OutlinedText(style, scale) { name = name };
            label.AddToClassList("label");
            if (fill.HasValue) label.Fill = fill.Value;
            return label;
        }

        // Pinned to the left (edge 0) or right (edge 1) screen edge so it stays visible at any aspect,
        // the way the original anchored EdgeLabel to that edge (of the real canvas rect, not the fixed
        // stage) instead of the canvas centre; `top: 50%` finds Root's real vertical centre at any aspect.
        public OutlinedText CreateEdgeLabel(string name, TypeStyle style, float edge, float y, Color? fill = null)
        {
            var label = Outlined(name, style, fill, 1f);
            label.AddToClassList(edge < 0.5f ? "label--edge-left" : "label--edge-right");
            var height = label.LineHeight * 1.2f;
            label.style.position = Position.Absolute;
            label.style.width = 600f;
            label.style.height = height;
            label.style.top = Length.Percent(50);
            label.style.marginTop = -(y + height / 2f);
            if (edge < 0.5f) label.style.left = 40f; else label.style.right = 40f;
            Root.Add(label);
            return label;
        }

        // Moves an already-placed (CreateLabel/CreateEdgeLabel) stage element to a new centre -- the same
        // stage-centred, y-up coordinates PlaceOnStage took -- keeping the width/height it already has.
        // DuelTaunt uses this to move its label to whichever side was tauntable, like the original moving
        // the Text's own RectTransform.anchoredPosition.
        public void Reposition(VisualElement element, float x, float y)
        {
            var width = element.resolvedStyle.width > 0f ? element.resolvedStyle.width : element.style.width.value.value;
            var height = element.resolvedStyle.height > 0f ? element.resolvedStyle.height : element.style.height.value.value;
            PlaceOnStage(element, x, y, width, height);
        }

        // Places an element on the 1440 x 1080 stage, `position` measured from the stage's centre with y up
        // (as the original's canvas-centred anchoredPosition was), pivoted at its own centre.
        private static void PlaceOnStage(VisualElement element, float x, float y, float width, float height)
        {
            element.style.position = Position.Absolute;
            element.style.width = width;
            element.style.height = height;
            element.style.left = StageWidth / 2f + x - width / 2f;
            element.style.top = StageHeight / 2f - y - height / 2f;
        }

        // Places an element relative to its own parent's origin (not the stage centre), y up, pivoted at
        // (pivotX, pivotY) -- 0 is that edge of the element, 1 the opposite edge; (0.5, 0.5) is centred.
        // Mirrors RectTransform's anchoredPosition + pivot for a parent anchored at its own (0, 0).
        private static void PlacePivoted(VisualElement element, float x, float y, float width, float height, float pivotX, float pivotY)
        {
            element.style.position = Position.Absolute;
            element.style.width = width;
            element.style.height = height;
            element.style.left = x - pivotX * width;
            element.style.top = -y - (1f - pivotY) * height;
        }

        // The x, from the stage centre, that `side`'s fighter stands at: a quarter or three quarters of the width.
        private static float StageX(Side side) => ((float)FighterLineup.X(side) - 0.5f) * StageWidth;

        // One of the original fighters on the field, drawn from its FighterMotion (UQD-T35): the sprite the motion's pose
        // calls for (idle, run, or a frame of the attack or defeat strip) at the original's scale, centred where
        // obj_player stood (the sprites' origin is their centre), the right one mirrored to face left. The root sits
        // at the sprite's feet and moves along the stage with the motion's x.
        private sealed class Fighter
        {
            private readonly VisualElement root;
            private float shownFor;
            private Sprite shownSprite;
            private bool spriteStale = true;

            public Fighter(DuelView view, Side side)
            {
                Motion = new FighterMotion(side);
                var facing = FighterLineup.Facing(side);
                var size = OriginalSpriteSize * PixelScale;
                root = new VisualElement { name = side == Side.Left ? "Left" : "Right", pickingMode = PickingMode.Ignore };
                PlaceOnStage(root, StageX(side), FighterY - size / 2f, 0f, 0f);
                view.stage.Add(root);

                Image = new VisualElement { name = "Sprite", pickingMode = PickingMode.Ignore };
                PlacePivoted(Image, 0f, 0f, size, size, 0.5f, 0f);
                Image.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                Image.style.scale = new Scale(new Vector2(facing, 1f));
                root.Add(Image);

                Show(null, null);
            }

            // The original's fighter sprites are all 128 x 128 (Data/assets.json).
            private const float OriginalSpriteSize = 128f;

            public VisualElement Image { get; }
            public VisualElement Root => root;
            public FighterMotion Motion { get; }
            public FighterLook Look { get; private set; }
            public float Alpha => (float)FighterLineup.Alpha(shownFor);

            // The sprite on screen; null in a scene that carries no fighters.
            public Sprite Sprite => shownSprite;

            // Puts `look` on the field, faded out, with `tint` as its image_blend (null: none).
            public void Show(FighterLook look, Rgb? tint)
            {
                Look = look;
                shownFor = 0f;
                Image.style.unityBackgroundImageTintColor = tint.HasValue
                    ? (Color)new Color32(tint.Value.R, tint.Value.G, tint.Value.B, 255)
                    : Color.white;
                Image.style.opacity = Alpha;
                spriteStale = true;
                Draw();
            }

            // A hitch (a scene load, a stalled frame) advances the fade by at most this much, as the original's
            // frame-locked fade would not have jumped either; at any frame rate above 20 fps it is pure clock time.
            private const float MaxFadeStep = 0.05f;

            public void Animate(float dt, bool creeping)
            {
                shownFor += Mathf.Min(dt, MaxFadeStep);
                Image.style.opacity = Alpha;
                if (creeping) Motion.Creep(dt);
                Motion.Advance(dt);
                Draw();
            }

            // Places the root at the motion's x and shows the sprite its pose calls for, swapping the image only when
            // it changes.
            public void Draw()
            {
                root.style.left = StageWidth * (float)Motion.XFraction;
                var sprite = SpriteFor(Look, Motion);
                if (sprite == shownSprite && !spriteStale) return;
                shownSprite = sprite;
                spriteStale = false;
                Image.style.backgroundImage = sprite != null
                    ? new StyleBackground(sprite)
                    : new StyleBackground(StyleKeyword.None);
            }

            private static Sprite SpriteFor(FighterLook look, FighterMotion motion)
            {
                if (look == null) return null;
                switch (motion.Sprite)
                {
                    case FighterSprite.Run: return look.Run;
                    case FighterSprite.Attack: return Frame(look.Attack, motion.Frame) ?? look.Idle;
                    case FighterSprite.Defeat: return Frame(look.Defeat, motion.Frame) ?? look.Idle;
                    default: return look.Idle;
                }
            }

            private static Sprite Frame(IReadOnlyList<Sprite> strip, int frame) =>
                strip.Count == 0 ? null : strip[Mathf.Clamp(frame, 0, strip.Count - 1)];
        }
    }
}
