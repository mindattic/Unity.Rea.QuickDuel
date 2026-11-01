using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // The original's F12 debug overlay (UQD-T50, DebugReadout), the developer's toggle: each side's reaction in whole
    // milliseconds (9999 before it has struck) at the room's middle height by its edge, the frames drawn a second in
    // room_speed's place, and the phase with its count at the top left, in the fonts and places obj_game and
    // obj_gameclock drew them. Off when the scene starts; F12 turns it on and off, in the editor and development builds
    // only (Debug.isDebugBuild), never in a release build. It draws over everything in the duel, so it can be read
    // whatever is in front.
    [RequireComponent(typeof(DuelController), typeof(DuelView))]
    public sealed class DuelDebugOverlay : MonoBehaviour
    {
        private const float PixelScale = DuelView.PixelScale;
        private const float LabelWidth = 400f;

        private DuelController controller;
        private DuelView view;
        private InputAction toggle;
        private double rate;

        // Whether F12 does anything here: the editor and development builds. Tests may change it.
        public bool Allowed { get; set; }

        // The overlay is up.
        public bool Visible { get; private set; }

        // The drawn overlay, for tests and screenshots.
        public VisualElement Element { get; private set; }
        public OutlinedText LeftReaction { get; private set; }
        public OutlinedText RightReaction { get; private set; }
        public OutlinedText Rate { get; private set; }
        public OutlinedText Phase { get; private set; }
        public OutlinedText Count { get; private set; }

        private void Awake()
        {
            Allowed = Debug.isDebugBuild;
            // obj_game's F12 key press: debug = !debug.
            toggle = new InputAction("DebugOverlay", InputActionType.Button, "<Keyboard>/f12");
            toggle.performed += _ => Toggle();
        }

        private void OnEnable() => toggle.Enable();
        private void OnDisable() => toggle.Disable();
        private void OnDestroy() => toggle.Dispose();

        private void Start()
        {
            controller = GetComponent<DuelController>();
            view = GetComponent<DuelView>();
            Build();
            Draw();
        }

        // F12: on and off, when allowed.
        public void Toggle()
        {
            if (!Allowed) return;
            Visible = !Visible;
            if (Element != null) Draw();
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            if (dt > 0f) rate = rate <= 0 ? 1.0 / dt : rate + (1.0 / dt - rate) * 0.1;
            Draw();
        }

        private void Build()
        {
            Element = new VisualElement { name = "DebugOverlay", pickingMode = PickingMode.Ignore };
            Element.style.position = Position.Absolute;
            Element.style.left = Element.style.top = Element.style.right = Element.style.bottom = 0;
            // Over everything, the letterbox and the fades included.
            view.Root.Add(Element);

            Rate = Line("DebugRate", OriginalType.Name, DebugReadout.RateX, DebugReadout.RateY, TextAnchor.UpperLeft);
            LeftReaction = Line("DebugLeftReaction", OriginalType.Name, DebugReadout.LeftReactionX, DebugReadout.ReactionY, TextAnchor.UpperLeft);
            RightReaction = Line("DebugRightReaction", OriginalType.Name, DebugReadout.RightReactionX, DebugReadout.ReactionY, TextAnchor.UpperRight);
            Phase = Line("DebugPhase", OriginalType.Hud, DebugReadout.PhaseX, DebugReadout.PhaseY, TextAnchor.UpperLeft);
            Count = Line("DebugCount", OriginalType.Hud, DebugReadout.PhaseX, DebugReadout.CountY, TextAnchor.UpperLeft);
        }

        // Text whose top is at room y, starting (fa_left) or ending (fa_right) at room x.
        private OutlinedText Line(string name, TypeStyle style, int x, int y, TextAnchor align)
        {
            var text = new OutlinedText(style) { name = name };
            text.style.position = Position.Absolute;
            var right = align == TextAnchor.UpperRight;
            text.style.left = x * PixelScale - (right ? LabelWidth : 0f);
            text.style.top = y * PixelScale;
            text.style.width = LabelWidth;
            text.style.height = text.LineHeight;
            text.style.unityTextAlign = align;
            text.style.whiteSpace = WhiteSpace.NoWrap;
            text.style.overflow = Overflow.Visible;
            Element.Add(text);
            return text;
        }

        private void Draw()
        {
            Element.style.display = Visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!Visible) return;
            Element.BringToFront();

            var phase = controller.Phase;
            var result = phase == DuelPhase.Resolved ? controller.Result : null;
            Set(LeftReaction, DebugReadout.Reaction(result?.LeftReaction));
            Set(RightReaction, DebugReadout.Reaction(result?.RightReaction));
            Set(Rate, DebugReadout.Rate(rate));
            Set(Phase, phase.ToString());
            Set(Count, phase == DuelPhase.Countdown ? DebugReadout.Milliseconds(controller.CountdownRemaining)
                : phase == DuelPhase.Strike || phase == DuelPhase.Resolved ? controller.ReactionCount.ToString()
                : "");
        }

        private static void Set(OutlinedText text, string value)
        {
            if (text.text != value) text.text = value;
        }
    }
}
