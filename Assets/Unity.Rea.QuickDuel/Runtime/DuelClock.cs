using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // The match clock (UQD-T50), as obj_time drew it (RoundClock): "mm:ss" in fnt_basic outlined 1, white, at the top
    // centre of the room, the minutes ending 3 pixels left of the middle, the colon centred on it and the seconds starting
    // 3 right of it, dropping in from 100 pixels above the room 5 a step until it is home 5 pixels down, then counting
    // the match's time.
    //
    // obj_game made one obj_time with each gamescreen room, that is with each match, and it ran until the room was left.
    // So the clock starts with the scene and starts over, dropping in again, whenever a new match takes the field in it
    // (a new match or fighter roll: a mode change, or online the host's next match), and it stops where it is once the
    // verdict screen covers the duel (UQD-T48), the original's room_goto. Everything steps at the original's 60 a second
    // off the controller's clock (UQD-LAW-2), never by rendered frames.
    //
    // obj_time drew at depth 0 in the ordinary Draw event, so it stands on the stage with the duel's text: over the arena
    // and the fighters, under the splatters, the flash, the corner arts, the letterbox and the loading fade.
    [RequireComponent(typeof(DuelController), typeof(DuelView))]
    public sealed class DuelClock : MonoBehaviour
    {
        private const float PixelScale = DuelView.PixelScale;

        // A label's box: wide enough for any reading, and the face's own line high.
        private const float LabelWidth = 200f;

        private DuelController controller;
        private DuelView view;
        private DuelVerdict verdict;
        private int? shownRoll;
        private IMatchView shownMatch;
        private double? stoppedAt;

        // The clock reading the clock's steps are counted from: when the scene, or the match on the field, began.
        public double StartedAt { get; private set; }

        // The step the clock is on, from StartedAt (held once the verdict is up).
        public long Tick { get; private set; } = -1;

        // The reading on screen, "mm:ss".
        public string Text => RoundClock.Text(Tick);

        // timeY: the top of the text, in room pixels.
        public int Y => RoundClock.YAt(Tick);

        // in_position: home, and counting.
        public bool InPosition => RoundClock.InPosition(Tick);

        // The clock has stopped (the verdict covers the duel).
        public bool Stopped => stoppedAt.HasValue;

        // The drawn parts, for tests and screenshots: strMinutes, the ":" and strSeconds.
        public VisualElement Element { get; private set; }
        public OutlinedText Minutes { get; private set; }
        public OutlinedText Colon { get; private set; }
        public OutlinedText Seconds { get; private set; }

        private void Start()
        {
            controller = GetComponent<DuelController>();
            view = GetComponent<DuelView>();
            verdict = GetComponent<DuelVerdict>();
            Build();
            Restart();
            Advance();
            Draw();
        }

        private void Update()
        {
            Advance();
            Draw();
        }

        // A new obj_time: from the top, at 00:00.
        private void Restart()
        {
            shownRoll = controller.FighterRoll;
            shownMatch = controller.Match;
            StartedAt = controller.Now;
            stoppedAt = null;
        }

        private void Advance()
        {
            if (shownRoll != controller.FighterRoll || !ReferenceEquals(shownMatch, controller.Match))
                Restart();
            if (!stoppedAt.HasValue && verdict != null && verdict.Shown)
                stoppedAt = controller.Now;
            var now = stoppedAt ?? controller.Now;
            Tick = RoundClock.TickAt(now - StartedAt);
        }

        private void Build()
        {
            Element = new VisualElement { name = "Clock", pickingMode = PickingMode.Ignore };
            Element.style.position = Position.Absolute;
            Element.style.left = 0;
            Element.style.width = DuelView.StageWidth;
            view.Stage.Add(Element);

            // fa_right at room_width / 2 - 3, fa_center at room_width / 2, fa_left at room_width / 2 + 3; all fa_top.
            Minutes = Part("ClockMinutes", RoundClock.MinutesRight - LabelWidth / PixelScale, TextAnchor.UpperRight);
            Colon = Part("ClockColon", RoundClock.ColonX - LabelWidth / PixelScale / 2f, TextAnchor.UpperCenter);
            Colon.text = ":";
            Seconds = Part("ClockSeconds", RoundClock.SecondsLeft, TextAnchor.UpperLeft);
        }

        private OutlinedText Part(string name, float left, TextAnchor align)
        {
            var text = new OutlinedText(OriginalType.Hud) { name = name };
            text.style.position = Position.Absolute;
            text.style.left = left * PixelScale;
            text.style.top = 0;
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
            Element.style.top = Y * PixelScale;
            var minutes = RoundClock.MinutesText(Tick);
            var seconds = RoundClock.SecondsText(Tick);
            if (Minutes.text != minutes) Minutes.text = minutes;
            if (Seconds.text != seconds) Seconds.text = seconds;
        }
    }
}
