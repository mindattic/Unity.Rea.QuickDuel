using System.Collections;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    // The main menu (UQD-T45): the title over bg_splatter_scroll, New Game, Versus, the computer's difficulty and Quit,
    // each flicking under the pointer and clicking when pressed, and New Game and Versus fading into the duel with the
    // mode chosen. The menu's clock is held and stepped by hand, and the pointer is placed in room pixels.
    public class MainMenuTests : InputTestFixture
    {
        private MainMenuController menu;
        private double now;
        private Vector2? pointer;
        private string leftFor;
        private int quits;

        private const float PixelScale = 1080f / 480f;

        private IEnumerator Load(bool reallyLeave = false)
        {
            LoadingHandoff.Clear();
            DuelModeHandoff.Clear();
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null, "MainMenu scene has no MainMenuController.");
            now = menu.StartedAt;
            menu.Clock = () => now;
            pointer = null;
            menu.Pointer = () => pointer;
            quits = 0;
            menu.OnQuit = () => quits++;
            // The room ran on the real clock until now; start it over on the held one.
            menu.Begin();
            leftFor = null;
            if (!reallyLeave) menu.Leave = name => leftFor = name;
        }

        [TearDown]
        public override void TearDown()
        {
            LoadingHandoff.Clear();
            DuelModeHandoff.Clear();
            base.TearDown();
        }

        // Shows the frame of the room's `step`-th step (1 the first).
        private IEnumerator AtStep(long step)
        {
            now = menu.StartedAt + (step - 0.5) / MainMenu.TicksPerSecond;
            yield return null;
            yield return null;
        }

        private IEnumerator ClickAt(Mouse mouse, float x, float y)
        {
            pointer = new Vector2(x, y);
            yield return null;
            Press(mouse.leftButton);
            Release(mouse.leftButton);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheMenuComesAfterTheIntroAndCarriesTheOriginalsArtAndSounds()
        {
            yield return Load();
            Assert.That(SceneManager.GetActiveScene().buildIndex, Is.EqualTo(1), "intro, main menu, duel");
            Assert.That(SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/Duel.unity"), Is.EqualTo(2));
            Assert.That(menu.NextScene, Is.EqualTo("Duel"));
            Assert.That(menu.Background.name, Does.StartWith("bg_splatter_scroll"));
            Assert.That(menu.Title.name, Does.StartWith("spr_titlescreen"));
            Assert.That(menu.LoadingImages, Has.Count.EqualTo(MenuFade.Images));
            Assert.That(menu.ButtonSprites, Has.Count.EqualTo(5));
            for (var i = 0; i < 5; i++)
            {
                Assert.That(menu.ButtonSprites[i].name, Does.StartWith(menu.Menu.Buttons[i].Sprite + "_0"));
                Assert.That(menu.ButtonOverSprites[i].name, Does.StartWith(menu.Menu.Buttons[i].OverSprite));
            }
            Assert.That(menu.Flick.name, Is.EqualTo("sfx_flick"));
            Assert.That(menu.Click.name, Is.EqualTo("sfx_click"));
            Assert.That(menu.TitleElement.style.backgroundImage.value.sprite, Is.SameAs(menu.Title));
        }

        [UnityTest]
        public IEnumerator TheRoomOpensUnderTheFirstLoadingImageWithTheBloodScrollingBehindTheTitle()
        {
            yield return Load();
            yield return AtStep(1);
            Assert.That(menu.Overlay.style.backgroundImage.value.sprite, Is.SameAs(menu.LoadingImages[0]));
            Assert.That(menu.Overlay.style.opacity.value, Is.EqualTo(0.99f).Within(1e-5f));
            Assert.That(menu.Overlay.parent.IndexOf(menu.Overlay), Is.EqualTo(menu.Overlay.parent.childCount - 1), "over everything");
            Assert.That(menu.ScrollTiles[0].style.left.value.value, Is.EqualTo(-1 * PixelScale).Within(1e-3f));
            Assert.That(menu.ScrollTiles[1].style.left.value.value, Is.EqualTo((MainMenu.ScrollWidth - 1) * PixelScale).Within(1e-3f));
            Assert.That(menu.Room.IndexOf(menu.ScrollTiles[0]), Is.LessThan(menu.Room.IndexOf(menu.TitleElement)), "the title over the background");
            Assert.That(menu.Room.IndexOf(menu.TitleElement), Is.LessThan(menu.Room.IndexOf(menu.ButtonElements[0])), "the buttons over the title");

            yield return AtStep(150);
            Assert.That(menu.Overlay.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(menu.ScrollTiles[0].style.left.value.value, Is.EqualTo(-150 * PixelScale).Within(1e-3f));
            Screenshots.Capture("menu");
        }

        [UnityTest]
        public IEnumerator TheButtonsStandWhereTheRoomPlacedThemWithTheirLabels()
        {
            yield return Load();
            yield return AtStep(150);
            var newGame = menu.ButtonElements[0];
            Assert.That(newGame.style.left.value.value, Is.EqualTo((576 - 64) * PixelScale).Within(1e-3f));
            Assert.That(newGame.style.top.value.value, Is.EqualTo((320 - 32) * PixelScale).Within(1e-3f));
            Assert.That(newGame.style.width.value.value, Is.EqualTo(128 * PixelScale).Within(1e-3f));
            Assert.That(newGame.style.backgroundImage.value.sprite, Is.SameAs(menu.ButtonSprites[0]));
            Assert.That(menu.Labels[0].text, Is.EqualTo("New Game"));
            Assert.That(menu.Labels[0].Style, Is.EqualTo(OriginalType.Button), "fnt_basic outlined 1");
            Assert.That(menu.Labels[1].text, Is.EqualTo("Versus"));
            Assert.That(menu.Labels[2].text, Is.EqualTo("Normal"));
            Assert.That(menu.Labels[3].text, Is.EqualTo("Quit"));
            Assert.That(menu.Labels[4].text, Is.EqualTo("Credits"));
            Assert.That(menu.Labels[0].style.top.value.value, Is.EqualTo(314 * PixelScale).Within(1e-3f));
            Assert.That(menu.MessageText.text, Is.Empty);
            Assert.That(menu.MessageText.Style, Is.EqualTo(OriginalType.Message));
        }

        [UnityTest]
        public IEnumerator HoveringAButtonFlicksHighlightsItAndShowsItsMessage()
        {
            yield return Load();
            yield return AtStep(150);
            pointer = new Vector2(600, 330);
            yield return AtStep(151);
            Assert.That(menu.Played, Is.EqualTo(new[] { MainMenuController.FlickCue }));
            Assert.That(menu.ButtonElements[0].style.backgroundImage.value.sprite, Is.SameAs(menu.ButtonOverSprites[0]));
            Assert.That(menu.MessageText.text, Is.EqualTo("Start a new game"));
            Screenshots.Capture("menu-hover");

            pointer = new Vector2(605, 335);
            yield return AtStep(152);
            Assert.That(menu.Played, Has.Count.EqualTo(1), "once on entering");

            pointer = new Vector2(300, 200);
            yield return AtStep(153);
            Assert.That(menu.ButtonElements[0].style.backgroundImage.value.sprite, Is.SameAs(menu.ButtonSprites[0]));
            Assert.That(menu.MessageText.text, Is.Empty);
        }

        [UnityTest]
        public IEnumerator NewGameClicksAndFadesIntoTheDuelAgainstTheComputerAtTheChosenDifficulty()
        {
            yield return Load(reallyLeave: true);
            var mouse = InputSystem.AddDevice<Mouse>();
            yield return AtStep(150);
            yield return ClickAt(mouse, 600, 448);
            Assert.That(menu.Menu.Difficulty, Is.EqualTo(AiDifficulty.Hard));
            Assert.That(menu.Labels[2].text, Is.EqualTo("Hard"));
            Assert.That(menu.Played, Is.EqualTo(new[] { MainMenuController.FlickCue, MainMenuController.ClickCue }));

            yield return ClickAt(mouse, 600, 320);
            Assert.That(menu.Played, Is.EqualTo(new[] { MainMenuController.FlickCue, MainMenuController.ClickCue,
                MainMenuController.FlickCue, MainMenuController.ClickCue }));
            Assert.That(menu.Menu.Fade.Mode, Is.EqualTo(LoadingFadeMode.FadeIn));
            var image = menu.Menu.Fade.Image;

            yield return AtStep(200);
            Assert.That(menu.Overlay.style.backgroundImage.value.sprite, Is.SameAs(menu.LoadingImages[image]));
            Assert.That(menu.Overlay.style.opacity.value, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(menu.HasLeft, Is.False);

            yield return AtStep(400);
            Assert.That(menu.HasLeft, Is.True);
            for (var frame = 0; frame < 30 && SceneManager.GetActiveScene().name != "Duel"; frame++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Duel"));
            yield return null;

            var session = Object.FindAnyObjectByType<DuelSession>();
            Assert.That(session.Mode, Is.EqualTo(DuelMode.VsAiHard), "New Game plays the computer at the chosen difficulty");
            Assert.That(session.ModeText, Is.Empty, "the gamescreen drew no mode line (UQD-T67)");
            Assert.That(DuelModeHandoff.Pending, Is.Null, "taken once");
            Assert.That(DuelModeHandoff.Difficulty, Is.EqualTo(AiDifficulty.Hard), "kept for the menu's next visit");
            var fade = session.GetComponent<DuelFade>();
            Assert.That(fade.ArrivalImage, Is.EqualTo(image), "the duel opens under the image the menu faded in");
            Assert.That(LoadingHandoff.Pending, Is.Null);
        }

        [UnityTest]
        public IEnumerator VersusFadesIntoATwoPlayerDuel()
        {
            yield return Load();
            var mouse = InputSystem.AddDevice<Mouse>();
            yield return AtStep(150);
            yield return ClickAt(mouse, 600, 390);
            Assert.That(menu.Menu.Mode, Is.EqualTo(DuelMode.LocalPvp));
            yield return AtStep(400);
            Assert.That(leftFor, Is.EqualTo("Duel"));
            Assert.That(DuelModeHandoff.Pending, Is.EqualTo(DuelMode.LocalPvp));
            Assert.That(LoadingHandoff.Pending, Is.EqualTo(menu.Menu.Fade.Image));
        }

        [UnityTest]
        public IEnumerator QuitAndEscapeEndTheGame()
        {
            yield return Load();
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return AtStep(150);
            yield return ClickAt(mouse, 30, 440);
            Assert.That(quits, Is.EqualTo(1));
            Assert.That(menu.Played, Is.EqualTo(new[] { MainMenuController.FlickCue, MainMenuController.ClickCue }));

            yield return Load();
            yield return AtStep(10);
            Press(keyboard.escapeKey);
            Release(keyboard.escapeKey);
            yield return null;
            Assert.That(quits, Is.EqualTo(1), "obj_mainmenu's Escape");
            Assert.That(menu.Played, Is.Empty, "game_end() at once, no click");
        }

        // The credits (UQD-T47): the Credits button rolls the author's credit and credits.txt's arena photo credits up
        // the room over a shade, the buttons standing down, until a click or Escape closes them.
        [UnityTest]
        public IEnumerator CreditsRollsTheAuthorAndTheArenaPhotoCreditsUntilAClickOrEscapeClosesThem()
        {
            yield return Load();
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(menu.CreditsText, Is.Not.Null, "the scene carries the original's credits.txt");
            Assert.That(menu.CreditsText.text, Does.Contain("Dusty Junction"));
            Assert.That(menu.Author, Is.EqualTo("Ryan DeBraal"));
            yield return AtStep(150);
            Assert.That(menu.Credits.style.display.value, Is.EqualTo(DisplayStyle.None));
            var credits = menu.ButtonElements[4];
            Assert.That(credits.style.left.value.value, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(credits.style.top.value.value, Is.EqualTo((384 - 32) * PixelScale).Within(1e-3f));

            yield return ClickAt(mouse, 60, 384);
            Assert.That(menu.Menu.CreditsOpen, Is.True);
            Assert.That(menu.Played, Is.EqualTo(new[] { MainMenuController.FlickCue, MainMenuController.ClickCue }));
            var texts = new System.Collections.Generic.List<string>();
            foreach (var line in menu.CreditLines) texts.Add(line.text);
            Assert.That(texts, Does.Contain("Ryan DeBraal"));
            foreach (var name in new[] { "Dusty Junction", "The Fumaroles", "Sozza's Point", "Southern Ridge", "Arid Plains" })
                Assert.That(texts, Does.Contain(name));
            Assert.That(texts, Does.Contain("http://commons.wikimedia.org/wiki/File:Serengeti_-_plains.jpg"));
            Assert.That(menu.CreditLines[0].Style, Is.EqualTo(OriginalType.Banner));

            // Opened at the 151st step; 200 steps later the roll is 200 pixels up from the foot.
            yield return AtStep(351);
            Assert.That(menu.Credits.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(menu.ButtonElements[0].style.display.value, Is.EqualTo(DisplayStyle.None), "the buttons stand down");
            Assert.That(menu.TitleElement.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(menu.Room.IndexOf(menu.Credits), Is.GreaterThan(menu.Room.IndexOf(menu.ButtonElements[4])));
            var top = menu.Menu.Credits.Top;
            Assert.That(top, Is.EqualTo(MainMenu.RoomHeight - (int)menu.Menu.Credits.Steps));
            Assert.That(menu.CreditLines[2].style.top.value.value,
                Is.EqualTo((top + menu.Menu.Credits.Lines[2].Top) * PixelScale).Within(1e-3f));
            Screenshots.Capture("menu-credits");

            yield return ClickAt(mouse, 600, 320);
            Assert.That(menu.Menu.CreditsOpen, Is.False);
            Assert.That(menu.Menu.Chosen, Is.False, "the click that closes them presses nothing");
            Assert.That(menu.Credits.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(menu.ButtonElements[0].style.display.value, Is.EqualTo(DisplayStyle.Flex));

            menu.Menu.Press(60, 384);
            Press(keyboard.escapeKey);
            Release(keyboard.escapeKey);
            yield return null;
            Assert.That(menu.Menu.CreditsOpen, Is.False, "Escape closes them");
            Assert.That(quits, Is.Zero, "and does not end the game");
        }

        [UnityTest]
        public IEnumerator TheArrowKeysWalkTheButtonsAndEnterPressesTheHighlightedOne()
        {
            yield return Load();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return AtStep(150);
            Press(keyboard.downArrowKey);
            Release(keyboard.downArrowKey);
            Press(keyboard.downArrowKey);
            Release(keyboard.downArrowKey);
            yield return null;
            Assert.That(menu.Menu.Hovered.Kind, Is.EqualTo(MenuButtonKind.Versus));
            Assert.That(menu.Played, Is.EqualTo(new[] { MainMenuController.FlickCue, MainMenuController.FlickCue }));
            Press(keyboard.enterKey);
            Release(keyboard.enterKey);
            yield return null;
            Assert.That(menu.Menu.Chosen, Is.True);
            Assert.That(menu.Menu.Mode, Is.EqualTo(DuelMode.LocalPvp));
        }
    }
}
