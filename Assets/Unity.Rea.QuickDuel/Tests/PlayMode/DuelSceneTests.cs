using System;
using System.Collections;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    public class DuelSceneTests
    {
        private DuelController controller;

        public static DuelSettings FastSettings() => new DuelSettings
        {
            CountdownSeconds = 0.2,
            MinStandoffSeconds = 0.1,
            MaxStandoffSeconds = 0.2,
            StrikeTimeoutSeconds = 1.0,
            SettleSeconds = TuningStore.Profile.Duel.SettleSeconds
        };

        public static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds = 5f)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail("Timed out waiting for the duel.");
                yield return null;
            }
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            controller = Object.FindFirstObjectByType<DuelController>();
            Assert.That(controller, Is.Not.Null, "Duel scene has no DuelController.");
            controller.Settings = FastSettings();
        }

        [UnityTest]
        public IEnumerator ARoundResolvesToTheFasterStrike()
        {
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Ready));
            Assert.That(controller.BeginRound(), Is.True);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Countdown));

            yield return WaitUntil(() => controller.Phase == DuelPhase.Strike);
            var cue = controller.CueAt;
            controller.Strike(Side.Right, cue + 0.250);
            controller.Strike(Side.Left, cue + 0.180);

            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Resolved));
            Assert.That(controller.Result.Outcome, Is.EqualTo(DuelOutcome.FasterStrike));
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Left));
            Assert.That(controller.Result.LeftReaction, Is.EqualTo(0.180).Within(1e-9));
        }

        [UnityTest]
        public IEnumerator ALoneStrikeResolvesOnTheNextTicks()
        {
            controller.BeginRound();
            yield return WaitUntil(() => controller.Phase == DuelPhase.Strike);
            controller.Strike(Side.Right, controller.Now);

            yield return WaitUntil(() => controller.Phase == DuelPhase.Resolved);
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Right));
        }

        [UnityTest]
        public IEnumerator StrikingDuringTheStandoffLoses()
        {
            controller.BeginRound();
            yield return WaitUntil(() => controller.Phase == DuelPhase.Standoff);
            controller.Strike(Side.Left, controller.Now);

            Assert.That(controller.Result.Outcome, Is.EqualTo(DuelOutcome.EarlyStrike));
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Right));
        }

        [UnityTest]
        public IEnumerator ANewRoundStartsOnlyAfterTheLastResolves()
        {
            controller.BeginRound();
            Assert.That(controller.BeginRound(), Is.False);
            yield return WaitUntil(() => controller.Phase == DuelPhase.Resolved, 3f);
            Assert.That(controller.Result.Outcome, Is.EqualTo(DuelOutcome.NoStrike));

            Assert.That(controller.BeginRound(), Is.True);
            Assert.That(controller.Round, Is.EqualTo(2));
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Countdown));
        }
    }
}
