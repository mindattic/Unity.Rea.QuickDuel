using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    // Escape (or gamepad Select+Start) leaves the duel for the main menu at any moment, as obj_game's Escape left for the
    // dropped character select (UQD-T65); the verdict screen, which had no Escape, ignores it (DuelVerdictTests).
    public class DuelQuitTests : InputTestFixture
    {
        private DuelController controller;
        private DuelQuit quit;
        private Keyboard keyboard;
        private readonly List<string> left = new List<string>();

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            keyboard = InputSystem.AddDevice<Keyboard>();
            controller = Object.FindFirstObjectByType<DuelController>();
            quit = controller.GetComponent<DuelQuit>();
            Assert.That(quit, Is.Not.Null, "Duel scene has no DuelQuit.");
            controller.Settings = DuelSceneTests.FastSettings();
            left.Clear();
            quit.Leave = left.Add;
        }

        [UnityTest]
        public IEnumerator EscapeLeavesForTheMenuBeforeARoundStarts()
        {
            yield return Load();
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Ready));
            Press(keyboard.escapeKey);
            yield return null;

            Assert.That(left, Is.EqualTo(new[] { "MainMenu" }));
        }

        [UnityTest]
        public IEnumerator EscapeLeavesForTheMenuMidRound()
        {
            yield return Load();
            controller.BeginRound();
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Standoff);

            Press(keyboard.escapeKey);
            yield return null;

            Assert.That(left, Is.EqualTo(new[] { "MainMenu" }), "obj_game's Escape left mid-round too");
        }

        [UnityTest]
        public IEnumerator GamepadSelectAndStartLeavesWithoutAlsoBeginningARound()
        {
            yield return Load();
            var session = controller.GetComponent<DuelSession>();
            var pad = InputSystem.AddDevice<Gamepad>();
            Press(pad.selectButton);
            yield return null;
            Press(pad.startButton);
            yield return null;
            Release(pad.selectButton);
            Release(pad.startButton);
            yield return null;

            Assert.That(left, Is.EqualTo(new[] { "MainMenu" }));
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Ready), "Select+Start leaves; it shouldn't also begin a round.");
            Assert.That(session.Mode, Is.EqualTo(DuelMode.LocalPvp));
        }

        [UnityTest]
        public IEnumerator GamepadStartAloneNeitherLeavesNorBeginsALocalRound()
        {
            yield return Load();
            var pad = InputSystem.AddDevice<Gamepad>();
            Press(pad.startButton);
            yield return null;

            Assert.That(left, Is.Empty);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Ready), "a local round's startup runs by itself (UQD-T66)");
        }

        [UnityTest]
        public IEnumerator GamepadSelectAloneDoesNotChangeTheMode()
        {
            yield return Load();
            var session = controller.GetComponent<DuelSession>();
            var pad = InputSystem.AddDevice<Gamepad>();
            Press(pad.selectButton);
            yield return null;
            Release(pad.selectButton);
            yield return null;

            Assert.That(left, Is.Empty);
            Assert.That(session.Mode, Is.EqualTo(DuelMode.LocalPvp), "the original had no mode key in the duel (UQD-T67)");
        }
    }
}
