using System.Collections;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    // Drives the real scene through fake Input System devices; strikes are queued with explicit event
    // timestamps, so the asserted reactions come from those times, not from when a frame ran.
    public class DuelInputTests : InputTestFixture
    {
        private DuelController controller;
        private Keyboard keyboard;

        private IEnumerator Load()
        {
            // Devices go in after the load; the scene's actions pick them up.
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            keyboard = InputSystem.AddDevice<Keyboard>();
            controller = Object.FindFirstObjectByType<DuelController>();
            Assert.That(controller.GetComponent<DuelInput>(), Is.Not.Null, "Duel scene has no DuelInput.");
            controller.Settings = DuelSceneTests.FastSettings();
            // A cold scene's first frames can take longer than a fast round lasts: let them pass before one begins.
            yield return null;
            yield return null;
        }

        // obj_game's global left press in the startup begins the round at once (UQD-T66).
        [UnityTest]
        public IEnumerator AClickInTheStartupBeginsARoundAndKeysStrikeByEventTime()
        {
            yield return Load();
            var mouse = InputSystem.AddDevice<Mouse>();
            Press(mouse.leftButton);
            Release(mouse.leftButton);
            yield return null;
            Assert.That(controller.Round, Is.EqualTo(1), "the round began");

            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            var cue = controller.CueAt;
            // Versus: Left Ctrl and Right Ctrl (UQD-T69). One keyboard delivers its events in time order (the Input
            // System drops older ones).
            Press(keyboard.leftCtrlKey, cue + 0.120);
            Press(keyboard.rightCtrlKey, cue + 0.200);
            yield return null;

            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Resolved));
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Left));
            Assert.That(controller.Result.LeftReaction, Is.EqualTo(0.120).Within(1e-6));
            Assert.That(controller.Result.RightReaction, Is.EqualTo(0.200).Within(1e-6));
        }

        // UQD-T42, UQD-T66: a click over the aftermath begins nothing; once the loading fade has reset the battle the
        // startup runs, and a click skips it.
        [UnityTest]
        public IEnumerator AClickWaitsForTheLoadingFadeToResetTheBattle()
        {
            yield return Load();
            var mouse = InputSystem.AddDevice<Mouse>();
            controller.BeginRound();
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            var cue = controller.CueAt;
            controller.Strike(Side.Left, cue + 0.1);
            controller.Strike(Side.Right, cue + 0.2);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Resolved));

            Press(mouse.leftButton);
            Release(mouse.leftButton);
            yield return null;
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Resolved), "not over the aftermath");

            var reset = controller.EndedAt.Value + (RoundFade.ResetTick + 1.0) / RoundFade.TicksPerSecond;
            controller.Clock = () => reset;
            yield return null;
            Assert.That(controller.GetComponent<DuelFade>().HasReset, Is.True);
            Press(mouse.leftButton);
            Release(mouse.leftButton);
            yield return null;
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Countdown));
        }

        [UnityTest]
        public IEnumerator SecondGamepadStrikesForTheRight()
        {
            yield return Load();
            var pad1 = InputSystem.AddDevice<Gamepad>();
            var pad2 = InputSystem.AddDevice<Gamepad>();
            // A pad's South skips the startup, as the click does (UQD-T66).
            Press(pad1.buttonSouth);
            Release(pad1.buttonSouth);
            yield return null;
            Assert.That(controller.Round, Is.EqualTo(1), "the round began");

            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            var cue = controller.CueAt;
            Press(pad2.buttonSouth, cue + 0.150);
            Press(pad1.buttonSouth, cue + 0.300);
            yield return null;

            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Right));
            Assert.That(controller.Result.RightReaction, Is.EqualTo(0.150).Within(1e-6));
        }

        // Against the computer the left mouse button strikes for the player (obj_game's global left press, UQD-T69).
        [UnityTest]
        public IEnumerator AgainstTheComputerAClickStrikesForTheLeftByEventTime()
        {
            yield return Load();
            Assert.That(controller.GetComponent<DuelSession>().SetMode(DuelMode.VsAiNormal), Is.True);
            yield return null;
            var mouse = InputSystem.AddDevice<Mouse>();
            Assert.That(controller.BeginRound(), Is.True);

            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            var cue = controller.CueAt;
            Press(mouse.leftButton, cue + 0.180);
            yield return null;

            // The computer (Normal, 300 ms at the soonest) is slower, so the round resolves once it settles.
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved);
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Left));
            Assert.That(controller.Result.LeftReaction, Is.EqualTo(0.180).Within(1e-6));
        }

        // Against the computer Space strikes for the player, and the Ctrl keys are Versus's (UQD-T69).
        [UnityTest]
        public IEnumerator AgainstTheComputerSpaceStrikesAndTheCtrlKeysDoNot()
        {
            yield return Load();
            Assert.That(controller.GetComponent<DuelSession>().SetMode(DuelMode.VsAiNormal), Is.True);
            yield return null;
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            var cue = controller.CueAt;
            Press(keyboard.leftCtrlKey, cue + 0.100);
            Press(keyboard.spaceKey, cue + 0.150);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved);
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Left));
            Assert.That(controller.Result.LeftReaction, Is.EqualTo(0.150).Within(1e-6), "Space struck; Left Ctrl did not");
        }

        // In Versus Space and the mouse strike for nobody: only the Ctrl keys do (UQD-T69).
        [UnityTest]
        public IEnumerator InVersusSpaceAndTheMouseStrikeForNobody()
        {
            yield return Load();
            var mouse = InputSystem.AddDevice<Mouse>();
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            var cue = controller.CueAt;
            Press(keyboard.spaceKey, cue + 0.050);
            Press(mouse.leftButton, cue + 0.060);
            Press(keyboard.rightCtrlKey, cue + 0.200);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved);
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Right));
            Assert.That(controller.Result.LeftReaction, Is.Null);
        }

        [UnityTest]
        public IEnumerator AKeyDuringTheStandoffIsAnEarlyStrike()
        {
            yield return Load();
            controller.BeginRound();
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Standoff);
            Press(keyboard.rightCtrlKey, controller.CueAt - 0.05);
            yield return null;

            Assert.That(controller.Result.Outcome, Is.EqualTo(DuelOutcome.EarlyStrike));
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Left));
            Assert.That(controller.Result.RightReaction, Is.EqualTo(-0.05).Within(1e-6));
        }
    }
}
