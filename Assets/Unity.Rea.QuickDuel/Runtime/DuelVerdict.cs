using System;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // The match verdict (UQD-T48), as the original's victory and gameover rooms (VerdictScreen): once a round has ended
    // the match and the loading fade's image is full over the reset battle, the duel cuts to bg_victory or bg_gameover,
    // as obj_fade_gamescreen's room_goto did, with "Victory" or "Game Over" in the Old English outlined 2, the match's
    // result and fastest reaction on obj_victory's lines, and Continue or Retry on spr_btn_right4 at (576, 448), its
    // "_over" sprite drawn while the pointer is on it. A click on it (or Enter, Space, a pad's South or Start) plays
    // sfx_click and leaves: Retry loads the duel again for a new match in the same mode, Continue goes back to the main
    // menu, each opening under the image this screen cut from (the original's fade_asset).
    //
    // Local play only: an online match has no verdict room, and its host replaces a finished match on the next round as
    // before. The room is drawn at the original's pixel scale on the duel's 640 x 480 frame (OriginalFrame, UQD-T50),
    // over everything else on it (the katana cursor stays on top).
    [RequireComponent(typeof(DuelController), typeof(DuelView))]
    public sealed class DuelVerdict : MonoBehaviour
    {
        // bg_victory, bg_gameover, spr_btn_right4 and its "_over" twin. Written by DuelSceneBuilder.
        [SerializeField] private Sprite victoryBackground;
        [SerializeField] private Sprite gameOverBackground;
        [SerializeField] private Sprite button;
        [SerializeField] private Sprite buttonOver;

        // sfx_click.
        [SerializeField] private AudioClip click;

        // Where Continue and Retry lead. Written by DuelSceneBuilder.
        [SerializeField] private string menuScene = "MainMenu";
        [SerializeField] private string duelScene = "Duel";

        public const string ClickCue = "click";

        private const float PixelScale = OutlinedText.PixelScale;

        private DuelController controller;
        private DuelView view;
        private DuelFade fade;
        private VisualElement overlay;
        private VisualElement room;
        private VisualElement background;
        private OutlinedText heading;
        private OutlinedText result;
        private OutlinedText fastest;
        private VisualElement buttonElement;
        private OutlinedText buttonLabel;
        private InputAction press;
        private InputAction activate;
        private Vector2? lastPointer;
        private bool? shownOver;
        private int image;
        private bool left;

        public void SetAssets(Sprite victory, Sprite gameOver, Sprite buttonSprite, Sprite buttonOverSprite, AudioClip clickClip,
            string menuSceneName, string duelSceneName)
        {
            victoryBackground = victory;
            gameOverBackground = gameOver;
            button = buttonSprite;
            buttonOver = buttonOverSprite;
            click = clickClip;
            menuScene = menuSceneName ?? throw new ArgumentNullException(nameof(menuSceneName));
            duelScene = duelSceneName ?? throw new ArgumentNullException(nameof(duelSceneName));
        }

        public Sprite VictoryBackground => victoryBackground;
        public Sprite GameOverBackground => gameOverBackground;
        public Sprite Button => button;
        public Sprite ButtonOver => buttonOver;
        public AudioClip Click => click;
        public string MenuScene => menuScene;
        public string DuelScene => duelScene;

        // The verdict on screen, or null while the match plays on.
        public VerdictScreen Screen { get; private set; }

        public bool Shown => Screen != null;

        // Where the pointer is, in room pixels, or null when there is none. Tests replace it.
        public Func<Vector2?> Pointer { get; set; }

        // What the button does once chosen: the scene is loaded. Tests replace it.
        public Action<string> Leave { get; set; } = name => SceneManager.LoadScene(name);

        // The button has been pressed and its scene asked for.
        public bool HasLeft => left;

        // Every sound played, in order, for tests and debugging.
        public System.Collections.Generic.List<string> Played { get; } = new System.Collections.Generic.List<string>();

        // The drawn tree, for tests and screenshots.
        public VisualElement Overlay => overlay;
        public VisualElement Background => background;
        public VisualElement ButtonElement => buttonElement;
        public string HeadingText => Shown ? heading.text : "";
        public string ResultText => Shown ? result.text : "";
        public string FastestText => Shown ? fastest.text : "";
        public string ButtonText => Shown ? buttonLabel.text : "";

        private void Awake()
        {
            controller = GetComponent<DuelController>();
            Pointer = ReadPointer;

            // obj_btn_retry / _nextbattle's left pressed: a click or a touch, at the pointer.
            press = new InputAction("VerdictPress", InputActionType.Button, "<Pointer>/press");
            press.performed += _ =>
            {
                var at = Pointer?.Invoke();
                if (Screen != null && at.HasValue) Screen.Press(at.Value.x, at.Value.y);
            };

            // Not the original's: the one button answers a key or a pad too.
            activate = new InputAction("VerdictActivate", InputActionType.Button);
            activate.AddBinding("<Keyboard>/enter");
            activate.AddBinding("<Keyboard>/numpadEnter");
            activate.AddBinding("<Keyboard>/space");
            activate.AddBinding("<Gamepad>/buttonSouth");
            activate.AddBinding("<Gamepad>/start");
            activate.performed += ctx =>
            {
                // Select held with Start is DuelQuit's chord.
                if (ctx.control.device is Gamepad pad && pad.selectButton.isPressed)
                    return;
                Screen?.Activate();
            };
        }

        private void Start()
        {
            view = GetComponent<DuelView>();
            fade = GetComponent<DuelFade>();
            if (fade != null) fade.BattleReset += OnBattleReset;
            controller.RoundStarted += Hide;
        }

        private void OnEnable()
        {
            press.Enable();
            activate.Enable();
        }

        private void OnDisable()
        {
            press.Disable();
            activate.Disable();
        }

        private void OnDestroy()
        {
            press.Dispose();
            activate.Dispose();
            if (fade != null) fade.BattleReset -= OnBattleReset;
            if (controller != null) controller.RoundStarted -= Hide;
        }

        // obj_fade_gamescreen's full image: a finished match goes to its verdict room instead of the next startup.
        private void OnBattleReset()
        {
            if (controller.IsOnline || !controller.Match.IsOver)
                return;
            image = fade.Image ?? 0;
            Show(VerdictScreen.For(controller.Match, controller.Session.AiPlaysRight, SeatName));
            fade.Cut();
        }

        private string SeatName(Side side) => side == Side.Left ? "Left" : controller.Session.AiPlaysRight ? "The AI" : "Right";

        // Puts `screen` up over everything.
        public void Show(VerdictScreen screen)
        {
            if (overlay == null) Build();
            if (Screen != null) Screen.Clicked -= OnClicked;
            Screen = screen ?? throw new ArgumentNullException(nameof(screen));
            Screen.Clicked += OnClicked;
            left = false;
            shownOver = null;
            lastPointer = null;

            var sprite = screen.Kind == VerdictKind.GameOver ? gameOverBackground : victoryBackground;
            background.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
            heading.text = screen.Heading;
            result.text = screen.Result;
            fastest.text = screen.Fastest;
            buttonLabel.text = screen.Button.Label;
            overlay.style.display = DisplayStyle.Flex;
            overlay.BringToFront();
            Draw();
        }

        private void Hide()
        {
            if (Screen != null) Screen.Clicked -= OnClicked;
            Screen = null;
            if (overlay != null) overlay.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            if (Screen == null) return;
            var at = Pointer?.Invoke();
            if (at.HasValue && at != lastPointer)
            {
                lastPointer = at;
                Screen.PointAt(at.Value.x, at.Value.y);
            }
            Draw();
            if (Screen.Choice != VerdictChoice.None && !left)
            {
                left = true;
                // room_goto: the next room opens under the image this one cut from (fade_asset).
                LoadingHandoff.Give(image);
                if (Screen.Choice == VerdictChoice.Retry)
                {
                    DuelModeHandoff.Give(controller.Session.Mode);
                    Leave?.Invoke(duelScene);
                }
                else
                {
                    Leave?.Invoke(menuScene);
                }
            }
        }

        // sfx_click, heard on through the room change.
        private void OnClicked(MenuButton pressed)
        {
            Played.Add(ClickCue);
            if (click == null) return;
            var player = new GameObject("VerdictClick", typeof(AudioSource));
            DontDestroyOnLoad(player);
            player.GetComponent<AudioSource>().PlayOneShot(click);
            Destroy(player, click.length + 0.1f);
        }

        private void Draw()
        {
            var over = Screen.Hovered;
            if (shownOver == over) return;
            shownOver = over;
            var sprite = over ? buttonOver : button;
            buttonElement.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
        }

        // The pointer's place in the room, from its screen position through the panel the room is drawn on.
        private Vector2? ReadPointer()
        {
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null || room?.panel == null) return null;
            var screen = pointer.position.ReadValue();
            var panel = RuntimePanelUtils.ScreenToPanel(room.panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
            return room.WorldToLocal(panel) / PixelScale;
        }

        private void Build()
        {
            if (view == null) view = GetComponent<DuelView>();
            overlay = new VisualElement { name = "Verdict", pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = Color.black;
            overlay.style.display = DisplayStyle.None;
            view.Root.Add(overlay);

            // The 640 x 480 room at the original's scale, centred.
            room = new VisualElement { name = "VerdictRoom", pickingMode = PickingMode.Ignore };
            room.style.position = Position.Absolute;
            room.style.width = VerdictScreen.RoomWidth * PixelScale;
            room.style.height = VerdictScreen.RoomHeight * PixelScale;
            room.style.left = Length.Percent(50);
            room.style.top = Length.Percent(50);
            room.style.marginLeft = -VerdictScreen.RoomWidth * PixelScale / 2f;
            room.style.marginTop = -VerdictScreen.RoomHeight * PixelScale / 2f;
            room.style.overflow = Overflow.Hidden;
            overlay.Add(room);

            // The room's background, 640 x 480 from its top left.
            background = new VisualElement { name = "VerdictBackground", pickingMode = PickingMode.Ignore };
            background.style.position = Position.Absolute;
            background.style.left = background.style.top = 0;
            background.style.width = VerdictScreen.RoomWidth * PixelScale;
            background.style.height = VerdictScreen.RoomHeight * PixelScale;
            background.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            room.Add(background);

            heading = CenteredText("VerdictHeading", OriginalType.Title, VerdictScreen.HeadingX, VerdictScreen.HeadingY);
            result = CenteredText("VerdictResult", OriginalType.Results, VerdictScreen.RoomWidth / 2, VerdictScreen.ResultY);
            fastest = CenteredText("VerdictFastest", OriginalType.Results, VerdictScreen.RoomWidth / 2, VerdictScreen.FastestY);
            room.Add(heading);
            room.Add(result);
            room.Add(fastest);

            // obj_btn_retry / _nextbattle: its sprite (origin 64, 32) on the instance, its label over it.
            var place = new VerdictScreen(VerdictKind.Victory, "", "").Button;
            buttonElement = new VisualElement { name = "VerdictButton", pickingMode = PickingMode.Ignore };
            buttonElement.style.position = Position.Absolute;
            buttonElement.style.left = (place.X - MenuButton.OriginX) * PixelScale;
            buttonElement.style.top = (place.Y - MenuButton.OriginY) * PixelScale;
            buttonElement.style.width = MenuButton.Width * PixelScale;
            buttonElement.style.height = MenuButton.Height * PixelScale;
            buttonElement.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            room.Add(buttonElement);
            buttonLabel = CenteredText("VerdictButtonLabel", OriginalType.Button, place.LabelX, place.LabelY);
            room.Add(buttonLabel);
        }

        // Text centred on room x with its top at room y, free to run past the room's width either way.
        private static OutlinedText CenteredText(string name, TypeStyle style, int x, int y)
        {
            const float width = VerdictScreen.RoomWidth;
            var text = new OutlinedText(style) { name = name };
            text.style.position = Position.Absolute;
            text.style.left = (x - width / 2f) * PixelScale;
            text.style.top = y * PixelScale;
            text.style.width = width * PixelScale;
            text.style.height = text.LineHeight;
            text.style.unityTextAlign = TextAnchor.UpperCenter;
            text.style.whiteSpace = WhiteSpace.NoWrap;
            text.style.overflow = Overflow.Visible;
            return text;
        }
    }
}
