using System.Collections;
using System.Linq;
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
    // The intro (UQD-T44): bg_mapscreen under twelve drifting clouds, the story typed out a character every 4 steps, a
    // loading image faded in and the next scene entered under it; a click or a key starts that fade at once. The intro's
    // clock is held and stepped by hand from the moment the scene started, so each step is looked at on its own.
    public class IntroTests : InputTestFixture
    {
        private IntroController intro;
        private double now;
        private string leftFor;

        private IEnumerator Load(bool reallyLeave = false)
        {
            LoadingHandoff.Clear();
            yield return SceneManager.LoadSceneAsync("Intro", LoadSceneMode.Single);
            yield return null;
            intro = Object.FindAnyObjectByType<IntroController>();
            Assert.That(intro, Is.Not.Null, "Intro scene has no IntroController.");
            now = intro.StartedAt;
            intro.Clock = () => now;
            // The room ran on the real clock until now; start it over on the held one.
            intro.Begin();
            leftFor = null;
            if (!reallyLeave) intro.Leave = name => leftFor = name;
        }

        [TearDown]
        public override void TearDown()
        {
            LoadingHandoff.Clear();
            base.TearDown();
        }

        // Shows the frame of the room's `step`-th step (1 the first).
        private IEnumerator AtStep(long step)
        {
            now = intro.StartedAt + (step - 0.5) / IntroScene.TicksPerSecond;
            yield return null;
            yield return null;
        }

        private const float PixelScale = 1080f / 480f;

        [UnityTest]
        public IEnumerator TheIntroIsTheGamesFirstSceneAndCarriesTheOriginalsArtAndStory()
        {
            yield return Load();
            Assert.That(SceneManager.GetActiveScene().buildIndex, Is.Zero, "the game opens on the intro");
            Assert.That(intro.NextScene, Is.EqualTo("MainMenu"), "the intro leads to the main menu (UQD-T45)");
            Assert.That(intro.Background.name, Does.StartWith("bg_mapscreen"));
            Assert.That(intro.Clouds.Select(c => c.name.Substring(0, 10)), Is.EqualTo(new[] { "spr_cloud1", "spr_cloud2", "spr_cloud3", "spr_cloud4" }));
            Assert.That(intro.LoadingImages, Has.Count.EqualTo(LoadingFade.Images));
            Assert.That(intro.LoadingImages[0].name, Does.StartWith("spr_loading1"));
            Assert.That(intro.Story, Does.StartWith("For centuries the empire had been fragmented.\n"));
            Assert.That(intro.Story, Does.Contain("A young boy was crowned the Enlightened Ruler."));
            Assert.That(intro.Story, Does.EndWith("The Bushido Coda."));
            Assert.That(intro.Backdrop.style.backgroundImage.value.sprite, Is.SameAs(intro.Background));
        }

        [UnityTest]
        public IEnumerator TheRoomOpensUnderTheFirstLoadingImageFadingAway()
        {
            yield return Load();
            yield return AtStep(1);
            Assert.That(intro.Overlay.style.backgroundImage.value.sprite, Is.SameAs(intro.LoadingImages[0]));
            Assert.That(intro.Overlay.style.opacity.value, Is.EqualTo(0.99f).Within(1e-5f));
            Assert.That(intro.Overlay.parent.IndexOf(intro.Overlay), Is.EqualTo(intro.Overlay.parent.childCount - 1), "over everything");
            Screenshots.Capture("intro-opening");

            yield return AtStep(50);
            Assert.That(intro.Overlay.style.opacity.value, Is.EqualTo(0.5f).Within(1e-5f));
            yield return AtStep(110);
            Assert.That(intro.Scene.Fade.Mode, Is.EqualTo(LoadingFadeMode.Off));
            Assert.That(intro.Overlay.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator TwelveCloudsDriftLeftAtHalfAlphaOverTheStory()
        {
            yield return Load();
            yield return AtStep(1);
            Assert.That(intro.CloudElements, Has.Count.EqualTo(12));
            var story = intro.Text;
            foreach (var element in intro.CloudElements)
            {
                Assert.That(element.style.opacity.value, Is.EqualTo(0.5f));
                Assert.That(element.style.width.value.value, Is.EqualTo(256 * PixelScale).Within(1e-3f));
                Assert.That(element.parent.IndexOf(element), Is.GreaterThan(story.parent.IndexOf(story)), "depth -100 over the text's 1");
            }
            var first = intro.Scene.Clouds[0];
            var x = first.X;
            Assert.That(intro.CloudElements[0].style.left.value.value, Is.EqualTo(x * PixelScale).Within(1e-3f));
            Assert.That(intro.CloudElements[0].style.backgroundImage.value.sprite, Is.SameAs(intro.Clouds[0]));

            yield return AtStep(31);
            Assert.That(first.X, Is.EqualTo(x - 30).Or.GreaterThan(x), "a pixel a step, unless it wrapped");
            Assert.That(intro.CloudElements[0].style.left.value.value, Is.EqualTo(first.X * PixelScale).Within(1e-3f));
            Assert.That(intro.CloudElements[0].style.top.value.value, Is.EqualTo(first.Y * PixelScale).Within(1e-3f));
        }

        [UnityTest]
        public IEnumerator TheStoryIsTypedACharacterEveryFourStepsFromThreeSecondsIn()
        {
            yield return Load();
            yield return AtStep(179);
            Assert.That(intro.Text.style.display.value, Is.EqualTo(DisplayStyle.None));
            yield return AtStep(184);
            Assert.That(intro.Text.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(intro.Text.text, Is.EqualTo("F"));
            Assert.That(intro.Text.Style, Is.EqualTo(OriginalType.Intro), "fnt_mono outlined 2");
            Assert.That(intro.Text.style.left.value.value, Is.EqualTo(40 * PixelScale).Within(1e-3f));
            Assert.That(intro.Text.style.top.value.value, Is.EqualTo(50 * PixelScale).Within(1e-3f));

            yield return AtStep(IntroScene.StepTyping(45));
            Assert.That(intro.Text.text, Is.EqualTo("For centuries the empire had been fragmented."));
            Screenshots.Capture("intro-typing");

            var length = intro.Story.Length;
            yield return AtStep(IntroScene.StepTyping(length));
            Assert.That(intro.Text.text, Is.EqualTo(intro.Story));
            Screenshots.Capture("intro-story");
            yield return AtStep(IntroScene.StepFadingIn(length) - 1);
            Assert.That(intro.Scene.Fade.Mode, Is.EqualTo(LoadingFadeMode.Off), "held 300 steps");
            yield return AtStep(IntroScene.StepFadingIn(length) + 50);
            Assert.That(intro.Overlay.style.opacity.value, Is.EqualTo(0.51f).Within(1e-4f));
            Assert.That(intro.Overlay.style.backgroundImage.value.sprite, Is.SameAs(intro.LoadingImages[intro.Scene.Fade.Image]));
            Assert.That(leftFor, Is.Null);

            yield return AtStep(IntroScene.StepFadingIn(length) + 100);
            Assert.That(leftFor, Is.EqualTo("MainMenu"));
            Assert.That(LoadingHandoff.Pending, Is.EqualTo(intro.Scene.Fade.Image), "fade_asset goes on to the next room");
        }

        // The panel drawn into a texture: the map under the clouds, then the white story typed over it.
        [UnityTest]
        public IEnumerator TheIntroIsDrawnOnScreen()
        {
            yield return Load();
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device to draw with.");

            var document = intro.GetComponentInChildren<UIDocument>();
            var target = new RenderTexture(960, 540, 0, RenderTextureFormat.ARGB32);
            var panel = Object.Instantiate(document.panelSettings);
            panel.targetTexture = target;
            document.panelSettings = panel;

            yield return AtStep(170);
            yield return null;
            var map = Read(target);
            Screenshots.Save("09-intro-clouds", map);
            var before = White(map);

            yield return AtStep(IntroScene.StepTyping(intro.Story.Length));
            yield return null;
            var story = Read(target);
            Screenshots.Save("09-intro-story", story);
            var after = White(story);
            Assert.That(after, Is.GreaterThan(before + 500), $"white pixels {before} before the story, {after} with it");

            yield return AtStep(IntroScene.StepFadingIn(intro.Story.Length) + 99);
            yield return null;
            var fade = Read(target);
            Screenshots.Save("09-intro-fade", fade);

            Object.Destroy(map);
            Object.Destroy(story);
            Object.Destroy(fade);
            target.Release();
        }

        private static Texture2D Read(RenderTexture target)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            return pixels;
        }

        private static int White(Texture2D pixels) =>
            pixels.GetPixels32().Count(p => p.r > 245 && p.g > 245 && p.b > 245);

        [UnityTest]
        public IEnumerator AKeyPressStartsTheFadeToTheNextSceneAtOnce()
        {
            yield return Load();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return AtStep(400);
            Assert.That(intro.Scene.Fade.Mode, Is.EqualTo(LoadingFadeMode.Off));
            Press(keyboard.enterKey);
            Release(keyboard.enterKey);
            yield return AtStep(401);
            Assert.That(intro.Scene.Fade.Mode, Is.EqualTo(LoadingFadeMode.FadeIn));
            Assert.That(intro.Scene.Fade.Alpha, Is.EqualTo(0.01).Within(1e-9));
            yield return AtStep(501);
            Assert.That(leftFor, Is.EqualTo("MainMenu"));
        }

        [UnityTest]
        public IEnumerator AClickSkipsTheIntroIntoTheMainMenuWhichOpensUnderTheSameImage()
        {
            yield return Load(reallyLeave: true);
            var mouse = InputSystem.AddDevice<Mouse>();
            yield return AtStep(10);
            Press(mouse.leftButton);
            Release(mouse.leftButton);
            yield return AtStep(11);
            Assert.That(intro.Scene.Fade.Mode, Is.EqualTo(LoadingFadeMode.FadeIn));
            var image = intro.Scene.Fade.Image;

            yield return AtStep(111);
            Assert.That(intro.HasLeft, Is.True);
            for (var frame = 0; frame < 30 && SceneManager.GetActiveScene().name != "MainMenu"; frame++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            yield return null;

            var menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu.Menu.Fade.Image, Is.EqualTo(image), "obj_fade_mainmenu opens under fade_asset");
            Assert.That(menu.Overlay.style.backgroundImage.value.sprite, Is.SameAs(menu.LoadingImages[image]));
            Assert.That(menu.Overlay.style.opacity.value, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(LoadingHandoff.Pending, Is.Null, "taken once");
        }
    }
}
