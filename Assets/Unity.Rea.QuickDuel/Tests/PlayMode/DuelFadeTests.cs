using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    // obj_fade_gamescreen (UQD-T42): three seconds after the cut one of the 14 loading images fades in at 0.005 a step,
    // the battle is reset under it, and it fades out again. The controller's clock is held and stepped by hand, so every
    // stage is looked at on the step it belongs to.
    public class DuelFadeTests
    {
        private DuelController controller;
        private DuelView view;
        private DuelFade fade;
        private double now;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<DuelController>();
            view = controller.GetComponent<DuelView>();
            fade = controller.GetComponent<DuelFade>();
            Assert.That(fade, Is.Not.Null, "Duel scene has no DuelFade.");
            var settings = DuelSceneTests.FastSettings();
            settings.StrikeTimeoutSeconds = 5;
            controller.Settings = settings;
        }

        [Test]
        public void TheSceneCarriesTheFourteenLoadingImages()
        {
            Assert.That(fade.Images, Has.Count.EqualTo(RoundFade.Images));
            Assert.That(fade.Images.All(i => i != null));
            Assert.That(fade.Images.Distinct().Count(), Is.EqualTo(RoundFade.Images));
        }

        // Plays a round to a fair cut, then holds the clock there.
        private IEnumerator CutAndHoldTheClock()
        {
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            var cue = controller.CueAt;
            controller.Strike(Side.Left, cue + 0.15);
            controller.Strike(Side.Right, cue + 0.25);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Resolved));
            now = controller.Now;
            controller.Clock = () => now;
            yield return null;
        }

        private IEnumerator At(int tick)
        {
            now = controller.EndedAt.Value + (tick + 0.5) / RoundFade.TicksPerSecond;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheImageFadesInAfterTheAftermathResetsTheBattleAndFadesOut()
        {
            yield return CutAndHoldTheClock();
            var weapons = controller.GetComponent<DuelWeapons>();
            var blood = controller.GetComponent<DuelBlood>();

            yield return At(RoundFade.AftermathTicks - 1);
            Assert.That(fade.Stage, Is.EqualTo(RoundFadeStage.Aftermath));
            Assert.That(fade.Alpha, Is.Zero);
            Assert.That(fade.Overlay.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(fade.Pending, Is.True);
            Assert.That(view.WinsText, Is.EqualTo("Wins!"), "the aftermath plays on");
            Assert.That(view.PromptText, Is.Empty, "no next round over the aftermath");

            yield return At(RoundFade.AftermathTicks + 99);
            Assert.That(fade.Stage, Is.EqualTo(RoundFadeStage.FadingIn));
            Assert.That(fade.Alpha, Is.EqualTo(0.5f).Within(1e-4f), "0.005 a step");
            Assert.That(fade.Overlay.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(fade.Overlay.style.opacity.value, Is.EqualTo(fade.Alpha));
            Assert.That(fade.Overlay.parent.IndexOf(fade.Overlay), Is.EqualTo(fade.Overlay.parent.childCount - 1), "over everything");
            Assert.That(fade.Image, Is.InRange(0, RoundFade.Images - 1));
            Assert.That(fade.Overlay.style.backgroundImage.value.sprite, Is.SameAs(fade.Images[fade.Image.Value]));
            Assert.That(weapons.Toss, Is.Not.Null, "nothing reset while the image comes up");
            Assert.That(blood.Spray.Emitting, Is.True);
            Assert.That(fade.HasReset, Is.False);
            Screenshots.Capture("fade-in");

            var splatters = blood.Field.Splatters.Count;
            yield return At(RoundFade.ResetTick);
            Assert.That(fade.HasReset, Is.True);
            Assert.That(fade.Pending, Is.False);
            Assert.That(fade.Alpha, Is.EqualTo(1f).Within(1e-4f), "reset under a full image");
            Assert.That(weapons.Toss, Is.Null, "every obj_weapon destroyed");
            Assert.That(blood.Spray.Emitting, Is.False, "the blood emitter destroyed");
            Assert.That(blood.Field.Splatters.Count, Is.EqualTo(splatters), "the splatters fade on at their own pace");
            foreach (var side in new[] { Side.Left, Side.Right })
            {
                Assert.That(view.Motion(side).Pose, Is.EqualTo(FighterPose.Idle), "set_state_idle");
                Assert.That(view.FighterShows(side), Is.SameAs(view.FighterIn(side).Idle));
            }
            Assert.That(view.WinsText, Is.Empty);
            Assert.That(view.WinnerNameText, Is.Empty);
            Assert.That(view.CounterText, Is.Empty);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Resolved), "the next round waits for the startup");
            Assert.That(controller.GetComponent<DuelStartup>().IsRunning, Is.True, "gameclock.mode = \"startup\" under the full image");
            Assert.That(view.PromptText, Is.Empty);

            yield return At(RoundFade.ResetTick + 100);
            Assert.That(fade.Stage, Is.EqualTo(RoundFadeStage.FadingOut));
            Assert.That(fade.Alpha, Is.EqualTo(0.5f).Within(1e-4f));
            Screenshots.Capture("fade-out");

            yield return At(RoundFade.OffTick);
            Assert.That(fade.Stage, Is.EqualTo(RoundFadeStage.Off));
            Assert.That(fade.Overlay.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Motion(Side.Left).Pose, Is.EqualTo(FighterPose.Idle));

            Assert.That(controller.BeginRound(), Is.True);
            yield return null;
            Assert.That(fade.HasReset, Is.False);
            Assert.That(fade.Image, Is.Null);
        }

        [UnityTest]
        public IEnumerator ARoundBegunMidFadeTakesTheImageAway()
        {
            yield return CutAndHoldTheClock();
            yield return At(RoundFade.AftermathTicks + 50);
            Assert.That(fade.Overlay.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            Assert.That(controller.BeginRound(), Is.True, "online the host's Space can come first");
            yield return null;
            Assert.That(fade.Stage, Is.EqualTo(RoundFadeStage.Off));
            Assert.That(fade.Pending, Is.False);
            Assert.That(fade.Overlay.style.display.value, Is.EqualTo(DisplayStyle.None));
        }
    }
}
