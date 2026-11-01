using System;
using System.Collections.Generic;

namespace Unity.Rea.QuickDuel.Core
{
    // The main menu's buttons (UQD-T45): the original's New Game, Versus and Quit, the computer's difficulty, which
    // stands in the slot of the original's empty Options button, and Credits (UQD-T47).
    // Retry and Continue are the verdict screen's (UQD-T48, VerdictScreen).
    public enum MenuButtonKind { NewGame, Versus, Difficulty, Quit, Credits, Retry, Continue }

    // One of the menu's buttons as the original placed and drew it: the instance's room position (the sprite's origin,
    // 64, 32, sits on it), its sprite and the "_over" sprite drawn while the mouse is over it, its outlined label and the
    // message mouseover_message_draw shows for it at the foot of the room.
    public sealed class MenuButton
    {
        // Every button sprite is 128 x 64 with its origin at (64, 32).
        public const int Width = 128;
        public const int Height = 64;
        public const int OriginX = 64;
        public const int OriginY = 32;

        // draw_text_outline(x + 6, y - 6, ...) on the right-hand buttons, x - 6 on the left-hand ones.
        public const int LabelOffset = 6;

        internal MenuButton(MenuButtonKind kind, int x, int y, string sprite, bool rightHand, string label, string message,
            int bboxLeft, int bboxTop, int bboxRight, int bboxBottom)
        {
            Kind = kind;
            X = x;
            Y = y;
            Sprite = sprite;
            OverSprite = sprite + "_over";
            RightHand = rightHand;
            Label = label;
            Message = message;
            Left = x - OriginX + bboxLeft;
            Top = y - OriginY + bboxTop;
            Right = x - OriginX + bboxRight;
            Bottom = y - OriginY + bboxBottom;
        }

        public MenuButtonKind Kind { get; }

        // The instance's room position.
        public int X { get; }
        public int Y { get; }

        // spr_btn_right1 and so on, and its "_over" twin.
        public string Sprite { get; }
        public string OverSprite { get; }

        // A right-hand button (the right column) draws its label 6 px right of x; a left-hand one 6 px left.
        public bool RightHand { get; }

        // The label, centred on LabelX with its top at LabelY, in fnt_basic outlined 1.
        public string Label { get; internal set; }
        public int LabelX => RightHand ? X + LabelOffset : X - LabelOffset;
        public int LabelY => Y - LabelOffset;

        // mouseover_message_draw's message while the mouse is over the button.
        public string Message { get; internal set; }

        // The room box the mouse must be in, edges included: the sprite's rectangle mask (its .sprite.gmx bbox_*, which is
        // the bounds of its opaque pixels) placed on the instance.
        public int Left { get; }
        public int Top { get; }
        public int Right { get; }
        public int Bottom { get; }

