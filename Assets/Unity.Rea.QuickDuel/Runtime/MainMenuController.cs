using System;
using System.Collections.Generic;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // The main menu (UQD-T45), as the original's mainmenu room drew it (MainMenu): bg_splatter_scroll scrolling left
    // behind the title's cut-out logo, New Game, Versus, the computer's difficulty, Quit and Credits on the original's
    // button art, each flicking (sfx_flick) and showing its "_over" sprite and its message while the mouse is over it and
    // clicking (sfx_click) when pressed. New Game and Versus fade a loading image in (MenuFade) and enter the duel under it
    // with the mode chosen (DuelModeHandoff); Quit and Escape end the game; Credits rolls the author's credit and the
    // arena photo credits of the original's credits.txt (CreditsRoll, UQD-T47) up the room until a click, Enter, Space,
    // a pad's South or Start, or Escape closes them. The arrow keys or a pad's d-pad walk the buttons, and Enter, Space
    // or the pad's South or Start press the highlighted one, so the menu can be played without a mouse.
    //
    // Everything steps at the original's 60 a second counted off Clock, from the moment the scene starts. The room is
    // drawn at the original's pixel scale on the 640 x 480 frame letterboxed into the window (OriginalFrame, UQD-T50),
    // black around it; the loading image covers the room. The whole tree is built here in code.
    public sealed class MainMenuController : MonoBehaviour, IKatanaCursorHost
    {
        // The scene New Game and Versus lead to. Written by MainMenuSceneBuilder.
        [SerializeField] private string nextScene = "Duel";

        // bg_splatter_scroll, spr_titlescreen, spr_loading1 to 14 in order, and each button's sprite and "_over" sprite,
        // in MainMenu's button order. Written by MainMenuSceneBuilder.
        [SerializeField] private Sprite background;
        [SerializeField] private Sprite title;
        [SerializeField] private List<Sprite> loadingImages = new List<Sprite>();
        [SerializeField] private List<Sprite> buttonSprites = new List<Sprite>();
        [SerializeField] private List<Sprite> buttonOverSprites = new List<Sprite>();

        // sfx_flick and sfx_click.
        [SerializeField] private AudioClip flick;
        [SerializeField] private AudioClip click;

        // The original's datafiles/credits.txt, read when the room starts, and the game's author (the manifest's
        // text.author). Written by MainMenuSceneBuilder.
        [SerializeField] private TextAsset creditsText;
        [SerializeField] private string author = MainMenu.DefaultAuthor;

        public const string FlickCue = "flick";
        public const string ClickCue = "click";

        private const float PixelScale = OutlinedText.PixelScale;

        private readonly System.Random random = new System.Random();
        private UIDocument document;
        private VisualElement room;
        private readonly List<VisualElement> scrollTiles = new List<VisualElement>();
        private VisualElement titleElement;
        private readonly List<VisualElement> buttonElements = new List<VisualElement>();
        private readonly List<OutlinedText> labels = new List<OutlinedText>();
        private OutlinedText message;
        private VisualElement credits;
        private readonly List<OutlinedText> creditLines = new List<OutlinedText>();
        private bool? shownCredits;
        private VisualElement overlay;
        private AudioSource sounds;
        private InputAction press;
        private InputAction next;
        private InputAction previous;
        private InputAction activate;
        private InputAction cancel;
        private Vector2? lastPointer;
        private int? shownImage;
        private bool?[] shownOver;
        private bool left;

        public void SetAssets(string nextSceneName, Sprite splatterScroll, Sprite titleScreen, IEnumerable<Sprite> loading,
            IEnumerable<Sprite> buttons, IEnumerable<Sprite> buttonsOver, AudioClip flickClip, AudioClip clickClip)
        {
            nextScene = nextSceneName ?? throw new ArgumentNullException(nameof(nextSceneName));
            background = splatterScroll;
            title = titleScreen;
            loadingImages = new List<Sprite>(loading ?? throw new ArgumentNullException(nameof(loading)));
            buttonSprites = new List<Sprite>(buttons ?? throw new ArgumentNullException(nameof(buttons)));
            buttonOverSprites = new List<Sprite>(buttonsOver ?? throw new ArgumentNullException(nameof(buttonsOver)));
            flick = flickClip;
            click = clickClip;
        }

        public void SetCredits(TextAsset text, string gameAuthor)
        {
            creditsText = text;
            author = string.IsNullOrWhiteSpace(gameAuthor) ? throw new ArgumentException("The credits need an author.", nameof(gameAuthor)) : gameAuthor;
        }

        public string NextScene => nextScene;
        public TextAsset CreditsText => creditsText;
        public string Author => author;
        public Sprite Background => background;
        public Sprite Title => title;
        public IReadOnlyList<Sprite> LoadingImages => loadingImages;
        public IReadOnlyList<Sprite> ButtonSprites => buttonSprites;
        public IReadOnlyList<Sprite> ButtonOverSprites => buttonOverSprites;
        public AudioClip Flick => flick;
        public AudioClip Click => click;

        // Seconds on the realtime timeline; tests hold it still and step it by hand.
        public Func<double> Clock { get; set; } = () => Time.realtimeSinceStartupAsDouble;

        // Where the pointer is, in room pixels, or null when there is none; read every frame and handed to the menu
        // when it moves. Tests replace it.
        public Func<Vector2?> Pointer { get; set; }

        // What happens once the fade in is full: the next scene is loaded. Tests may replace it.
        public Action<string> Leave { get; set; } = name => SceneManager.LoadScene(name);

        // game_end(). Tests replace it so quitting does not tear down the test process.
        public Action OnQuit { get; set; } = Application.Quit;

        // The clock reading the room's steps are counted from.
        public double StartedAt { get; private set; }

        // The room's state.
        public MainMenu Menu { get; private set; }

        // The menu is over and the duel has been asked for.
        public bool HasLeft => left;

        // Every sound played, in order, for tests and debugging.
        public List<string> Played { get; } = new List<string>();

        // The drawn tree, for tests and screenshots.
        public VisualElement Root { get; private set; }
        public VisualElement Room => room;
        public IReadOnlyList<VisualElement> ScrollTiles => scrollTiles;
        public VisualElement TitleElement => titleElement;
        public IReadOnlyList<VisualElement> ButtonElements => buttonElements;
        public IReadOnlyList<OutlinedText> Labels => labels;
        public OutlinedText MessageText => message;
        public VisualElement Credits => credits;
        public IReadOnlyList<OutlinedText> CreditLines => creditLines;
        public VisualElement Overlay => overlay;

        // obj_mouse (depth -5000) draws under obj_fade_mainmenu (-10000) and over the rest (UQD-T46).
        public VisualElement CursorCeiling => overlay;

        private void Awake()
        {
            // obj_mainmenu's F1: fullscreen (UQD-T50).
            GameWindow.On(gameObject);
            Pointer = ReadPointer;
            sounds = gameObject.AddComponent<AudioSource>();
            sounds.playOnAwake = false;

            // obj_btn_*'s left pressed: a click or a touch, at the pointer.
            press = new InputAction("MenuPress", InputActionType.Button, "<Pointer>/press");
            press.performed += _ =>
            {
                var at = Pointer?.Invoke();
                if (at.HasValue) Menu?.Press(at.Value.x, at.Value.y);
            };

            next = new InputAction("MenuNext", InputActionType.Button);
            next.AddBinding("<Keyboard>/downArrow");
            next.AddBinding("<Keyboard>/rightArrow");
            next.AddBinding("<Gamepad>/dpad/down");
            next.AddBinding("<Gamepad>/dpad/right");
            next.performed += _ => Menu?.FocusNext();

            previous = new InputAction("MenuPrevious", InputActionType.Button);
            previous.AddBinding("<Keyboard>/upArrow");
            previous.AddBinding("<Keyboard>/leftArrow");
            previous.AddBinding("<Gamepad>/dpad/up");
            previous.AddBinding("<Gamepad>/dpad/left");
            previous.performed += _ => Menu?.FocusPrevious();

            activate = new InputAction("MenuActivate", InputActionType.Button);
            activate.AddBinding("<Keyboard>/enter");
            activate.AddBinding("<Keyboard>/numpadEnter");
            activate.AddBinding("<Keyboard>/space");
            activate.AddBinding("<Gamepad>/buttonSouth");
            activate.AddBinding("<Gamepad>/start");
            activate.performed += _ => Menu?.Activate();

            // obj_mainmenu's Escape: game_end().
            cancel = new InputAction("MenuCancel", InputActionType.Button, "<Keyboard>/escape");
            cancel.performed += _ => Menu?.Cancel();
        }

        private void OnEnable()
        {
            press.Enable();
            next.Enable();
            previous.Enable();
            activate.Enable();
            cancel.Enable();
        }

        private void OnDisable()
        {
            press.Disable();
            next.Disable();
            previous.Disable();
            activate.Disable();
            cancel.Disable();
        }

        private void OnDestroy()
        {
            press.Dispose();
            next.Dispose();
            previous.Dispose();
            activate.Dispose();
            cancel.Dispose();
        }

        private void Start() => Begin();

        // Starts the room over from the clock's reading now: nothing under the pointer, and the image the intro left
        // (fade_asset; spr_loading1 when the menu is loaded on its own) full and fading out.
        public void Begin()
        {
            var image = LoadingHandoff.TryTake(out var handed) && handed >= 0 && handed < MenuFade.Images ? handed : 0;
            if (Menu != null)
            {
                Menu.Flicked -= OnFlicked;
                Menu.Clicked -= OnClicked;
                Menu.Quit -= OnMenuQuit;
            }
            Menu = new MainMenu(image, DuelModeHandoff.Difficulty, random, ReadCredits());
            Menu.Flicked += OnFlicked;
            Menu.Clicked += OnClicked;
            Menu.Quit += OnMenuQuit;
            if (Root == null) Build();
            StartedAt = Clock();
            left = false;
            shownImage = null;
            shownOver = null;
            shownCredits = null;
            lastPointer = null;
            Played.Clear();
            Advance();
            Draw();
        }

        private void Update()
        {
            var at = Pointer?.Invoke();
            if (at.HasValue && at != lastPointer)
            {
                lastPointer = at;
                Menu.PointAt(at.Value.x, at.Value.y);
            }
            Advance();
            Draw();
        }

        private void Advance()
        {
            Menu.RunTo(LoadingFade.StepsAt(Clock() - StartedAt, MainMenu.TicksPerSecond));
            DuelModeHandoff.Difficulty = Menu.Difficulty;
            if (Menu.Finished && !left)
            {
                left = true;
                DuelModeHandoff.Give(Menu.Mode);
                LoadingHandoff.Give(Menu.Fade.Image);
                Leave?.Invoke(nextScene);
            }
        }

        // credits.txt laid out under the author's credit; a scene that carries no credits shows the author alone.
        private CreditsRoll ReadCredits() =>
            creditsText != null ? CreditsRoll.FromText(author, creditsText.text) : new CreditsRoll(author, Array.Empty<CreditsEntry>());

        private void OnFlicked(MenuButton button) => Play(FlickCue, flick);
        private void OnClicked(MenuButton button) => Play(ClickCue, click);
        private void OnMenuQuit() => OnQuit?.Invoke();

        private void Play(string cue, AudioClip clip)
        {
            Played.Add(cue);
            if (clip != null && sounds != null) sounds.PlayOneShot(clip);
        }

        // The pointer's place in the room, from its screen position through the panel the room is drawn on.
        private Vector2? ReadPointer()
        {
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null || room?.panel == null) return null;
            var screen = pointer.position.ReadValue();
            var panel = RuntimePanelUtils.ScreenToPanel(room.panel, new Vector2(screen.x, Screen.height - screen.y));
            return room.WorldToLocal(panel) / PixelScale;
        }

        private void Build()
        {
            var documentObject = new GameObject("MainMenuDocument", typeof(UIDocument));
            documentObject.transform.SetParent(transform, false);
            document = documentObject.GetComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/DuelPanelSettings");

            Root = document.rootVisualElement;
            Root.name = "MainMenu";
            Root.pickingMode = PickingMode.Ignore;
            Root.style.backgroundColor = Color.black;
            // The room is the frame, letterboxed into the window (UQD-T50).
            OriginalFrame.Fit(Root);

            // The 640 x 480 room at the original's scale, filling the frame; the scrolling background stays inside it.
            room = new VisualElement { name = "Room", pickingMode = PickingMode.Ignore };
            room.style.position = Position.Absolute;
            room.style.width = MainMenu.RoomWidth * PixelScale;
            room.style.height = MainMenu.RoomHeight * PixelScale;
            room.style.left = Length.Percent(50);
            room.style.top = Length.Percent(50);
            room.style.marginLeft = -MainMenu.RoomWidth * PixelScale / 2f;
            room.style.marginTop = -MainMenu.RoomHeight * PixelScale / 2f;
            room.style.overflow = Overflow.Hidden;
            Root.Add(room);

            // bg_splatter_scroll, tiled: two copies side by side cover the room wherever the scroll has got to.
            for (var i = 0; i < 2; i++)
            {
                var tile = new VisualElement { name = $"Scroll{i + 1}", pickingMode = PickingMode.Ignore };
                tile.style.position = Position.Absolute;
                tile.style.top = 0;
                tile.style.width = MainMenu.ScrollWidth * PixelScale;
                tile.style.height = MainMenu.ScrollHeight * PixelScale;
                tile.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                tile.style.backgroundImage = background != null ? new StyleBackground(background) : new StyleBackground(StyleKeyword.None);
                room.Add(tile);
                scrollTiles.Add(tile);
            }

            // obj_title (depth 100): spr_titlescreen over the room.
            titleElement = new VisualElement { name = "Title", pickingMode = PickingMode.Ignore };
            titleElement.style.position = Position.Absolute;
            titleElement.style.left = titleElement.style.top = 0;
            titleElement.style.width = MainMenu.RoomWidth * PixelScale;
            titleElement.style.height = MainMenu.RoomHeight * PixelScale;
            titleElement.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            titleElement.style.backgroundImage = title != null ? new StyleBackground(title) : new StyleBackground(StyleKeyword.None);
            room.Add(titleElement);

            // The buttons (depth 0), each its sprite with its label over it.
            for (var i = 0; i < Menu.Buttons.Count; i++)
            {
                var button = Menu.Buttons[i];
                var element = new VisualElement { name = button.Kind.ToString(), pickingMode = PickingMode.Ignore };
                element.style.position = Position.Absolute;
                element.style.left = (button.X - MenuButton.OriginX) * PixelScale;
                element.style.top = (button.Y - MenuButton.OriginY) * PixelScale;
                element.style.width = MenuButton.Width * PixelScale;
                element.style.height = MenuButton.Height * PixelScale;
                element.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                room.Add(element);
                buttonElements.Add(element);

                var label = CenteredText(button.Kind + "Label", OriginalType.Button, button.LabelX, button.LabelY);
                room.Add(label);
                labels.Add(label);
            }

            // mouseover_message_draw.
            message = CenteredText("Message", OriginalType.Message, MainMenu.MessageX, MainMenu.MessageY);
            room.Add(message);

            // The credits (UQD-T47): a black shade over the room and the roll's lines on it, over the buttons.
            credits = new VisualElement { name = "Credits", pickingMode = PickingMode.Ignore };
            credits.style.position = Position.Absolute;
            credits.style.left = credits.style.top = 0;
            credits.style.width = MainMenu.RoomWidth * PixelScale;
            credits.style.height = MainMenu.RoomHeight * PixelScale;
            credits.style.backgroundColor = new Color(0f, 0f, 0f, (float)CreditsRoll.Shade);
            credits.style.display = DisplayStyle.None;
            room.Add(credits);
            foreach (var line in Menu.Credits.Lines)
            {
                var text = CenteredText("Credit" + line.Kind, line.Style, MainMenu.RoomWidth / 2, 0);
                text.text = line.Text;
                credits.Add(text);
                creditLines.Add(text);
            }

            // obj_fade_mainmenu (Draw GUI, depth -10000): the loading image over everything, stretched over the room.
            overlay = new VisualElement { name = "LoadingFade", pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Root.Add(overlay);
        }

        // Text centred on room x with its top at room y (fa_center, the default fa_top), free to run past the room's
        // width either way.
        private static OutlinedText CenteredText(string name, TypeStyle style, int x, int y)
        {
            const float width = MainMenu.RoomWidth;
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

        private void Draw()
        {
            var scroll = Menu.ScrollX;
            for (var i = 0; i < scrollTiles.Count; i++)
                scrollTiles[i].style.left = (scroll + i * MainMenu.ScrollWidth) * PixelScale;

            if (shownOver == null) shownOver = new bool?[Menu.Buttons.Count];
            for (var i = 0; i < Menu.Buttons.Count; i++)
            {
                var button = Menu.Buttons[i];
                var over = Menu.Hovered == button;
                if (shownOver[i] != over)
                {
                    shownOver[i] = over;
                    var sprites = over ? buttonOverSprites : buttonSprites;
                    var sprite = i < sprites.Count ? sprites[i] : null;
                    buttonElements[i].style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
                }
                if (labels[i].text != button.Label) labels[i].text = button.Label;
            }
            if (message.text != Menu.Message) message.text = Menu.Message;

            var open = Menu.CreditsOpen;
            if (shownCredits != open)
            {
                shownCredits = open;
                // The room's own drawing stands down under the credits; the blood scrolls on through the shade.
                var menuShown = open ? DisplayStyle.None : DisplayStyle.Flex;
                titleElement.style.display = menuShown;
                foreach (var element in buttonElements) element.style.display = menuShown;
                foreach (var label in labels) label.style.display = menuShown;
                message.style.display = menuShown;
                credits.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (open)
            {
                var lines = Menu.Credits.Lines;
                for (var i = 0; i < lines.Count && i < creditLines.Count; i++)
                    creditLines[i].style.top = Menu.Credits.LineY(lines[i]) * PixelScale;
            }

            var fade = Menu.Fade;
            if (shownImage != fade.Image)
            {
                shownImage = fade.Image;
                var sprite = fade.Image < loadingImages.Count ? loadingImages[fade.Image] : null;
                // A scene that carries no images fades a black screen instead.
                overlay.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
                overlay.style.backgroundColor = sprite != null ? Color.clear : Color.black;
            }
            var alpha = (float)fade.DrawnAlpha;
            overlay.style.opacity = alpha;
            overlay.style.display = alpha > 0f ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
