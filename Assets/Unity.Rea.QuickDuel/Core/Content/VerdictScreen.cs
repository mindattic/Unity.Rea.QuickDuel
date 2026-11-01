using System;
using System.Globalization;

namespace Unity.Rea.QuickDuel.Core
{
    // Which of the original's two verdict rooms a finished match goes to.
    public enum VerdictKind { Victory, GameOver }

    // What the verdict screen's button chose: nothing yet, a new match in the same mode, or the main menu.
    public enum VerdictChoice { None, Retry, Continue }

    // The match verdict (UQD-T48), as the original's victory and gameover rooms had it. Every figure is the original's
    // own, read from BushidoCoda.gmx, not a tuning choice:
    //
    //   obj_fade_gamescreen  once its loading image is full and the battle reset under it: if (is_singleplayer)
    //                        { if (player[left].HP <= 0) room_goto(gameover) else if (player[right].HP <= 0)
    //                        room_goto(victory) } -- a cut, the verdict rooms have no fade of their own
    //   victory.room         640 x 480 at 60 fps, bg_victory (640 x 480) as its background; obj_btn_nextbattle at
    //                        (576, 448) and obj_victory
    //   gameover.room        the same with bg_gameover; obj_btn_retry at (576, 448), obj_victory, seven obj_virtue
    //   obj_gameover         fnt_oldenglish, draw_text_outline(room_width/2, 64, 2, "Game Over")
    //   obj_victory          fnt_oldenglish_small, draw_text_outline(room_width/2, room_height/4, 2, "Round Time"),
    //                        (.., room_height/3, 2, "Fastest Reaction"), (.., room_height/2, 2, "Other")
    //   obj_btn_retry /      spr_btn_right4 (origin 64, 32, bbox 1, 4 to 127, 63); Mouse enter: showmsg = true (no
    //   obj_btn_nextbattle   flick); Mouse leave: showmsg = false; Left pressed: audio_play_sound(sfx_click) and
    //                        room_goto(travel), or room_goto(location) in Versus; Draw: spr_btn_right4_over while
    //                        showmsg, else spr_btn_right4, and "Retry" / "Continue" in fnt_basic outlined 1 centred on
    //                        (x + 6, y - 6)
    //
    // The rebuild's best of 3 replaces the HP that sent the original there (BIBLE), and the travel and location rooms the
    // buttons went on to are dropped with the overworld, so Retry starts a new match in the same mode and Continue goes
    // back to the main menu. Fixed defects: the gameover room placed obj_victory where obj_gameover was meant to be, so
    // "Game Over" was never drawn, and both drew their headings and labels left-aligned from the room's middle; obj_victory's
    // three labels had no values, so the rebuild fills the first two with the match's result and its fastest reaction
    // ("Other" had nothing to say and is left out). Victory, which had no heading, takes "Victory" in Game Over's style.
    public sealed class VerdictScreen
    {
        public const int RoomWidth = 640;
        public const int RoomHeight = 480;

        public const string VictoryBackground = "bg_victory";
        public const string GameOverBackground = "bg_gameover";

        public const string VictoryHeading = "Victory";
        public const string GameOverHeading = "Game Over";

        // obj_gameover: centred on room_width/2, its top at 64.
        public const int HeadingX = RoomWidth / 2;
        public const int HeadingY = 64;

        // obj_victory's first two lines: room_height/4 and room_height/3.
        public const int ResultY = RoomHeight / 4;
        public const int FastestY = RoomHeight / 3;

        public const string FastestLabel = "Fastest Reaction";

        public VerdictScreen(VerdictKind kind, string result, string fastest)
        {
            Kind = kind;
            Result = result ?? "";
            Fastest = fastest ?? "";
            Button = kind == VerdictKind.GameOver
                ? new MenuButton(MenuButtonKind.Retry, 576, 448, "spr_btn_right4", true, "Retry", "", 1, 4, 127, 63)
                : new MenuButton(MenuButtonKind.Continue, 576, 448, "spr_btn_right4", true, "Continue", "", 1, 4, 127, 63);
        }

        // The screen for a finished match: Game Over when the computer took it, Victory otherwise (in Versus a player always
        // won). `nameOf` names each seat.
        public static VerdictScreen For(IMatchView match, bool vsComputer, Func<Side, string> nameOf)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (nameOf == null) throw new ArgumentNullException(nameof(nameOf));
            if (!match.Winner.HasValue) throw new InvalidOperationException("The match is not over.");
            var winner = match.Winner.Value;
            var loser = winner == Side.Left ? Side.Right : Side.Left;
            var result = $"{nameOf(winner)} takes the match {match.WinsOf(winner)}-{match.WinsOf(loser)}";
            return new VerdictScreen(KindOf(winner, vsComputer), result, FastestLine(match, nameOf));
        }

        // Game Over only when the computer, in the right seat, won.
        public static VerdictKind KindOf(Side winner, bool vsComputer) =>
            vsComputer && winner == Side.Right ? VerdictKind.GameOver : VerdictKind.Victory;

        // The match's fastest strike and whose it was, or "Fastest Reaction  -" when nobody struck in time.
        public static string FastestLine(IMatchView match, Func<Side, string> nameOf)
        {
            var left = match.BestReactionOf(Side.Left);
            var right = match.BestReactionOf(Side.Right);
            if (!left.HasValue && !right.HasValue)
                return FastestLabel + "  -";
            var side = !right.HasValue || (left.HasValue && left.Value <= right.Value) ? Side.Left : Side.Right;
            var seconds = side == Side.Left ? left.Value : right.Value;
            var ms = Math.Round(seconds * 1000, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
            return $"{FastestLabel}  {ms} ms ({nameOf(side)})";
        }

        public VerdictKind Kind { get; }

        // bg_victory or bg_gameover.
        public string Background => Kind == VerdictKind.GameOver ? GameOverBackground : VictoryBackground;

        public string Heading => Kind == VerdictKind.GameOver ? GameOverHeading : VictoryHeading;

        // "Left takes the match 2-1", on obj_victory's first line.
        public string Result { get; }

        // The match's fastest reaction, on its second.
        public string Fastest { get; }

        // obj_btn_retry on Game Over, obj_btn_nextbattle on Victory.
        public MenuButton Button { get; }

        // The mouse is over the button (its showmsg): the "_over" sprite is drawn.
        public bool Hovered { get; private set; }

        public VerdictChoice Choice { get; private set; }

        // The button was pressed: sfx_click.
        public event Action<MenuButton> Clicked;

        // The mouse is at room position (x, y).
        public void PointAt(double x, double y)
        {
            if (Choice != VerdictChoice.None) return;
            Hovered = Button.Contains(x, y);
        }

        // The left button went down at room position (x, y): the button is pressed if the mouse is on it.
        public void Press(double x, double y)
        {
            PointAt(x, y);
            if (Hovered) Activate();
        }

        // A keyboard or pad presses the one button (not the original's, which had no keys here).
        public void Activate()
        {
            if (Choice != VerdictChoice.None) return;
            Hovered = true;
            Choice = Button.Kind == MenuButtonKind.Retry ? VerdictChoice.Retry : VerdictChoice.Continue;
            Clicked?.Invoke(Button);
        }
    }
}
