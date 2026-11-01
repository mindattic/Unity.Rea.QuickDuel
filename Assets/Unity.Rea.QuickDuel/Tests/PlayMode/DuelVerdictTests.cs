using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    // The match verdict (UQD-T48): a finished match cuts from the loading fade's full image to the original's victory or
    // gameover room, whose one button starts a new match or goes back to the main menu.
    public class DuelVerdictTests
    {
        private DuelController controller;
        private DuelSession session;
        private DuelFade fade;
        private DuelVerdict verdict;
        private readonly List<string> left = new List<string>();
        private double now;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            DuelModeHandoff.Clear();
            LoadingHandoff.Clear();
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<DuelController>();
            session = controller.GetComponent<DuelSession>();
            fade = controller.GetComponent<DuelFade>();
            verdict = controller.GetComponent<DuelVerdict>();
            Assert.That(verdict, Is.Not.Null, "Duel scene has no DuelVerdict.");
            controller.Settings = DuelSceneTests.FastSettings();
            left.Clear();
            verdict.Leave = left.Add;
            verdict.Pointer = () => null;
        }

        [TearDown]
        public void ForgetHandoffs()
        {
            DuelModeHandoff.Clear();
            LoadingHandoff.Clear();
        }

        [Test]
        public void TheSceneCarriesTheVerdictArt()
        {
            Assert.That(verdict.VictoryBackground.name, Does.StartWith("bg_victory"));
            Assert.That(verdict.GameOverBackground.name, Does.StartWith("bg_gameover"));
            Assert.That(verdict.Button.name, Does.StartWith("spr_btn_right4"));
            Assert.That(verdict.ButtonOver.name, Does.StartWith("spr_btn_right4_over"));
            Assert.That(verdict.Click, Is.Not.Null);
            Assert.That((verdict.MenuScene, verdict.DuelScene), Is.EqualTo(("MainMenu", "Duel")));
        }

        // A round the left duelist takes by reaction, before the computer (Normal, 300 ms at the soonest) strikes.
        private IEnumerator LeftWinsARound()
        {
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            controller.Strike(Side.Left, controller.CueAt + 0.15);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved);
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Left));
        }

        // A round the computer takes: the left duelist never strikes.
        private IEnumerator TheComputerWinsARound()
        {
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved);
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Right));
        }

        // Holds the clock at `tick` steps after the last round's cut.
        private IEnumerator HoldAt(int tick)
        {
            controller.Clock = () => now;
            now = controller.EndedAt.Value + (tick + 0.5) / RoundFade.TicksPerSecond;
            yield return null;
            yield return null;
        }

        // obj_player (UQD-T64): a loser with HP left is held on the first defeat frame; only the killing blow, here the
        // round that decides the match, plays the whole strip, and its Animation End destroys the blood emitter.
        [UnityTest]
        public IEnumerator OnlyTheKillingBlowPlaysTheWholeDefeatStripAndEndsTheBlood()
        {
            var view = controller.GetComponent<DuelView>();
            var blood = controller.GetComponent<DuelBlood>();
            Assert.That(session.SetMode(DuelMode.VsAiNormal), Is.True);
            yield return LeftWinsARound();
            Assert.That(view.IsKillingBlow, Is.False);
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(view.Motion(Side.Right).Pose, Is.EqualTo(FighterPose.Defeated));
            Assert.That(view.Motion(Side.Right).Frame, Is.Zero, "held: the match goes on");
            Assert.That(blood.Spray.Emitting, Is.True, "the blood runs on until the loading fade");

            yield return LeftWinsARound();
            Assert.That(view.IsKillingBlow, Is.True);
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(view.Motion(Side.Right).Frame, Is.EqualTo(FighterMotion.StripFrames - 1), "the killing blow plays through");
            Assert.That(blood.Spray.Emitting, Is.False, "the strip's end destroyed the emitter");
        }

        [UnityTest]
        public IEnumerator AWonMatchCutsToVictoryUnderTheFullImageAndContinueGoesToTheMenu()
        {
            Assert.That(session.SetMode(DuelMode.VsAiNormal), Is.True);
            yield return LeftWinsARound();
            yield return HoldAt(RoundFade.ResetTick);
            Assert.That(verdict.Shown, Is.False, "one round won is not the match");
            controller.Clock = () => Time.realtimeSinceStartupAsDouble;
            yield return LeftWinsARound();
            Assert.That(controller.Match.IsOver, Is.True);

            yield return HoldAt(RoundFade.ResetTick - 1);
            Assert.That(verdict.Shown, Is.False, "the aftermath and the fade play first");
            var image = fade.Image;

            yield return HoldAt(RoundFade.ResetTick);
            Assert.That(verdict.Shown, Is.True);
            Assert.That(verdict.Screen.Kind, Is.EqualTo(VerdictKind.Victory));
            Assert.That(verdict.Background.style.backgroundImage.value.sprite, Is.SameAs(verdict.VictoryBackground));
            Assert.That(verdict.HeadingText, Is.EqualTo("Victory"));
            Assert.That(verdict.ResultText, Is.EqualTo("Left takes the match 2-0"));
            Assert.That(verdict.FastestText, Is.EqualTo("Fastest Reaction  150 ms (Left)"));
            Assert.That(verdict.ButtonText, Is.EqualTo("Continue"));
            Assert.That(verdict.ButtonElement.style.backgroundImage.value.sprite, Is.SameAs(verdict.Button));
            Assert.That(verdict.Overlay.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(fade.Overlay.style.display.value, Is.EqualTo(DisplayStyle.None), "a cut: the image goes with the room");
            var root = verdict.Overlay.parent;
            for (var i = root.IndexOf(verdict.Overlay) + 1; i < root.childCount; i++)
                Assert.That(root[i].name, Is.EqualTo("KatanaCursor"), "over everything but the katana");
            Screenshots.Capture("verdict-victory");

            // Tab no longer changes the mode under the verdict: its button decides what follows.
            Assert.That(session.CycleMode(), Is.False);

            // Nor does Escape leave: the original's victory and gameover rooms had no Escape (UQD-T65).
            var quit = controller.GetComponent<DuelQuit>();
            var escaped = new List<string>();
            quit.Leave = escaped.Add;
            Assert.That(quit.TryLeave(), Is.False);
            Assert.That(escaped, Is.Empty);

            verdict.Pointer = () => new Vector2(600, 450);
            yield return null;
            Assert.That(verdict.Screen.Hovered, Is.True);
            Assert.That(verdict.ButtonElement.style.backgroundImage.value.sprite, Is.SameAs(verdict.ButtonOver));

            verdict.Screen.Press(600, 450);
            yield return null;
            Assert.That(verdict.Played, Is.EqualTo(new[] { DuelVerdict.ClickCue }));
            Assert.That(left, Is.EqualTo(new[] { "MainMenu" }));
            Assert.That(LoadingHandoff.Pending, Is.EqualTo(image), "the menu opens under the image the verdict cut from");
        }

        [UnityTest]
        public IEnumerator ALostMatchIsGameOverAndRetryStartsANewMatchInTheSameMode()
        {
            Assert.That(session.SetMode(DuelMode.VsAiHard), Is.True);
            yield return TheComputerWinsARound();
            controller.Clock = () => Time.realtimeSinceStartupAsDouble;
            yield return TheComputerWinsARound();
            Assert.That(controller.Match.Winner, Is.EqualTo(Side.Right));

            yield return HoldAt(RoundFade.ResetTick);
            Assert.That(verdict.Shown, Is.True);
            Assert.That(verdict.Screen.Kind, Is.EqualTo(VerdictKind.GameOver));
            Assert.That(verdict.Background.style.backgroundImage.value.sprite, Is.SameAs(verdict.GameOverBackground));
            Assert.That(verdict.HeadingText, Is.EqualTo("Game Over"));
            Assert.That(verdict.ResultText, Is.EqualTo("The AI takes the match 2-0"));
            Assert.That(verdict.FastestText, Does.EndWith("(The AI)"));
            Assert.That(verdict.ButtonText, Is.EqualTo("Retry"));
            Screenshots.Capture("verdict-gameover");

            verdict.Screen.Activate();
            yield return null;
            Assert.That(left, Is.EqualTo(new[] { "Duel" }));
            Assert.That(DuelModeHandoff.Pending, Is.EqualTo(DuelMode.VsAiHard), "a new match in the same mode");
            Assert.That(LoadingHandoff.Pending, Is.Not.Null);
        }

        // The panel drawn into a texture: each verdict covers the duel with its own room.
        [UnityTest]
        public IEnumerator TheVerdictIsDrawnOnScreen()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device to draw with.");

            var document = controller.GetComponentInChildren<UIDocument>();
            var target = new RenderTexture(960, 540, 0, RenderTextureFormat.ARGB32);
            var panel = Object.Instantiate(document.panelSettings);
            panel.targetTexture = target;
            document.panelSettings = panel;
            for (var i = 0; i < 3; i++) yield return null;
            var duel = Read(target);

            verdict.Show(new VerdictScreen(VerdictKind.Victory, "Left takes the match 2-1", "Fastest Reaction  212 ms (Left)"));
            for (var i = 0; i < 3; i++) yield return null;
            var victory = Read(target);
            Screenshots.Save("10-verdict-victory", victory);

            verdict.Show(new VerdictScreen(VerdictKind.GameOver, "The AI takes the match 2-1", "Fastest Reaction  198 ms (The AI)"));
            verdict.Pointer = () => new Vector2(600, 450);
            for (var i = 0; i < 3; i++) yield return null;
            var gameOver = Read(target);
            Screenshots.Save("10-verdict-gameover", gameOver);

            Assert.That(Changed(duel, victory), Is.GreaterThan(0.5), "the victory room covers the duel");
            Assert.That(Changed(victory, gameOver), Is.GreaterThan(0.3), "the gameover room is another picture");

            Object.Destroy(duel);
            Object.Destroy(victory);
            Object.Destroy(gameOver);
            target.Release();
        }

        private static Texture2D Read(RenderTexture target)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            return pixels;
        }

        // The share of pixels that differ by more than a little between two captures.
        private static double Changed(Texture2D a, Texture2D b)
        {
            var pa = a.GetPixels32();
            var pb = b.GetPixels32();
            var n = 0;
            for (var i = 0; i < pa.Length; i++)
                if (Mathf.Abs(pa[i].r - pb[i].r) + Mathf.Abs(pa[i].g - pb[i].g) + Mathf.Abs(pa[i].b - pb[i].b) > 24)
                    n++;
            return (double)n / pa.Length;
        }

        [UnityTest]
        public IEnumerator AVersusMatchEndsInVictoryForWhicheverPlayerWon()
        {
            Assert.That(session.SetMode(DuelMode.LocalPvp), Is.True);
            for (var round = 0; round < 2; round++)
            {
                controller.Clock = () => Time.realtimeSinceStartupAsDouble;
                Assert.That(controller.BeginRound(), Is.True);
                yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
                var cue = controller.CueAt;
                controller.Strike(Side.Right, cue + 0.2);
                controller.Strike(Side.Left, cue + 0.3);
            }
            yield return HoldAt(RoundFade.ResetTick);
            Assert.That(verdict.Screen.Kind, Is.EqualTo(VerdictKind.Victory));
            Assert.That(verdict.ResultText, Is.EqualTo("Right takes the match 2-0"));
        }
    }
}
