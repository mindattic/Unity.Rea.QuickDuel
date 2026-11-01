using System.Collections;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    // obj_time (UQD-T50): the match clock drops in at the top centre of the room and counts the match's mm:ss. The
    // controller's clock is held and stepped by hand from the moment the clock started.
    public class DuelClockTests
    {
        private DuelController controller;
        private DuelView view;
        private DuelClock clock;
        private double now;

        private const float PixelScale = 1080f / 480f;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            DuelModeHandoff.Clear();
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<DuelController>();
            view = controller.GetComponent<DuelView>();
            clock = controller.GetComponent<DuelClock>();
            Assert.That(clock, Is.Not.Null, "Duel scene has no DuelClock.");
            now = clock.StartedAt;
            controller.Clock = () => now;
        }

        [TearDown]
        public void Clear() => DuelModeHandoff.Clear();

        private IEnumerator At(long tick)
        {
            now = clock.StartedAt + (tick + 0.5) / RoundClock.TicksPerSecond;
            yield return null;
            yield return null;
        }

        [Test]
        public void ItIsDrawnInTheBasicFaceOnTheStageUnderTheLetterbox()
        {
            Assert.That(clock.Element.parent, Is.SameAs(view.Stage), "obj_time drew at depth 0, with the duel's text");
            Assert.That(view.Root.IndexOf(view.Stage), Is.LessThan(view.Root.IndexOf(view.LetterboxTop)));
            Assert.That(clock.Minutes.Style, Is.EqualTo(OriginalType.Hud), "fnt_basic outlined 1");
            Assert.That(clock.Colon.text, Is.EqualTo(":"));
            Assert.That(clock.Minutes.resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.UpperRight));
            Assert.That(clock.Seconds.resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.UpperLeft));
            var room = view.Stage.worldBound;
            Assert.That(clock.Minutes.worldBound.xMax - room.xMin, Is.EqualTo(317 * PixelScale).Within(0.5f), "ends at room_width / 2 - 3");
            Assert.That(clock.Seconds.worldBound.xMin - room.xMin, Is.EqualTo(323 * PixelScale).Within(0.5f), "starts at room_width / 2 + 3");
            Assert.That(clock.Colon.worldBound.center.x - room.xMin, Is.EqualTo(320 * PixelScale).Within(0.5f), "centred on the middle");
        }

        [UnityTest]
        public IEnumerator ItDropsInFromAboveTheRoomAndCountsTheMatchsTime()
        {
            yield return At(0);
            Assert.That(clock.Y, Is.EqualTo(-95), "one step before the first draw");
            Assert.That(clock.Element.style.top.value.value, Is.EqualTo(-95 * PixelScale).Within(1e-3f));
            Assert.That(clock.InPosition, Is.False);
            Assert.That(clock.Text, Is.EqualTo("00:00"));

            yield return At(10);
            Assert.That(clock.Y, Is.EqualTo(-45));
            Screenshots.Capture("13-clock-dropping");

            yield return At(RoundClock.InPositionSteps - 1);
            Assert.That(clock.InPosition, Is.True);
            Assert.That(clock.Y, Is.EqualTo(5), "home 5 pixels down");
            Assert.That(clock.Element.style.top.value.value, Is.EqualTo(5 * PixelScale).Within(1e-3f));

            yield return At(RoundClock.InPositionSteps - 1 + 60 * 75);
            Assert.That(clock.Text, Is.EqualTo("01:15"));
            Assert.That(clock.Minutes.text, Is.EqualTo("01"));
            Assert.That(clock.Seconds.text, Is.EqualTo("15"));
            Screenshots.Capture("13-clock-running");
        }

        [UnityTest]
        public IEnumerator ItStartsOverWithANewMatch()
        {
            var session = controller.GetComponent<DuelSession>();
            yield return At(RoundClock.InPositionSteps - 1 + 60 * 40);
            Assert.That(clock.Text, Is.EqualTo("00:40"));

            var startedAt = clock.StartedAt;
            Assert.That(session.SetMode(DuelMode.VsAiNormal), Is.True, "a new match");
            yield return null;
            yield return null;
            Assert.That(clock.StartedAt, Is.EqualTo(now), "a new obj_time with the new match");
            Assert.That(clock.StartedAt, Is.Not.EqualTo(startedAt));
            Assert.That(clock.Text, Is.EqualTo("00:00"));
            Assert.That(clock.InPosition, Is.False, "dropping in again");
        }
    }
}
