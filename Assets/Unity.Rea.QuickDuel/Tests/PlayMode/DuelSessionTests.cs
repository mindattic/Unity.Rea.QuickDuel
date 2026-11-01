using System.Collections;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    public class DuelSessionTests
    {
        private DuelController controller;
        private DuelSession session;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindFirstObjectByType<DuelController>();
            session = controller.GetComponent<DuelSession>();
            Assert.That(session, Is.Not.Null, "Duel scene has no DuelSession.");
            controller.Settings = DuelSceneTests.FastSettings();
        }

        // UQD-T67: the gamescreen drew no mode line.
        [UnityTest]
        public IEnumerator LocalPlayDrawsNoModeLine()
        {
            yield return null;
            Assert.That(session.ModeText, Is.Empty);
            Assert.That(controller.GetComponent<DuelView>().Root.Q("Mode").resolvedStyle.display,
                Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator TheAiStrikesAsTheRightPlayerWithinItsDifficultyRange()
        {
            session.SetMode(DuelMode.VsAiHard);
            Assert.That(controller.GetComponent<DuelInput>().RightIsHuman, Is.False);

            controller.BeginRound();
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved);

            var range = TuningStore.Profile.Ai.Hard;
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Right));
            Assert.That(controller.Result.RightReaction, Is.InRange(range.Min, range.Max));
            Assert.That(session.Match.WinsOf(Side.Right), Is.EqualTo(1));
            Assert.That(session.ScoreText(Side.Right), Does.StartWith("AI  1/2"));
            yield return null;
            var view = controller.GetComponent<DuelView>();
            Assert.That(view.WinsText, Is.EqualTo("Wins!"), "the original's banner says the AI won");
            Assert.That(view.WinsSide, Is.EqualTo(Side.Left), "on the loser's side");
            Assert.That(view.WinnerNameText, Is.EqualTo(view.FighterIn(Side.Right)?.Name ?? "AI"));
            Assert.That(view.HeadlineText, Is.Empty);
        }

        [UnityTest]
        public IEnumerator AFasterHumanBeatsTheAi()
        {
            session.SetMode(DuelMode.VsAiEasy);
            controller.BeginRound();
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            controller.Strike(Side.Left, controller.CueAt + 0.150);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved);

            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Left));
            Assert.That(session.Match.BestReactionOf(Side.Left), Is.EqualTo(0.150).Within(1e-9));
        }

        [UnityTest]
        public IEnumerator TwoWinsTakeTheMatchAndTheNextRoundStartsAFreshOne()
        {
            for (var round = 0; round < 2; round++)
            {
                controller.BeginRound();
                yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
                controller.Strike(Side.Left, controller.CueAt + 0.2 + round * 0.05);
                controller.Strike(Side.Right, controller.CueAt + 0.4);
            }

            Assert.That(session.Match.IsOver, Is.True);
            Assert.That(session.Match.Winner, Is.EqualTo(Side.Left));
            Assert.That(session.VerdictText, Is.EqualTo("Left takes the match 2-0"));
            Assert.That(session.ScoreText(Side.Left), Is.EqualTo("Left  2/2   best 200 ms"));
            for (var i = 0; i < 30; i++) yield return null;
            Screenshots.Capture("07-match");

            controller.BeginRound();
            Assert.That(session.Match.WinsOf(Side.Left), Is.EqualTo(0));
            Assert.That(session.VerdictText, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ModeCyclesOnlyBetweenRoundsAndResetsTheMatch()
        {
            Assert.That(session.Mode, Is.EqualTo(DuelMode.LocalPvp));
            controller.BeginRound();
            Assert.That(session.CycleMode(), Is.False);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Standoff);
            controller.Strike(Side.Left, controller.Now);   // early strike (during the standoff) ends the round
            Assert.That(session.Match.WinsOf(Side.Right), Is.EqualTo(1));

            Assert.That(session.CycleMode(), Is.True);
            Assert.That(session.Mode, Is.EqualTo(DuelMode.VsAiEasy));
            Assert.That(session.Match.WinsOf(Side.Right), Is.EqualTo(0));
            session.CycleMode();
            session.CycleMode();
            session.CycleMode();
            Assert.That(session.Mode, Is.EqualTo(DuelMode.LocalPvp));
            Assert.That(controller.GetComponent<DuelInput>().RightIsHuman, Is.True);
            yield return null;
        }
    }
}
