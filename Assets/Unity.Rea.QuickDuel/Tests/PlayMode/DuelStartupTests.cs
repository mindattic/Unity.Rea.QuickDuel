using System.Collections;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    // The startup (UQD-T66), as gameclock_startup ran it: the first round's countdown begins by itself startup_time
    // after the scene starts, every later round's startup_time after the round transition's loading image is gone, a
    // click in the startup begins it at once, and a finished match never starts another.
    public class DuelStartupTests
    {
        private DuelController controller;
        private DuelStartup startup;
        private double now;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            DuelStartup.RunsByItself = true;
            DuelModeHandoff.Clear();
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<DuelController>();
            startup = controller.GetComponent<DuelStartup>();
            Assert.That(startup, Is.Not.Null, "Duel scene has no DuelStartup.");
            Assert.That(startup.Auto, Is.True);
            controller.Settings = DuelSceneTests.FastSettings();
            now = startup.SceneStartedAt;
            controller.Clock = () => now;
        }

        [TearDown]
        public void DriveRoundsByHandAgain()
        {
            DuelStartup.RunsByItself = false;
            DuelModeHandoff.Clear();
        }

        private IEnumerator At(double seconds)
        {
            now = seconds;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheFirstRoundsCountdownBeginsByItselfThreeSecondsIn()
        {
            Assert.That(startup.StartupSeconds, Is.EqualTo(3.0), "startup_time = 3 * room_speed");
            Assert.That(startup.BeginsAt, Is.EqualTo(startup.SceneStartedAt + 3.0).Within(1e-9), "juststarted: it counts at once");

            yield return At(startup.SceneStartedAt + 2.9);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Ready));
            Assert.That(startup.IsRunning, Is.True);

            yield return At(startup.SceneStartedAt + 3.01);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Countdown), "mode = \"countdown\"");
            Assert.That(startup.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator TheNextRoundBeginsThreeSecondsAfterTheLoadingImageIsGone()
        {
            Assert.That(startup.TrySkip(), Is.True, "a click in the startup begins the round at once");
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Countdown));
            Assert.That(startup.TrySkip(), Is.False, "no startup mid-round");

            yield return At(controller.CueAt + 0.01);
            controller.Strike(Side.Left, controller.CueAt + 0.005);
            yield return At(controller.CueAt + 0.2);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Resolved));
            var ended = controller.EndedAt.Value;
            var off = ended + (double)RoundFade.OffTick / RoundFade.TicksPerSecond;
            Assert.That(startup.BeginsAt, Is.EqualTo(off + 3.0).Within(1e-9));

            yield return At(ended + (RoundFade.ResetTick - 1.0) / RoundFade.TicksPerSecond);
            Assert.That(startup.IsRunning, Is.False, "over the aftermath and the fade-in");
            Assert.That(startup.TrySkip(), Is.False);

            yield return At(ended + (RoundFade.ResetTick + 0.5) / RoundFade.TicksPerSecond);
            Assert.That(startup.IsRunning, Is.True, "from the battle's reset under the full image");

            yield return At(off + 2.9);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Resolved));

            yield return At(off + 3.01);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Countdown));
            Assert.That(controller.Round, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator AFinishedMatchNeverStartsAnother()
        {
            Assert.That(controller.GetComponent<DuelSession>().SetMode(DuelMode.VsAiNormal), Is.True);
            for (var round = 0; round < 2; round++)
            {
                Assert.That(startup.TrySkip(), Is.True);
                yield return At(controller.CueAt + 0.01);
                controller.Strike(Side.Left, controller.CueAt + 0.005);
                yield return At(controller.CueAt + 0.2);
                Assert.That(controller.Result.Winner, Is.EqualTo(Side.Left));
                if (round == 0)
                    yield return At(controller.EndedAt.Value + (RoundFade.ResetTick + 0.5) / RoundFade.TicksPerSecond);
            }
            Assert.That(controller.Match.IsOver, Is.True);

            yield return At(startup.BeginsAt + 1);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Resolved), "the verdict's button decides what follows");
            Assert.That(startup.IsRunning, Is.False);
        }
    }
}
