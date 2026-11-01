using System.Collections;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    public class DuelTauntTests
    {
        private DuelController controller;
        private DuelTaunt taunt;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindFirstObjectByType<DuelController>();
            taunt = controller.GetComponent<DuelTaunt>();
            Assert.That(taunt, Is.Not.Null, "Duel scene has no DuelTaunt.");
            controller.Settings = DuelSceneTests.FastSettings();
        }

        [UnityTest]
        public IEnumerator AnEarlyStrikeIsTauntedAndTheTauntClearsNextRound()
        {
            controller.BeginRound();
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Standoff);
            controller.Strike(Side.Right, controller.Now);

            Assert.That(DuelTaunt.Lines, Does.Contain(taunt.Current));
            for (var i = 0; i < 30; i++) yield return null;
            // Where the GameMaker build drew "Premature Strike!": centred, white, on Get Ready's line, with no winner
            // banner or headline beside it.
            var view = controller.GetComponent<DuelView>();
            var label = view.Root.Q<OutlinedText>("Taunt");
            var stage = view.Root.Q("Stage").worldBound;
            Assert.That(label.worldBound.center.x, Is.EqualTo(stage.center.x).Within(3f), "centred");
            Assert.That(label.FillLayer.resolvedStyle.color, Is.EqualTo(Color.white));
            Assert.That(view.WinsText, Is.Empty);
            Assert.That(view.HeadlineText, Is.Empty);
            Screenshots.Capture("06-taunt");

            controller.BeginRound();
            Assert.That(taunt.Current, Is.Empty);
        }

        [UnityTest]
        public IEnumerator AFairWinIsNotTaunted()
        {
            controller.BeginRound();
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            controller.Strike(Side.Left, controller.CueAt + 0.2);
            controller.Strike(Side.Right, controller.CueAt + 0.3);

            Assert.That(taunt.Current, Is.Empty);
        }
    }
}