        // Whether the mouse at room position (x, y) is over the button: GameMaker tests the whole pixel the mouse is on.
        public bool Contains(double x, double y)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return false;
            var px = Math.Floor(x);
            var py = Math.Floor(y);
            return px >= Left && px <= Right && py >= Top && py <= Bottom;
        }
    }

    // The main menu room (UQD-T45), stepped at the original's 60 a second. Every figure is the original's own, read from
    // BushidoCoda.gmx, not a tuning choice:
    //
    //   mainmenu.room       640 x 480 at 60 fps; bg_splatter_scroll (1279 x 480) as its background, tiled both ways
    //                       and scrolling left a pixel a step (hspeed -1); obj_title, the six buttons, obj_mainmenu,
    //                       obj_mouse and obj_fade_mainmenu
    //   obj_title           spr_titlescreen (640 x 480, black, the logo cut out of it so the blood scrolls through) at
    //                       (0, 0), depth 100
    //   obj_btn_newgame     spr_btn_right1 at (576, 320): "New Game", "Start a new game"; is_singleplayer = true
    //   obj_btn_versus      spr_btn_right2 at (576, 384): "Versus", "Player vs. Player"; is_singleplayer = false
    //   obj_btn_quit        spr_btn_left4 at (64, 448): "Quit", "Quit the game"; game_end()
    //   obj_btn_credits     spr_btn_left1 at (64, 384): "Credits", "Who made what?"; its click went nowhere (its
    //                       room_goto commented out and no credits room made): fixed, it opens the credits (UQD-T47)
    //   obj_btn_*           Mouse enter: showmsg = true; audio_play_sound(sfx_flick). Mouse leave: showmsg = false.
    //                       Left pressed: audio_play_sound(sfx_click), then the button's own action; New Game and Versus
    //                       set obj_fade_mainmenu.mode = "fadein" (MenuFade), which enters the duel once it is full.
    //                       Draw: the "_over" sprite while showmsg, else the sprite; the label in fnt_basic, white,
    //                       outlined 1, centred on x + 6 (x - 6 on the left) with its top at y - 6; and, while showmsg,
    //                       mouseover_message_draw: the message in fnt_basic, white, not outlined, centred on
    //                       (room_width / 2, room_height - 30)
    //   obj_mainmenu        Escape: game_end()
    //
    // Options (spr_btn_right3 at (576, 448)) and Scores were empty stubs; the Options slot holds the computer's difficulty
    // instead, which the original never had: clicking it plays the click and steps Easy -> Normal -> Hard -> Easy, and
    // New Game plays the computer at it (BIBLE).
    //
    // Credits opens the credits screen (CreditsRoll) over the room: the buttons stand down, and a click, a press of
    // Enter, Space or a pad's South or Start, or Escape closes it again with the click (BIBLE).
    //
    // Mouse events come before a step's Step events in GameMaker, and buttons do not move, so a button is entered or
    // left the moment the pointer moves on or off it, and a press acts at once; the fade it starts moves on the next step.
    public sealed class MainMenu
    {
        public const int TicksPerSecond = 60;

        public const int RoomWidth = 640;
        public const int RoomHeight = 480;

        // bg_splatter_scroll and its hspeed.
        public const int ScrollWidth = 1279;
        public const int ScrollHeight = 480;
        public const int ScrollSpeed = -1;

        // mouseover_message_draw: centred on (room_width / 2, room_height - 30).
        public const int MessageX = RoomWidth / 2;
        public const int MessageY = RoomHeight - 30;

        private readonly Random random;
        private readonly List<MenuButton> buttons = new List<MenuButton>();
        private bool singleplayer = true;

        // The room as it is created: nothing under the mouse, and the fade at `openingImage` (the loading image the intro
        // faded in, the global fade_asset) full and about to fade out. `difficulty` is the computer's to start with;
        // `credits` is what the Credits button shows (the author's credit alone when none is given).
        public MainMenu(int openingImage, AiDifficulty difficulty, Random random, CreditsRoll credits = null)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            Fade = new MenuFade(openingImage);
            Difficulty = difficulty;
            Credits = credits ?? new CreditsRoll(DefaultAuthor, Array.Empty<CreditsEntry>());
            buttons.Add(new MenuButton(MenuButtonKind.NewGame, 576, 320, "spr_btn_right1", true, "New Game", "Start a new game", 6, 3, 127, 63));
            buttons.Add(new MenuButton(MenuButtonKind.Versus, 576, 384, "spr_btn_right2", true, "Versus", "Player vs. Player", 7, 5, 127, 63));
            buttons.Add(new MenuButton(MenuButtonKind.Difficulty, 576, 448, "spr_btn_right3", true, "", "", 4, 4, 127, 63));
            buttons.Add(new MenuButton(MenuButtonKind.Quit, 64, 448, "spr_btn_left4", false, "Quit", "Quit the game", 0, 3, 122, 60));
            buttons.Add(new MenuButton(MenuButtonKind.Credits, 64, 384, "spr_btn_left1", false, "Credits", "Who made what?", 0, 2, 127, 58));
            ShowDifficulty();
        }

        // The original project's option_author, for a menu made without the shipped credits.
        public const string DefaultAuthor = "Ryan DeBraal";

        // The buttons in the room's instance order.
        public IReadOnlyList<MenuButton> Buttons => buttons;

        public MenuButton Button(MenuButtonKind kind) => buttons.Find(b => b.Kind == kind);

        // The button the mouse is over (its showmsg), if any.
        public MenuButton Hovered { get; private set; }

        // mouseover_message_draw's text, or nothing.
        public string Message => Hovered?.Message ?? "";

        // The steps run so far.
        public long Steps { get; private set; }

        // Where bg_splatter_scroll's first tile is drawn, in room pixels: a pixel further left each step, wrapping.
        public int ScrollX => -(int)(Steps % ScrollWidth);

        // obj_fade_mainmenu.
        public MenuFade Fade { get; }

        // The computer's difficulty for New Game.
        public AiDifficulty Difficulty { get; private set; }

        // New Game or Versus was pressed: the fade to the duel is under way.
        public bool Chosen { get; private set; }

        // is_singleplayer as the buttons last left it, turned into the duel's mode: New Game plays the computer at the
        // chosen difficulty, Versus two players.
        public DuelMode Mode => singleplayer ? MatchSession.VsAi(Difficulty) : DuelMode.LocalPvp;

        // The fade in is full: room_goto(gamescreen).
        public bool Finished => Fade.Finished;

        // What the Credits button shows.
        public CreditsRoll Credits { get; }

        // The credits screen is up: the buttons stand down until it is closed.
        public bool CreditsOpen { get; private set; }

        // game_end(): the Quit button or Escape.
        public bool QuitRequested { get; private set; }

        // The mouse entered a button: sfx_flick.
        public event Action<MenuButton> Flicked;

        // A button was pressed: sfx_click.
        public event Action<MenuButton> Clicked;

        // game_end().
        public event Action Quit;

        // The mouse is at room position (x, y): the button under it shows its "_over" sprite and message, and one it has
        // just come onto flicks.
        public void PointAt(double x, double y)
        {
            if (Finished || CreditsOpen) return;
            MenuButton under = null;
            foreach (var button in buttons)
                if (button.Contains(x, y))
                {
                    under = button;
                    break;
                }
            Hover(under);
        }

        // The left button went down with the mouse at room position (x, y), as a touch does too: the button there is
        // entered, if it was not already, and pressed. Over the credits, a press anywhere closes them.
        public void Press(double x, double y)
        {
            if (CreditsOpen)
            {
                CloseCredits();
                return;
            }
            PointAt(x, y);
            Activate();
        }

        // A keyboard or pad moves the highlight to the next or previous button, as if the mouse had moved onto it.
        public void FocusNext() => FocusBy(1);
        public void FocusPrevious() => FocusBy(-1);

        // A keyboard or pad presses the highlighted button; nothing if none is highlighted. Over the credits, it closes
        // them.
        public void Activate()
        {
            if (CreditsOpen)
            {
                CloseCredits();
                return;
            }
            if (Finished || Hovered == null) return;
            var button = Hovered;
            Clicked?.Invoke(button);
            switch (button.Kind)
            {
                case MenuButtonKind.NewGame:
                    singleplayer = true;
                    Chosen = true;
                    Fade.FadeIn();
                    break;
                case MenuButtonKind.Versus:
                    singleplayer = false;
                    Chosen = true;
                    Fade.FadeIn();
                    break;
                case MenuButtonKind.Difficulty:
                    Difficulty = (AiDifficulty)(((int)Difficulty + 1) % 3);
                    ShowDifficulty();
                    break;
                case MenuButtonKind.Credits:
                    // Nothing stays lit, so the menu comes back with no button under the pointer until it moves.
                    Hovered = null;
                    Credits.Restart();
                    CreditsOpen = true;
                    break;
                default:
                    RequestQuit();
                    break;
            }
        }

        // obj_mainmenu's Escape; over the credits, it closes them instead.
        public void Cancel()
        {
            if (Finished) return;
            if (CreditsOpen)
            {
                CloseCredits();
                return;
            }
            RequestQuit();
        }

        // Runs steps until `steps` have run.
        public void RunTo(long steps)
        {
            while (Steps < steps && !Finished)
                Step();
        }

        // One 60 a second step: the fade fades, then the background moves on, and the credits roll while they are up.
        public void Step()
        {
            if (Finished) return;
            Steps++;
            Fade.Step(random);
            if (CreditsOpen) Credits.Step();
        }

        private void Hover(MenuButton button)
        {
            if (button == Hovered) return;
            Hovered = button;
            if (button != null) Flicked?.Invoke(button);
        }

        private void CloseCredits()
        {
            CreditsOpen = false;
            Clicked?.Invoke(Button(MenuButtonKind.Credits));
        }

        private void FocusBy(int by)
        {
            if (Finished || CreditsOpen) return;
            var index = Hovered == null ? (by > 0 ? -1 : 0) : buttons.IndexOf(Hovered);
            Hover(buttons[((index + by) % buttons.Count + buttons.Count) % buttons.Count]);
        }

        private void RequestQuit()
        {
            if (QuitRequested) return;
            QuitRequested = true;
            Quit?.Invoke();
        }

        private void ShowDifficulty()
        {
            var button = Button(MenuButtonKind.Difficulty);
            button.Label = Difficulty.ToString();
            button.Message = $"New Game plays the computer on {Difficulty}";
        }
    }
}
