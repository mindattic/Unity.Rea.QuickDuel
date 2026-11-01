using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The match verdict (UQD-T48): the original's victory and gameover rooms, their heading, obj_victory's lines and
    // obj_btn_retry / obj_btn_nextbattle.
    public class VerdictScreenTests
    {
        private static string Name(Side side) => side == Side.Left ? "Left" : "The AI";

        private static DuelResult Won(Side winner, double reaction) => winner == Side.Left
            ? new DuelResult(DuelOutcome.FasterStrike, Side.Left, reaction, reaction + 0.1)
            : new DuelResult(DuelOutcome.FasterStrike, Side.Right, reaction + 0.1, reaction);

        private static Match Played(params DuelResult[] rounds)
        {
            var match = new Match(2);
            foreach (var round in rounds) match.Record(round);
            return match;
        }

        [Test]
        public void The_computer_taking_the_match_is_game_over_and_anything_else_a_victory()
        {
            Assert.That(VerdictScreen.KindOf(Side.Right, vsComputer: true), Is.EqualTo(VerdictKind.GameOver));
            Assert.That(VerdictScreen.KindOf(Side.Left, vsComputer: true), Is.EqualTo(VerdictKind.Victory));
            Assert.That(VerdictScreen.KindOf(Side.Right, vsComputer: false), Is.EqualTo(VerdictKind.Victory), "in Versus a player won");
            Assert.That(VerdictScreen.KindOf(Side.Left, vsComputer: false), Is.EqualTo(VerdictKind.Victory));
        }

        [Test]
        public void Game_over_draws_bg_gameover_its_heading_and_the_retry_button()
        {
            var screen = VerdictScreen.For(Played(Won(Side.Right, 0.2), Won(Side.Left, 0.25), Won(Side.Right, 0.3)), true, Name);
            Assert.That(screen.Kind, Is.EqualTo(VerdictKind.GameOver));
            Assert.That(screen.Background, Is.EqualTo("bg_gameover"));
            Assert.That(screen.Heading, Is.EqualTo("Game Over"));
            Assert.That(screen.Result, Is.EqualTo("The AI takes the match 2-1"));
            Assert.That(screen.Fastest, Is.EqualTo("Fastest Reaction  200 ms (The AI)"));
            Assert.That(screen.Button.Kind, Is.EqualTo(MenuButtonKind.Retry));
            Assert.That(screen.Button.Label, Is.EqualTo("Retry"));
        }

        [Test]
        public void Victory_draws_bg_victory_its_heading_and_the_continue_button()
        {
            var screen = VerdictScreen.For(Played(Won(Side.Left, 0.2), Won(Side.Left, 0.18)), true, Name);
            Assert.That(screen.Kind, Is.EqualTo(VerdictKind.Victory));
            Assert.That(screen.Background, Is.EqualTo("bg_victory"));
            Assert.That(screen.Heading, Is.EqualTo("Victory"));
            Assert.That(screen.Result, Is.EqualTo("Left takes the match 2-0"));
            Assert.That(screen.Fastest, Is.EqualTo("Fastest Reaction  180 ms (Left)"));
            Assert.That(screen.Button.Kind, Is.EqualTo(MenuButtonKind.Continue));
            Assert.That(screen.Button.Label, Is.EqualTo("Continue"));
        }

        [Test]
        public void A_match_nobody_struck_in_time_has_no_fastest_reaction()
        {
            var match = Played(new DuelResult(DuelOutcome.EarlyStrike, Side.Left, null, -1), new DuelResult(DuelOutcome.EarlyStrike, Side.Left, null, -1));
            Assert.That(VerdictScreen.FastestLine(match, Name), Is.EqualTo("Fastest Reaction  -"));
        }

        [Test]
        public void An_unfinished_match_has_no_verdict()
        {
            Assert.Throws<InvalidOperationException>(() => VerdictScreen.For(Played(Won(Side.Left, 0.2)), true, Name));
        }

        [Test]
        public void Everything_stands_where_the_original_drew_it()
        {
            Assert.That((VerdictScreen.HeadingX, VerdictScreen.HeadingY), Is.EqualTo((320, 64)), "draw_text_outline(room_width/2, 64, ...)");
            Assert.That(VerdictScreen.ResultY, Is.EqualTo(120), "room_height/4");
            Assert.That(VerdictScreen.FastestY, Is.EqualTo(160), "room_height/3");
            var button = new VerdictScreen(VerdictKind.GameOver, "", "").Button;
            Assert.That((button.X, button.Y), Is.EqualTo((576, 448)));
            Assert.That((button.Sprite, button.OverSprite), Is.EqualTo(("spr_btn_right4", "spr_btn_right4_over")));
            Assert.That((button.LabelX, button.LabelY), Is.EqualTo((582, 442)), "draw_text_outline(x + 6, y - 6, ...)");
            // spr_btn_right4: origin (64, 32), bbox 1, 4 to 127, 63.
            Assert.That((button.Left, button.Top, button.Right, button.Bottom), Is.EqualTo((513, 420, 639, 479)));
            Assert.That(OriginalType.Title, Is.EqualTo(new TypeStyle("fnt_oldenglish", 2)), "obj_gameover's heading");
            Assert.That(OriginalType.Results, Is.EqualTo(new TypeStyle("fnt_oldenglish_small", 2)), "obj_victory's lines");
        }

        [Test]
        public void The_mouse_over_the_button_lights_it_and_a_press_on_it_clicks_once_and_chooses()
        {
            var screen = new VerdictScreen(VerdictKind.GameOver, "", "");
            var clicks = new List<MenuButtonKind>();
            screen.Clicked += b => clicks.Add(b.Kind);

            screen.PointAt(600, 450);
            Assert.That(screen.Hovered, Is.True);
            screen.PointAt(100, 100);
            Assert.That(screen.Hovered, Is.False);

            screen.Press(100, 100);
            Assert.That(screen.Choice, Is.EqualTo(VerdictChoice.None), "a press off the button does nothing");
            Assert.That(clicks, Is.Empty);

            screen.Press(600, 450);
            Assert.That(screen.Choice, Is.EqualTo(VerdictChoice.Retry));
            Assert.That(clicks, Is.EqualTo(new[] { MenuButtonKind.Retry }));

            screen.Press(600, 450);
            screen.Activate();
            Assert.That(clicks, Has.Count.EqualTo(1), "chosen once");
        }

        [Test]
        public void A_key_presses_the_one_button()
        {
            var screen = new VerdictScreen(VerdictKind.Victory, "", "");
            screen.Activate();
            Assert.That(screen.Choice, Is.EqualTo(VerdictChoice.Continue));
            Assert.That(screen.Hovered, Is.True);
        }
    }
}
