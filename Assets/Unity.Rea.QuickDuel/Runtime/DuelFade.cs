using System;
using System.Collections.Generic;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // The round transition (UQD-T42), as obj_fade_gamescreen drew it: three seconds after a round ends, one of the 14
    // loading images fades in over everything at 0.005 a step; once it is full the battle is reset under it (BattleReset:
    // the blood emitter and the weapons gone, both fighters back to idle, the aftermath's text cleared) and it fades out
    // again over the startup (RoundFade). Presentation only: it reads the verdict and the clock and decides nothing.
    //
    // Everything steps at the original's 60 a second counted off the controller's clock, not rendered frames. The image
    // is drawn at depth -10000, over everything else on the panel, stretched over the whole screen as the original's
    // 640 x 480 room. A round begun before the fade is done (online, the host's Space) simply takes the image away.
    //
    // Arriving from the main menu (or the verdict's Retry), the scene opens under the loading image that faded in there
    // (LoadingHandoff, the original's fade_asset) and fades it away at obj_fade_gamescreen's 0.005 a step, as
    // gameclock_startup's first pass turned that fade to "fadeout" from full (UQD-T66; it had faded at the menu's 0.01).
    // The first round's startup counts under it from the scene's start (juststarted, DuelStartup).
    [RequireComponent(typeof(DuelController), typeof(DuelView))]
    public sealed class DuelFade : MonoBehaviour
    {
        // spr_loading1 to 14, in order. Written by DuelSceneBuilder.
        [SerializeField] private List<Sprite> images = new List<Sprite>();

        private DuelController controller;
        private DuelView view;
        private VisualElement overlay;
        private readonly System.Random random = new System.Random();
        private double? endedAt;
        private bool reset;
        private LoadingFade arrival;
        private double arrivedAt;
        private long arrivalSteps;

        public void SetImages(IEnumerable<Sprite> loadingImages)
        {
            images = new List<Sprite>(loadingImages ?? throw new ArgumentNullException(nameof(loadingImages)));
        }

        public IReadOnlyList<Sprite> Images => images;

        // Raised once a transition's image is full and the battle is reset under it.
        public event Action BattleReset;

        // The loading image this transition draws (0 to 13 for spr_loading1 to 14); null when none is running.
        public int? Image { get; private set; }

        // Where the transition stands; Off when none is running.
        public RoundFadeStage Stage { get; private set; } = RoundFadeStage.Off;

        // The image's alpha now.
        public float Alpha { get; private set; }

        // A round has ended and the battle has not been reset under the image yet: the next round waits.
        public bool Pending => endedAt.HasValue && !reset;

        // The battle has been reset under this transition's image (until the next round begins).
        public bool HasReset => reset;

        // The intro's image fading away as the scene opens (0 to 13 for spr_loading1 to 14); null once it is gone or when
        // the scene was not entered from the intro.
        public int? ArrivalImage => arrival?.Image;

        // The clock reading the scene opened under it.
        public double ArrivedAt => arrivedAt;

        // That image's alpha now.
        public float ArrivalAlpha => arrival != null ? (float)arrival.DrawnAlpha : 0f;

        // The drawn image, for tests and screenshots.
        public VisualElement Overlay => overlay;

        private void Start()
        {
            controller = GetComponent<DuelController>();
            view = GetComponent<DuelView>();

            overlay = new VisualElement { name = "LoadingFade", pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            overlay.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            overlay.style.display = DisplayStyle.None;
            view.Root.Add(overlay);

            controller.RoundStarted += OnRoundStarted;
            controller.Resolved += OnResolved;

            if (LoadingHandoff.TryTake(out var image) && image >= 0 && image < LoadingFade.Images)
            {
                arrival = new LoadingFade(image, RoundFade.FadeSpeed);
                arrivedAt = controller.Now;
                arrivalSteps = 0;
                ShowImage(image);
            }
            Advance();
            Draw();
        }

        private void OnDestroy()
        {
            if (controller == null) return;
            controller.RoundStarted -= OnRoundStarted;
            controller.Resolved -= OnResolved;
        }

        // The next round has begun: whatever is left of the image goes with it.
        private void OnRoundStarted()
        {
            endedAt = null;
            reset = false;
            Image = null;
            Draw();
        }

        // The aftermath runs from the moment the round ended (its first strike, its early strike or its timeout).
        private void OnResolved(DuelResult verdict)
        {
            endedAt = controller.EndedAt ?? controller.Now;
            reset = false;
            Image = RoundFade.Pick(random);
            arrival = null;
            ShowImage(Image.Value);
            Advance();
            Draw();
        }

        // room_goto out of the room (the verdict screen, UQD-T48): the transition ends where it stands, its image gone with
        // the room.
        public void Cut()
        {
            endedAt = null;
            reset = false;
            Image = null;
            arrival = null;
            Stage = RoundFadeStage.Off;
            Alpha = 0f;
            Draw();
        }

        private void ShowImage(int image)
        {
            var sprite = image < images.Count ? images[image] : null;
            // A scene that carries no images fades a black screen instead.
            overlay.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
            overlay.style.backgroundColor = sprite != null ? Color.clear : Color.black;
        }

        private void Update()
        {
            Advance();
            Draw();
        }

        // Runs the transition on to the clock's current step, resetting the battle once the image is full.
        private void Advance()
        {
            if (arrival != null)
            {
                var steps = LoadingFade.StepsAt(controller.Now - arrivedAt);
                while (arrivalSteps < steps && arrival.Mode == LoadingFadeMode.FadeOut)
                {
                    arrival.Step(null);
                    arrivalSteps++;
                }
                if (arrival.Mode != LoadingFadeMode.FadeOut)
                    arrival = null;
            }

            if (!endedAt.HasValue)
            {
                Stage = RoundFadeStage.Off;
                Alpha = 0f;
                return;
            }
            var tick = RoundFade.TickAt(controller.Now - endedAt.Value);
            Stage = RoundFade.StageAt(tick);
            Alpha = (float)RoundFade.AlphaAt(tick);
            if (!reset && RoundFade.HasReset(tick))
            {
                reset = true;
                BattleReset?.Invoke();
            }
        }

        private void Draw()
        {
            if (overlay == null) return;
            var shown = endedAt.HasValue && Alpha > 0f;
            var opacity = shown ? Alpha : 0f;
            if (!endedAt.HasValue && ArrivalAlpha > 0f)
            {
                shown = true;
                opacity = ArrivalAlpha;
            }
            overlay.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            overlay.style.opacity = opacity;
            // Depth -10000: over everything any other component has hung off the panel since.
            if (shown && overlay.parent != null && overlay.parent.IndexOf(overlay) != overlay.parent.childCount - 1)
                overlay.BringToFront();
        }
    }
}
