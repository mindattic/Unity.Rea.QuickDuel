using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Unity.Rea.QuickDuel.Core.Tests
{
    // The main menu room (UQD-T45): obj_btn_newgame, _versus and _quit with their flick and click, the difficulty in the
    // Options slot, bg_splatter_scroll's scroll and obj_fade_mainmenu's fade, stepped at the original's 60 a second.
    public class MainMenuTests
    {
        private static MainMenu Menu(int image = 0, AiDifficulty difficulty = AiDifficulty.Normal, int seed = 1) =>
            new MainMenu(image, difficulty, new Random(seed));

        [Test]
        public void The_buttons_stand_where_the_room_placed_them_with_the_originals_labels_and_messages()
        {
            var menu = Menu();
            Assert.That(menu.Buttons.Select(b => b.Kind), Is.EqualTo(new[]
                { MenuButtonKind.NewGame, MenuButtonKind.Versus, MenuButtonKind.Difficulty, MenuButtonKind.Quit, MenuButtonKind.Credits }));
            Assert.That(menu.Buttons.Select(b => (b.X, b.Y)), Is.EqualTo(new[] { (576, 320), (576, 384), (576, 448), (64, 448), (64, 384) }));
            Assert.That(menu.Buttons.Select(b => b.Sprite), Is.EqualTo(new[] { "spr_btn_right1", "spr_btn_right2", "spr_btn_right3", "spr_btn_left4", "spr_btn_left1" }));
            Assert.That(menu.Buttons.Select(b => b.OverSprite), Is.EqualTo(new[] { "spr_btn_right1_over", "spr_btn_right2_over", "spr_btn_right3_over", "spr_btn_left4_over", "spr_btn_left1_over" }));
            Assert.That(menu.Buttons.Select(b => b.Label), Is.EqualTo(new[] { "New Game", "Versus", "Normal", "Quit", "Credits" }));
            Assert.That(menu.Button(MenuButtonKind.Credits).Message, Is.EqualTo("Who made what?"));
            Assert.That(menu.Button(MenuButtonKind.NewGame).Message, Is.EqualTo("Start a new game"));
            Assert.That(menu.Button(MenuButtonKind.Versus).Message, Is.EqualTo("Player vs. Player"));
            Assert.That(menu.Button(MenuButtonKind.Quit).Message, Is.EqualTo("Quit the game"));

            var newGame = menu.Button(MenuButtonKind.NewGame);
            Assert.That((newGame.LabelX, newGame.LabelY), Is.EqualTo((582, 314)), "draw_text_outline(x + 6, y - 6, ...)");
            var quit = menu.Button(MenuButtonKind.Quit);
            Assert.That((quit.LabelX, quit.LabelY), Is.EqualTo((58, 442)), "draw_text_outline(x - 6, y - 6, ...)");
            Assert.That((MainMenu.MessageX, MainMenu.MessageY), Is.EqualTo((320, 450)), "(room_width / 2, room_height - 30)");
        }

        [Test]
        public void A_button_is_under_the_mouse_inside_its_sprites_bounding_box_placed_on_the_instance()
        {
            var newGame = Menu().Button(MenuButtonKind.NewGame);
            // spr_btn_right1: origin (64, 32), bbox 6, 3 to 127, 63, at (576, 320).
            Assert.That((newGame.Left, newGame.Top, newGame.Right, newGame.Bottom), Is.EqualTo((518, 291, 639, 351)));
            Assert.That(newGame.Contains(518, 291), Is.True);
            Assert.That(newGame.Contains(639.9, 351.9), Is.True, "the whole last pixel");
            Assert.That(newGame.Contains(517.9, 300), Is.False);
            Assert.That(newGame.Contains(600, 352), Is.False);
            var quit = Menu().Button(MenuButtonKind.Quit);
            Assert.That((quit.Left, quit.Top, quit.Right, quit.Bottom), Is.EqualTo((0, 419, 122, 476)));
            // spr_btn_left1: bbox 0, 2 to 127, 58, at (64, 384).
            var credits = Menu().Button(MenuButtonKind.Credits);
            Assert.That((credits.Left, credits.Top, credits.Right, credits.Bottom), Is.EqualTo((0, 354, 127, 410)));
        }

        [Test]
        public void Moving_onto_a_button_flicks_once_and_shows_its_message_and_moving_off_clears_it()
        {
            var menu = Menu();
            var flicks = new List<MenuButtonKind>();
            menu.Flicked += b => flicks.Add(b.Kind);

            menu.PointAt(300, 200);
            Assert.That(menu.Hovered, Is.Null);
            Assert.That(menu.Message, Is.Empty);

            menu.PointAt(560, 320);
            menu.PointAt(570, 330);
            Assert.That(menu.Hovered.Kind, Is.EqualTo(MenuButtonKind.NewGame));
            Assert.That(menu.Message, Is.EqualTo("Start a new game"));
            Assert.That(flicks, Is.EqualTo(new[] { MenuButtonKind.NewGame }), "mouse enter, not every move");

            menu.PointAt(560, 390);
            Assert.That(flicks, Is.EqualTo(new[] { MenuButtonKind.NewGame, MenuButtonKind.Versus }));
            menu.PointAt(300, 200);
            Assert.That(menu.Hovered, Is.Null, "mouse leave: showmsg = false");
            Assert.That(flicks, Has.Count.EqualTo(2), "leaving is silent");
        }

        [Test]
        public void The_opening_fade_runs_out_and_chooses_the_image_the_menu_will_leave_under()
        {
            var menu = Menu(image: 5);
            menu.Step();
            Assert.That(menu.Fade.Image, Is.EqualTo(5));
            Assert.That(menu.Fade.Alpha, Is.EqualTo(0.99).Within(1e-9));
            menu.RunTo(100);
            Assert.That(menu.Fade.Mode, Is.EqualTo(LoadingFadeMode.FadeOut));
            menu.RunTo(101);
            Assert.That(menu.Fade.Mode, Is.EqualTo(LoadingFadeMode.Off), "the step after the alpha runs out");
            Assert.That(menu.Fade.Image, Is.EqualTo(5));

            var images = new HashSet<int>();
            for (var seed = 0; seed < 200; seed++)
            {
                var other = Menu(image: 5, seed: seed);
                other.RunTo(102);
                Assert.That(other.Fade.Alpha, Is.Zero);
                images.Add(other.Fade.Image);
            }
            Assert.That(images, Has.Count.EqualTo(MenuFade.Images), "irandom_range(1, 14) as \"off\" starts");
        }

        [Test]
        public void New_game_clicks_and_fades_the_chosen_image_in_then_plays_the_computer()
        {
            var menu = Menu(difficulty: AiDifficulty.Hard);
            var clicks = new List<MenuButtonKind>();
            menu.Clicked += b => clicks.Add(b.Kind);
            menu.RunTo(150);
            var image = menu.Fade.Image;

            menu.Press(600, 320);
            Assert.That(clicks, Is.EqualTo(new[] { MenuButtonKind.NewGame }));
            Assert.That(menu.Chosen, Is.True);
            Assert.That(menu.Mode, Is.EqualTo(DuelMode.VsAiHard));
            Assert.That(menu.Fade.Mode, Is.EqualTo(LoadingFadeMode.FadeIn));

            menu.RunTo(200);
            Assert.That(menu.Fade.Alpha, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(menu.Fade.Image, Is.EqualTo(image), "the image \"off\" chose");
            menu.RunTo(250);
            Assert.That(menu.Finished, Is.False);
            menu.RunTo(251);
            Assert.That(menu.Finished, Is.True, "full after 100 steps, the room left on the 101st");
            Assert.That(menu.Steps, Is.EqualTo(251));

            menu.Press(600, 384);
            Assert.That(clicks, Has.Count.EqualTo(1), "nothing once the room is left");
        }

        [Test]
        public void Versus_plays_two_players_and_the_last_of_the_two_pressed_wins()
        {
            var menu = Menu();
            menu.RunTo(150);
            menu.Press(600, 384);
            Assert.That(menu.Mode, Is.EqualTo(DuelMode.LocalPvp));
            menu.RunTo(160);
            menu.Press(600, 320);
            Assert.That(menu.Mode, Is.EqualTo(DuelMode.VsAiNormal), "is_singleplayer as it was last set");
            menu.RunTo(160 + 90);
            Assert.That(menu.Fade.Alpha, Is.EqualTo(1.0).Within(1e-9), "the fade went on without starting over");
        }

        [Test]
        public void A_button_pressed_during_the_opening_fade_turns_it_round()
        {
            var menu = Menu(image: 3);
            menu.RunTo(30);
            Assert.That(menu.Fade.Alpha, Is.EqualTo(0.70).Within(1e-9));
            menu.Press(600, 384);
            menu.RunTo(31);
            Assert.That(menu.Fade.Alpha, Is.EqualTo(0.71).Within(1e-9), "firstPass was spent: no restart from nothing");
            Assert.That(menu.Fade.Image, Is.EqualTo(3), "the image it opened under fades back in");
            menu.RunTo(70);
            Assert.That(menu.Finished, Is.True);
            Assert.That(menu.Steps, Is.InRange(60, 62), "30 steps back up from 0.70 and the room left after");
        }

        [Test]
        public void A_button_pressed_between_the_fade_out_and_off_fades_the_opening_image_in_from_nothing()
        {
            var menu = Menu(image: 3);
            menu.RunTo(101);
            Assert.That(menu.Fade.Mode, Is.EqualTo(LoadingFadeMode.Off));
            menu.Press(600, 320);
            menu.RunTo(102);
            Assert.That(menu.Fade.Alpha, Is.EqualTo(0.01).Within(1e-9));
            Assert.That(menu.Fade.Image, Is.EqualTo(3));
        }

        [Test]
        public void The_difficulty_button_steps_easy_normal_hard_without_leaving()
        {
            var menu = Menu(difficulty: AiDifficulty.Normal);
            var clicks = 0;
            menu.Clicked += _ => clicks++;
            var button = menu.Button(MenuButtonKind.Difficulty);
            menu.Press(600, 448);
            Assert.That(menu.Difficulty, Is.EqualTo(AiDifficulty.Hard));
            Assert.That(button.Label, Is.EqualTo("Hard"));
            Assert.That(menu.Message, Does.Contain("Hard"));
            menu.Press(600, 448);
            menu.Press(600, 448);
            Assert.That(menu.Difficulty, Is.EqualTo(AiDifficulty.Normal));
            Assert.That(clicks, Is.EqualTo(3));
            Assert.That(menu.Chosen, Is.False);
            Assert.That(menu.Fade.Mode, Is.Not.EqualTo(LoadingFadeMode.FadeIn));
            menu.Press(600, 320);
            Assert.That(menu.Mode, Is.EqualTo(DuelMode.VsAiNormal));
        }

        [Test]
        public void Quit_and_escape_end_the_game_and_a_press_off_every_button_does_nothing()
        {
            var menu = Menu();
            var quits = 0;
            var clicks = 0;
            menu.Quit += () => quits++;
            menu.Clicked += _ => clicks++;
            menu.Press(300, 200);
            Assert.That((quits, clicks), Is.EqualTo((0, 0)));

            menu.Press(64, 448);
            Assert.That(menu.QuitRequested, Is.True);
            Assert.That((quits, clicks), Is.EqualTo((1, 1)), "the click, then game_end()");

            var escaped = Menu();
            escaped.Quit += () => quits++;
            escaped.Cancel();
            Assert.That(escaped.QuitRequested, Is.True);
            Assert.That(quits, Is.EqualTo(2));
        }

        [Test]
        public void A_keyboard_or_pad_walks_the_buttons_in_order_and_presses_the_highlighted_one()
        {
            var menu = Menu();
            var flicks = new List<MenuButtonKind>();
            menu.Flicked += b => flicks.Add(b.Kind);
            menu.Activate();
            Assert.That(menu.Chosen, Is.False, "nothing highlighted, nothing pressed");

            menu.FocusNext();
            menu.FocusNext();
            menu.FocusPrevious();
            menu.FocusPrevious();
            Assert.That(flicks, Is.EqualTo(new[] { MenuButtonKind.NewGame, MenuButtonKind.Versus, MenuButtonKind.NewGame, MenuButtonKind.Credits }));
            menu.FocusNext();
            menu.FocusNext();
            menu.Activate();
            Assert.That(menu.Mode, Is.EqualTo(DuelMode.LocalPvp));
            Assert.That(menu.Chosen, Is.True);
        }

        [Test]
        public void The_background_scrolls_left_a_pixel_a_step_and_wraps_at_its_width()
        {
            var menu = Menu();
            Assert.That(menu.ScrollX, Is.Zero);
            menu.Step();
            Assert.That(menu.ScrollX, Is.EqualTo(-1));
            menu.RunTo(MainMenu.ScrollWidth - 1);
            Assert.That(menu.ScrollX, Is.EqualTo(-(MainMenu.ScrollWidth - 1)));
            menu.Step();
            Assert.That(menu.ScrollX, Is.Zero);
        }

        [Test]
        public void New_game_plays_the_computer_at_each_difficulty()
        {
            Assert.That(MatchSession.VsAi(AiDifficulty.Easy), Is.EqualTo(DuelMode.VsAiEasy));
            Assert.That(MatchSession.VsAi(AiDifficulty.Normal), Is.EqualTo(DuelMode.VsAiNormal));
            Assert.That(MatchSession.VsAi(AiDifficulty.Hard), Is.EqualTo(DuelMode.VsAiHard));
            foreach (AiDifficulty d in Enum.GetValues(typeof(AiDifficulty)))
                Assert.That(MatchSession.DifficultyOf(MatchSession.VsAi(d)), Is.EqualTo(d));
        }
    }
}
