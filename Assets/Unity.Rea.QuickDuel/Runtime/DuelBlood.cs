using System;
using System.Collections.Generic;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // The blood of the cut (UQD-T40): when a strike ends the round, as gameclock_aftermath.gml did, a blood emitter at the
    // centre of the fighters' line spurts a drop every 2 steps (BloodSpray), 6 to 12 splatters land over the screen
    // (SplatterField) and the screen flashes white ten times (WhiteFlash). Presentation only: it reads the verdict and the
    // clock and decides nothing.
    //
    // Everything steps at the original's 60 a second counted off the controller's clock, not rendered frames. Layers keep
    // the original's depths: the drops (depth 1) behind the fighters and the text (0), the splatters (-100) over them,
    // the flash (-1000) over those, and the letterbox (-10000) over all. A splatter is wiped quickly (0.01 a step) by a
    // left or right click on it, or by dragging into it with a button held, as obj_splatter's mouse events did; the
    // pointer is only looked at, so a click still strikes as it always has.
    [RequireComponent(typeof(DuelController), typeof(DuelView))]
    public sealed class DuelBlood : MonoBehaviour
    {
        // spr_blood_drop1 and 2, and spr_splatter1 to 10, in order; spr_flash. Written by DuelSceneBuilder.
        [SerializeField] private List<Sprite> drops = new List<Sprite>();
        [SerializeField] private List<SplatterLook> splatters = new List<SplatterLook>();
        [SerializeField] private Sprite flash;

        // A stall (a scene load) fades the splatters on by at most this many steps at once.
        private const int MaxFadeSteps = 60;

        private DuelController controller;
        private DuelView view;
        private VisualElement dropLayer;
        private VisualElement splatterLayer;
        private VisualElement flashBox;
        private readonly List<VisualElement> dropElements = new List<VisualElement>();
        private readonly Dictionary<Splatter, VisualElement> splatterElements = new Dictionary<Splatter, VisualElement>();
        private readonly List<Splatter> gone = new List<Splatter>();
        private readonly System.Random random = new System.Random();
        private SplatterField field;
        private long spraySince;
        private long flashSince;
        private long fieldTick;
        private bool flashing;

        public void SetSprites(IEnumerable<Sprite> dropSprites, IEnumerable<SplatterLook> splatterLooks, Sprite flashSprite)
        {
            drops = new List<Sprite>(dropSprites ?? throw new ArgumentNullException(nameof(dropSprites)));
            splatters = new List<SplatterLook>(splatterLooks ?? throw new ArgumentNullException(nameof(splatterLooks)));
            flash = flashSprite;
        }

        public IReadOnlyList<Sprite> Drops => drops;
        public IReadOnlyList<SplatterLook> Splatters => splatters;
        public Sprite Flash => flash;

        // This round's spray; null before the first cut.
        public BloodSpray Spray { get; private set; }

        // The splatters on screen.
        public SplatterField Field => field;

        // The flash's alpha now: 0.6 or 0 while it blinks, 0 once it is done.
        public float FlashAlpha => flashBox != null && flashBox.style.display != DisplayStyle.None ? flashBox.style.opacity.value : 0f;

        // The drawn elements, for tests and screenshots.
        public VisualElement DropLayer => dropLayer;
        public VisualElement SplatterLayer => splatterLayer;
        public VisualElement FlashBox => flashBox;
        public VisualElement ElementOf(Splatter splatter) => splatterElements.TryGetValue(splatter, out var e) ? e : null;
        public int DropsDrawn
        {
            get
            {
                var n = 0;
                foreach (var e in dropElements)
                    if (e.style.display != DisplayStyle.None) n++;
                return n;
            }
        }

        private void Start()
        {
            controller = GetComponent<DuelController>();
            view = GetComponent<DuelView>();
            field = new SplatterField(random);
            fieldTick = TickNow;

            dropLayer = Layer("BloodDrops");
            Fill(dropLayer);
            view.Stage.Insert(0, dropLayer);

            var root = view.Root;
            var at = root.IndexOf(view.Stage) + 1;
            splatterLayer = Layer("Splatters");
            Fill(splatterLayer);
            root.Insert(at, splatterLayer);

            flashBox = Layer("Flash");
            Fill(flashBox);
            if (flash != null)
            {
                flashBox.style.backgroundImage = new StyleBackground(flash);
                flashBox.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            }
            else
            {
                flashBox.style.backgroundColor = Color.white;
            }
            flashBox.style.display = DisplayStyle.None;
            root.Insert(at + 1, flashBox);

            controller.RoundStarted += OnRoundStarted;
            controller.Resolved += OnResolved;
            fade = GetComponent<DuelFade>();
            if (fade != null) fade.BattleReset += OnRoundStarted;
        }

        private DuelFade fade;

        private void OnDestroy()
        {
            if (fade != null) fade.BattleReset -= OnRoundStarted;
            if (controller == null) return;
            controller.RoundStarted -= OnRoundStarted;
            controller.Resolved -= OnResolved;
        }

        // The loading fade (UQD-T42) destroyed the emitter under its full image, or the next round began first; its drops
        // fall on, and the splatters are not the fade's to destroy (they fade on at their own pace).
        private void OnRoundStarted() => Spray?.Stop();

        private void OnResolved(DuelResult verdict)
        {
            if (!BloodSpray.Spills(verdict))
                return;
            Spray?.Stop();
            Spray = new BloodSpray(random);
            // The killing blow's defeat strip ends the spray at its Animation End (UQD-T64).
            if (verdict.Winner.HasValue && view.IsKillingBlow)
                Spray.StopAfter(FighterMotion.StripTicks);
            spraySince = TickNow;
            Advance();
            field.Spill();
            flashing = true;
            flashSince = TickNow;
            Draw();
        }

        private long TickNow => (long)Math.Floor(controller.Now * BloodSpray.TicksPerSecond);

        private void Update()
        {
            ReadPointer();
            Advance();
            Draw();
        }

        // Runs everything on to the clock's current step.
        private void Advance()
        {
            var now = TickNow;
            Spray?.AdvanceTo((int)Math.Min(int.MaxValue, now - spraySince));
            var steps = now - fieldTick;
            fieldTick = now;
            if (steps > 0) field.Advance((int)Math.Min(steps, MaxFadeSteps));
            if (flashing && now - flashSince >= WhiteFlash.Ticks) flashing = false;
        }

        private void Draw()
        {
            DrawDrops();
            DrawSplatters();
            var alpha = flashing ? WhiteFlash.AlphaAt((int)(TickNow - flashSince)) : 0;
            flashBox.style.display = alpha > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            flashBox.style.opacity = (float)alpha;
        }

        // Each drop at its room position on the stage, the original's pixels scaled up as the fighters are.
        private void DrawDrops()
        {
            var live = Spray?.Drops;
            var count = live?.Count ?? 0;
            while (dropElements.Count < count)
            {
                var element = new VisualElement { name = "Drop", pickingMode = PickingMode.Ignore };
                element.style.position = Position.Absolute;
                element.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                dropLayer.Add(element);
                dropElements.Add(element);
            }
            for (var i = 0; i < dropElements.Count; i++)
            {
                var element = dropElements[i];
                if (i >= count)
                {
                    element.style.display = DisplayStyle.None;
                    continue;
                }
                var drop = live[i];
                var size = BloodSpray.DropHeights[drop.Sprite] * DuelView.PixelScale;
                var sprite = drop.Sprite < drops.Count ? drops[drop.Sprite] : null;
                element.style.display = DisplayStyle.Flex;
                element.style.width = size;
                element.style.height = size;
                element.style.left = DuelView.StageWidth / 2f + (drop.X - BloodSpray.OriginX) * DuelView.PixelScale - size / 2f;
                element.style.top = drop.Y * DuelView.PixelScale - size / 2f;
                if (sprite != null)
                    element.style.backgroundImage = new StyleBackground(sprite);
                else
                    element.style.backgroundColor = new Color(0.55f, 0f, 0f);
            }
        }

        // Each splatter centred on its room position spread over the whole screen, at its alpha, drawn at the original's
        // pixel scale.
        private void DrawSplatters()
        {
            gone.Clear();
            foreach (var pair in splatterElements)
                if (!Contains(field.Splatters, pair.Key))
                    gone.Add(pair.Key);
            foreach (var splatter in gone)
            {
                splatterElements[splatter].RemoveFromHierarchy();
                splatterElements.Remove(splatter);
            }

            var size = SplatterField.Size * DuelView.PixelScale;
            foreach (var splatter in field.Splatters)
            {
                if (!splatterElements.TryGetValue(splatter, out var element))
                {
                    element = new VisualElement { name = "Splatter", pickingMode = PickingMode.Ignore };
                    element.style.position = Position.Absolute;
                    element.style.width = size;
                    element.style.height = size;
                    element.style.marginLeft = -size / 2f;
                    element.style.marginTop = -size / 2f;
                    element.style.left = Length.Percent(100f * splatter.X / BloodSpray.RoomWidth);
                    element.style.top = Length.Percent(100f * splatter.Y / BloodSpray.RoomHeight);
                    element.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                    var look = splatter.Kind < splatters.Count ? splatters[splatter.Kind] : null;
                    if (look?.Sprite != null)
                        element.style.backgroundImage = new StyleBackground(look.Sprite);
                    else
                        element.style.backgroundColor = new Color(0.55f, 0f, 0f);
                    splatterLayer.Add(element);
                    splatterElements[splatter] = element;
                }
                element.style.opacity = (float)splatter.Alpha;
            }
        }

        private static bool Contains(IReadOnlyList<Splatter> list, Splatter item)
        {
            for (var i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], item)) return true;
            return false;
        }

        // The pointer this frame, in panel space: obj_splatter's left or right button events.
        private void ReadPointer()
        {
            var pointer = Pointer.current;
            if (pointer == null || view.Root.panel == null || field.Splatters.Count == 0)
                return;
            var mouse = Mouse.current;
            var held = pointer.press.isPressed || (mouse != null && mouse.rightButton.isPressed);
            var pressed = pointer.press.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame);
            var screen = pointer.position.ReadValue();
            var panel = RuntimePanelUtils.ScreenToPanel(view.Root.panel, new Vector2(screen.x, Screen.height - screen.y));
            Touch(panel, held, pressed);
        }

        // The pointer at `panelPosition` (panel space, y down) with a button held or pressed this frame: wipes every
        // splatter whose bounding box it presses on or is dragged into. Returns how many are being wiped.
        public int Touch(Vector2 panelPosition, bool held, bool pressed)
        {
            var wiping = 0;
            foreach (var splatter in field.Splatters)
            {
                var inside = splatterElements.TryGetValue(splatter, out var element) && HitBox(splatter, element).Contains(panelPosition);
                if (splatter.Touch(inside, held, pressed)) wiping++;
            }
            return wiping;
        }

        // The splatter's bounding box in panel space: the opaque pixels' box (as GameMaker's mask) within its element.
        public Rect HitBox(Splatter splatter)
        {
            return splatterElements.TryGetValue(splatter, out var element) ? HitBox(splatter, element) : Rect.zero;
        }

        private Rect HitBox(Splatter splatter, VisualElement element)
        {
            var bound = element.worldBound;
            var look = splatter.Kind < splatters.Count ? splatters[splatter.Kind] : null;
            if (look == null || look.Bounds.width <= 0) return bound;
            var scale = bound.width / SplatterField.Size;
            var box = look.Bounds;
            return new Rect(bound.x + box.x * scale, bound.y + box.y * scale, box.width * scale, box.height * scale);
        }

        private static VisualElement Layer(string name)
        {
            var layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            return layer;
        }

        private static void Fill(VisualElement element)
        {
            element.style.left = 0;
            element.style.top = 0;
            element.style.right = 0;
            element.style.bottom = 0;
        }
    }
}
