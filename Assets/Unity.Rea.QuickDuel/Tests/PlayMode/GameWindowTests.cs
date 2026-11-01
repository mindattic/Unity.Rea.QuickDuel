using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    // The window (UQD-T50): every scene draws the original's 640 x 480 room as a 1440 x 1080 frame letterboxed into the
    // window, black around it, and F1 toggles fullscreen in every scene, as obj_intro, obj_mainmenu and obj_game did.
    public class GameWindowTests : InputTestFixture
    {
        [TearDown]
        public override void TearDown()
        {
            LoadingHandoff.Clear();
            DuelModeHandoff.Clear();
            base.TearDown();
        }

        private static IEnumerator Load(string scene)
        {
            LoadingHandoff.Clear();
            DuelModeHandoff.Clear();
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EverySceneDrawsTheRoomOnAFrameLetterboxedIntoTheWindow([Values("Intro", "MainMenu", "Duel")] string scene)
        {
            yield return Load(scene);
            var document = Object.FindAnyObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null);
            var root = document.rootVisualElement;
            var panel = root.panel.visualTree.layout;

            Assert.That(root.layout.width, Is.EqualTo(1440f).Within(0.5f), "640 original pixels at 2.25");
            Assert.That(root.layout.height, Is.EqualTo(1080f).Within(0.5f), "480 original pixels at 2.25");
            Assert.That(root.worldBound.center.x, Is.EqualTo(panel.center.x).Within(0.5f), "centred");
            Assert.That(root.worldBound.center.y, Is.EqualTo(panel.center.y).Within(0.5f));
            Assert.That(panel.width, Is.GreaterThanOrEqualTo(1440f - 0.5f), "the whole frame fits the window");
            Assert.That(panel.height, Is.GreaterThanOrEqualTo(1080f - 0.5f));
            Assert.That(Mathf.Abs(panel.width - 1440f) < 0.5f || Mathf.Abs(panel.height - 1080f) < 0.5f, Is.True,
                "and fills it one way, the bars the other");
            Assert.That(root.style.overflow.value, Is.EqualTo(Overflow.Hidden), "nothing strays past the room's edges");
            Assert.That(root.panel.visualTree.resolvedStyle.backgroundColor, Is.EqualTo(Color.black), "black bars");
        }

        // Whatever the test window's own aspect: the panel scales so the whole frame fits (Expand), never cropping it to the
        // window one way (Shrink cut a wide window's room off at the top and bottom).
        [UnityTest]
        public IEnumerator ThePanelFitsTheWholeFrameAtAnyAspect([Values("Intro", "MainMenu", "Duel")] string scene)
        {
            yield return Load(scene);
            var settings = Object.FindAnyObjectByType<UIDocument>().panelSettings;
            Assert.That(settings.scaleMode, Is.EqualTo(PanelScaleMode.ScaleWithScreenSize));
            Assert.That(settings.referenceResolution, Is.EqualTo(new Vector2Int(1440, 1080)));
            Assert.That(settings.screenMatchMode, Is.EqualTo(PanelScreenMatchMode.Expand));
        }

        [UnityTest]
        public IEnumerator TheDuelsStageIsTheRoom()
        {
            yield return Load("Duel");
            var view = Object.FindAnyObjectByType<DuelView>();
            Assert.That(view.Stage.worldBound.position.x, Is.EqualTo(view.Root.worldBound.position.x).Within(0.5f));
            Assert.That(view.Stage.worldBound.position.y, Is.EqualTo(view.Root.worldBound.position.y).Within(0.5f));
            Assert.That(view.Stage.worldBound.width, Is.EqualTo(view.Root.worldBound.width).Within(0.5f));
            // left_position_idle = room_width / 4: 160 original pixels in from the room's left edge.
            Assert.That(view.FighterElement(Core.Side.Left).worldBound.center.x - view.Root.worldBound.xMin,
                Is.EqualTo(160f * 2.25f).Within(3f));
        }

        [UnityTest]
        public IEnumerator F1TogglesFullscreenInEveryScene([Values("Intro", "MainMenu", "Duel")] string scene)
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return Load(scene);
            var window = Object.FindAnyObjectByType<GameWindow>();
            Assert.That(window, Is.Not.Null, $"{scene} has no F1 handler.");

            var fullscreen = false;
            var applied = new List<(int, int, FullScreenMode)>();
            window.IsFullscreen = () => fullscreen;
            window.CurrentSize = () => new Vector2Int(1000, 700);
            window.DisplaySize = () => new Vector2Int(2560, 1440);
            window.Apply = (w, h, mode) =>
            {
                applied.Add((w, h, mode));
                fullscreen = mode != FullScreenMode.Windowed;
            };

            PressAndRelease(keyboard.f1Key);
            yield return null;
            Assert.That(applied, Is.EqualTo(new[] { (2560, 1440, FullScreenMode.FullScreenWindow) }), "the whole display");

            PressAndRelease(keyboard.f1Key);
            yield return null;
            Assert.That(applied[1], Is.EqualTo((1000, 700, FullScreenMode.Windowed)), "back to the window it was");

        }

        [UnityTest]
        public IEnumerator F1DoesNotSkipTheIntro()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return Load("Intro");
            var intro = Object.FindAnyObjectByType<IntroController>();
            var window = Object.FindAnyObjectByType<GameWindow>();
            window.Apply = (w, h, mode) => { };
            var now = intro.StartedAt;
            intro.Clock = () => now;

            PressAndRelease(keyboard.f1Key);
            yield return null;
            now += 0.5;
            yield return null;
            Assert.That(intro.Scene.Fade.Mode, Is.Not.EqualTo(Core.LoadingFadeMode.FadeIn), "F1 is the window's");

            PressAndRelease(keyboard.spaceKey);
            yield return null;
            now += 0.5;
            yield return null;
            Assert.That(intro.Scene.Fade.Mode, Is.EqualTo(Core.LoadingFadeMode.FadeIn), "any other key still skips");
        }
    }
}
