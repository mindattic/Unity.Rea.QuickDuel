using System;
using System.Collections.Generic;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // The intro (UQD-T44), the game's first scene, as the original's intro room drew it (IntroScene): bg_mapscreen under
    // twelve clouds drifting left a pixel a step at half alpha and wrapping back in from the right, the story typed out
    // in fnt_mono a character every 4 steps from 3 s in, held 5 s once it is all up, then one of the loading images
    // faded in over everything and the next scene entered under it (LoadingHandoff, the original's fade_asset). The room
    // opens under spr_loading1 fading away. A click, any key or a pad's South or Start starts the fade to the next scene
    // at once.
    //
    // Everything steps at the original's 60 a second counted off Clock, not rendered frames, from the moment the scene
    // starts. The room is drawn at the original's pixel scale on the 640 x 480 frame letterboxed into the window
    // (OriginalFrame, UQD-T50), black around it, as the duel is. The whole tree is built here in code.
    public sealed class IntroController : MonoBehaviour
    {
        // The scene the intro leads to: the original's mainmenu (UQD-T45). Written by IntroSceneBuilder.
        [SerializeField] private string nextScene = "MainMenu";

        // bg_mapscreen, spr_cloud1 to 4 and spr_loading1 to 14, in order. Written by IntroSceneBuilder.
        [SerializeField] private Sprite background;
        [SerializeField] private List<Sprite> clouds = new List<Sprite>();
        [SerializeField] private List<Sprite> loadingImages = new List<Sprite>();

        // load_text.gml's introText, read from the asset manifest by IntroSceneBuilder.
        [SerializeField, TextArea(4, 12)] private string story = "";

        private const float PixelScale = OutlinedText.PixelScale;

        private readonly System.Random random = new System.Random();
        private UIDocument document;
        private VisualElement room;
        private VisualElement backdrop;
        private OutlinedText text;
        private readonly List<VisualElement> cloudElements = new List<VisualElement>();
        private VisualElement overlay;
        private InputAction skip;
        private int shownTyped = -1;
        private int? shownImage;
        private bool left;

        public void SetAssets(string nextSceneName, Sprite map, IEnumerable<Sprite> cloudSprites, IEnumerable<Sprite> loading, string introText)
        {
            nextScene = nextSceneName ?? throw new ArgumentNullException(nameof(nextSceneName));
            background = map;
            clouds = new List<Sprite>(cloudSprites ?? throw new ArgumentNullException(nameof(cloudSprites)));
            loadingImages = new List<Sprite>(loading ?? throw new ArgumentNullException(nameof(loading)));
            story = introText ?? throw new ArgumentNullException(nameof(introText));
        }

        public string NextScene => nextScene;
        public Sprite Background => background;
        public IReadOnlyList<Sprite> Clouds => clouds;
        public IReadOnlyList<Sprite> LoadingImages => loadingImages;
        public string Story => story;

        // Seconds on the realtime timeline; tests hold it still and step it by hand.
        public Func<double> Clock { get; set; } = () => Time.realtimeSinceStartupAsDouble;

        // What happens once the fade in is full: the next scene is loaded. Tests may replace it.
        public Action<string> Leave { get; set; } = name => SceneManager.LoadScene(name);

        // The clock reading the room's steps are counted from.
        public double StartedAt { get; private set; }

        // The room's state.
        public IntroScene Scene { get; private set; }

        // The intro is over and the next scene has been asked for.
        public bool HasLeft => left;

        // The drawn tree, for tests and screenshots.
        public VisualElement Root { get; private set; }
        public VisualElement Backdrop => backdrop;
        public OutlinedText Text => text;
        public IReadOnlyList<VisualElement> CloudElements => cloudElements;
        public VisualElement Overlay => overlay;

        private void Awake()
        {
            // obj_intro's F1: fullscreen (UQD-T50).
            GameWindow.On(gameObject);
            skip = new InputAction("IntroSkip", InputActionType.Button);
            skip.AddBinding("<Pointer>/press");
            skip.AddBinding("<Keyboard>/anyKey");
            skip.AddBinding("<Gamepad>/buttonSouth");
            skip.AddBinding("<Gamepad>/start");
            // F1 is the window's (GameWindow, UQD-T50), as obj_intro's own F1 press was: it does not skip.
            skip.performed += context =>
            {
                if (context.control is UnityEngine.InputSystem.Controls.AnyKeyControl && IsWindowKey()) return;
                Skip();
            };
        }

        private static bool IsWindowKey()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.f1Key.isPressed;
        }

        private void OnEnable() => skip.Enable();
        private void OnDisable() => skip.Disable();
        private void OnDestroy() => skip.Dispose();

        private void Start()
        {
            Scene = new IntroScene(story, random);
            Build();
            Begin();
        }

        // Starts the room over from the clock's reading now: new clouds, no text, spr_loading1 full and fading out.
        public void Begin()
        {
            Scene = new IntroScene(story, random);
            StartedAt = Clock();
            left = false;
            shownTyped = -1;
            shownImage = null;
            for (var i = 0; i < cloudElements.Count; i++)
            {
                var sprite = Scene.Clouds[i].Kind < clouds.Count ? clouds[Scene.Clouds[i].Kind] : null;
                cloudElements[i].style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
            }
            Advance();
            Draw();
        }

        // obj_intro's global left press: the fade to the next scene starts on the next step.
        public void Skip() => Scene?.Skip();

        private void Update()
        {
            Advance();
            Draw();
        }

        private void Advance()
        {
            Scene.RunTo(LoadingFade.StepsAt(Clock() - StartedAt, IntroScene.TicksPerSecond));
            if (Scene.Finished && !left)
            {
                left = true;
                LoadingHandoff.Give(Scene.Fade.Image);
                Leave?.Invoke(nextScene);
            }
        }

        private void Build()
        {
            var documentObject = new GameObject("IntroDocument", typeof(UIDocument));
            documentObject.transform.SetParent(transform, false);
            document = documentObject.GetComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/DuelPanelSettings");

            Root = document.rootVisualElement;
            Root.name = "Intro";
            Root.pickingMode = PickingMode.Ignore;
            Root.style.backgroundColor = Color.black;
            // The room is the frame, letterboxed into the window (UQD-T50); what strays past it is cut off at its edges.
            OriginalFrame.Fit(Root);

            // bg_mapscreen, the room's background, covering the room as the arenas do.
            backdrop = FullScreen("Map");
            backdrop.style.backgroundImage = background != null ? new StyleBackground(background) : new StyleBackground(StyleKeyword.None);
            backdrop.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            backdrop.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            backdrop.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            Root.Add(backdrop);

            // The 640 x 480 room at the original's scale, filling the frame.
            room = new VisualElement { name = "Room", pickingMode = PickingMode.Ignore };
            room.style.position = Position.Absolute;
            room.style.width = IntroScene.RoomWidth * PixelScale;
            room.style.height = IntroScene.RoomHeight * PixelScale;
            room.style.left = Length.Percent(50);
            room.style.top = Length.Percent(50);
            room.style.marginLeft = -IntroScene.RoomWidth * PixelScale / 2f;
            room.style.marginTop = -IntroScene.RoomHeight * PixelScale / 2f;
            room.style.overflow = Overflow.Visible;
            Root.Add(room);

            // obj_typewriter_intro (depth 1): left aligned from (40, 50), fnt_mono outlined 2.
            text = new OutlinedText(OriginalType.Intro) { name = "Story" };
            text.style.position = Position.Absolute;
            text.style.left = IntroScene.TextX * PixelScale;
            text.style.top = IntroScene.TextY * PixelScale;
            text.style.width = (IntroScene.RoomWidth - IntroScene.TextX) * PixelScale;
            text.style.height = (IntroScene.RoomHeight - IntroScene.TextY) * PixelScale;
            text.style.unityTextAlign = TextAnchor.UpperLeft;
            text.style.whiteSpace = WhiteSpace.NoWrap;
            text.style.overflow = Overflow.Visible;
            text.style.display = DisplayStyle.None;
            room.Add(text);

            // obj_cloud1 to 4 (depth -100, over the text), in the room's instance order.
            foreach (var cloud in Scene.Clouds)
            {
                var element = new VisualElement { name = $"Cloud{cloud.Kind + 1}", pickingMode = PickingMode.Ignore };
                element.style.position = Position.Absolute;
                element.style.width = IntroScene.CloudWidth * PixelScale;
                element.style.height = IntroScene.CloudHeight * PixelScale;
                element.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                var sprite = cloud.Kind < clouds.Count ? clouds[cloud.Kind] : null;
                element.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
                element.style.opacity = (float)IntroScene.CloudAlpha;
                room.Add(element);
                cloudElements.Add(element);
            }

            // obj_fade_intro (depth -10000): the loading image over everything, stretched over the screen.
            overlay = FullScreen("LoadingFade");
            overlay.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Root.Add(overlay);
        }

        private static VisualElement FullScreen(string name)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.style.position = Position.Absolute;
            element.style.left = element.style.top = element.style.right = element.style.bottom = 0;
            return element;
        }

        private void Draw()
        {
            if (Scene.Typed != shownTyped)
            {
                shownTyped = Scene.Typed;
                text.text = Scene.TypedText;
            }
            text.style.display = Scene.TypewriterShown ? DisplayStyle.Flex : DisplayStyle.None;

            for (var i = 0; i < cloudElements.Count; i++)
            {
                cloudElements[i].style.left = Scene.Clouds[i].X * PixelScale;
                cloudElements[i].style.top = Scene.Clouds[i].Y * PixelScale;
            }

            var fade = Scene.Fade;
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
