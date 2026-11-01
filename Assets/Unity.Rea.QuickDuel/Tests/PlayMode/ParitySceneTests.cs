using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    // One drawn-on-screen check per parity scene (UQD-T51): the intro, the main menu, its credits, the duel and
    // the verdict are each loaded the way the player reaches them, drawn through their panel into a 1440 x 1080
    // texture (the original's room at the game's own 3x scale), and must hold a real picture, not a blank or
    // single-colour frame. With QUICKDUEL_SHOTS set the pictures are saved as parity-<scene>.png; the committed
    // evidence is in docs/screenshots.
    public class ParitySceneTests
    {
        private const int Width = 1440;
        private const int Height = 1080;

        private RenderTexture target;
        private PanelSettings clone;

        [TearDown]
        public void Release()
        {
            LoadingHandoff.Clear();
            DuelModeHandoff.Clear();
            if (clone != null)
            {
                clone.targetTexture = null;
                Object.Destroy(clone);
                clone = null;
            }
            if (target != null)
            {
                target.Release();
                Object.Destroy(target);
                target = null;
            }
        }

        private void DrawInto(Component scene)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                // A skipped run would pass with nothing drawn, so a missing device fails unless opted out.
                if (System.Environment.GetEnvironmentVariable("QUICKDUEL_ALLOW_NO_GRAPHICS") == "1")
                    Assert.Ignore("No graphics device to draw with (QUICKDUEL_ALLOW_NO_GRAPHICS=1).");
                Assert.Fail("No graphics device to draw with: run the PlayMode tests without -nographics (or set QUICKDUEL_ALLOW_NO_GRAPHICS=1).");
            }
            var document = scene.GetComponentInChildren<UIDocument>();
            Assert.That(document, Is.Not.Null, "the scene draws no UI document");
            target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            clone = Object.Instantiate(document.panelSettings);
            clone.targetTexture = target;
            document.panelSettings = clone;
        }

        private Texture2D Capture(string name)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            Screenshots.Save("parity-" + name, pixels);
            return pixels;
        }

        // A picture, not a blank: a tenth or more of the frame is lit (the menu is white and red on black) and it
        // holds many different colours.
        private static void AssertDrawn(Texture2D pixels, string scene)
        {
            var all = pixels.GetPixels32();
            var lit = all.Count(p => p.r + p.g + p.b > 30);
            var colours = all.Select(p => (p.r >> 4) << 8 | (p.g >> 4) << 4 | (p.b >> 4)).Distinct().Count();
            Assert.That(lit, Is.GreaterThan(all.Length / 10), $"{scene}: only {lit} of {all.Length} pixels are lit");
            Assert.That(colours, Is.GreaterThan(12), $"{scene}: only {colours} distinct colours");
        }

        private IEnumerator Frames(int count = 3)
        {
            for (var i = 0; i < count; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator TheIntroIsDrawn()
        {
            LoadingHandoff.Clear();
            yield return SceneManager.LoadSceneAsync("Intro", LoadSceneMode.Single);
            yield return null;
            var intro = Object.FindAnyObjectByType<IntroController>();
            Assert.That(intro, Is.Not.Null);
            var now = intro.StartedAt;
            intro.Clock = () => now;
            intro.Begin();
            intro.Leave = _ => { };
            DrawInto(intro);

            now = intro.StartedAt + (IntroScene.StepTyping(intro.Story.Length) - 0.5) / IntroScene.TicksPerSecond;
            yield return Frames();
            var pixels = Capture("intro");
            AssertDrawn(pixels, "intro");
            Assert.That(intro.Text.text, Is.EqualTo(intro.Story), "the whole story is typed");
            Object.Destroy(pixels);
        }

        private IEnumerator LoadMenu()
        {
            LoadingHandoff.Clear();
            DuelModeHandoff.Clear();
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheMainMenuIsDrawn()
        {
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            var now = menu.StartedAt;
            menu.Clock = () => now;
            menu.Pointer = () => null;
            menu.Leave = _ => { };
            menu.Begin();
            DrawInto(menu);

            now = menu.StartedAt + (150 - 0.5) / MainMenu.TicksPerSecond;
            yield return Frames();
            var pixels = Capture("menu");
            AssertDrawn(pixels, "menu");
            Assert.That(menu.ButtonElements.All(b => b.style.display.value == DisplayStyle.Flex), Is.True, "every button is up");
            Object.Destroy(pixels);
        }

        [UnityTest]
        public IEnumerator TheCreditsAreDrawn()
        {
            yield return LoadMenu();
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            var now = menu.StartedAt;
            menu.Clock = () => now;
            menu.Pointer = () => null;
            menu.Leave = _ => { };
            menu.Begin();
            DrawInto(menu);

            now = menu.StartedAt + (150 - 0.5) / MainMenu.TicksPerSecond;
            yield return Frames();
            menu.Menu.Press(60, 384);
            now = menu.StartedAt + (350 - 0.5) / MainMenu.TicksPerSecond;
            yield return Frames();
            Assert.That(menu.Menu.CreditsOpen, Is.True);
            Assert.That(menu.Credits.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            var pixels = Capture("credits");
            AssertDrawn(pixels, "credits");
            Object.Destroy(pixels);
        }

        [UnityTest]
        public IEnumerator TheDuelIsDrawn()
        {
            DuelModeHandoff.Clear();
            LoadingHandoff.Clear();
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelController>();
            Assert.That(controller, Is.Not.Null);
            controller.Settings = DuelSceneTests.FastSettings();
            DrawInto(controller);

            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            var cue = controller.CueAt;
            controller.Strike(Side.Left, cue + 0.18);
            controller.Strike(Side.Right, cue + 0.25);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Resolved));
            yield return Frames(10);
            var pixels = Capture("duel");
            AssertDrawn(pixels, "duel");
            Object.Destroy(pixels);
        }

        [UnityTest]
        public IEnumerator TheVerdictIsDrawn()
        {
            DuelModeHandoff.Clear();
            LoadingHandoff.Clear();
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            var controller = Object.FindAnyObjectByType<DuelController>();
            Assert.That(controller, Is.Not.Null, "Duel scene has no DuelController.");
            var verdict = controller.GetComponent<DuelVerdict>();
            Assert.That(verdict, Is.Not.Null);
            verdict.Leave = _ => { };
            verdict.Pointer = () => null;
            DrawInto(controller);

            verdict.Show(new VerdictScreen(VerdictKind.Victory, "Left takes the match 2-1", "Fastest Reaction  212 ms (Left)"));
            yield return Frames();
            Assert.That(verdict.Shown, Is.True);
            var pixels = Capture("verdict");
            AssertDrawn(pixels, "verdict");
            Object.Destroy(pixels);
        }
    }
}
