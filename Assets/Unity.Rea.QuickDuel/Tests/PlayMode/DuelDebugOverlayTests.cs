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
    // The original's F12 debug overlay as the developer's toggle (UQD-T50): off at the start, F12 turns it on and off in
    // the editor and development builds, and it shows each side's reaction where obj_game drew it.
    public class DuelDebugOverlayTests : InputTestFixture
    {
        private const float PixelScale = 1080f / 480f;

        [TearDown]
        public override void TearDown()
        {
            DuelModeHandoff.Clear();
            base.TearDown();
        }

        private static IEnumerator Load()
        {
            DuelModeHandoff.Clear();
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator F12TogglesItOffAtTheStart()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return Load();
            var overlay = Object.FindAnyObjectByType<DuelDebugOverlay>();
            Assert.That(overlay, Is.Not.Null, "Duel scene has no DuelDebugOverlay.");
            Assert.That(overlay.Allowed, Is.True, "the editor is a development build");
            Assert.That(overlay.Visible, Is.False, "off at the start, not globals.gml's debug = true");
            Assert.That(overlay.Element.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            PressAndRelease(keyboard.f12Key);
            yield return null;
            yield return null;
            Assert.That(overlay.Visible, Is.True);
            Assert.That(overlay.Element.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(overlay.LeftReaction.text, Is.EqualTo("9999"), "no strike yet");
            Assert.That(overlay.RightReaction.text, Is.EqualTo("9999"));
            Assert.That(overlay.Phase.text, Is.EqualTo("Ready"));
            Screenshots.Capture("14-debug-overlay");

            PressAndRelease(keyboard.f12Key);
            yield return null;
            Assert.That(overlay.Visible, Is.False);
        }

        [UnityTest]
        public IEnumerator ReleaseBuildsIgnoreF12()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return Load();
            var overlay = Object.FindAnyObjectByType<DuelDebugOverlay>();
            overlay.Allowed = false;
            PressAndRelease(keyboard.f12Key);
            yield return null;
            Assert.That(overlay.Visible, Is.False);
        }

        [UnityTest]
        public IEnumerator EachSidesReactionStandsWhereObjGameDrewIt()
        {
            yield return Load();
            var controller = Object.FindAnyObjectByType<DuelController>();
            var view = controller.GetComponent<DuelView>();
            var overlay = controller.GetComponent<DuelDebugOverlay>();
            overlay.Toggle();
            yield return null;
            yield return null;

            var room = view.Root.worldBound;
            Assert.That(overlay.LeftReaction.Style, Is.EqualTo(OriginalType.Name), "fnt_oldenglish_small outlined 1");
            Assert.That(overlay.LeftReaction.worldBound.xMin - room.xMin, Is.EqualTo(10 * PixelScale).Within(0.5f));
            Assert.That(overlay.LeftReaction.worldBound.yMin - room.yMin, Is.EqualTo(240 * PixelScale).Within(0.5f));
            Assert.That(overlay.RightReaction.worldBound.xMax - room.xMin, Is.EqualTo(630 * PixelScale).Within(0.5f));
            Assert.That(overlay.RightReaction.resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.UpperRight));
            Assert.That(overlay.Rate.worldBound.xMin - room.xMin, Is.EqualTo(320 * PixelScale).Within(0.5f));
            Assert.That(overlay.Phase.Style, Is.EqualTo(OriginalType.Hud));
        }
    }
}
