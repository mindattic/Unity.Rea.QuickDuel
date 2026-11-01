using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    // The scripted end-to-end play-through (UQD-T54): the game is walked the way a player meets it (every mode, a clean
    // win, a premature strike, a round nobody strikes in, and a won and a lost match's verdict) and each stage is drawn
    // through its panel into a 1440 x 1080 texture (the original room at the game's 3x scale) and checked for a real
    // picture. With QUICKDUEL_SHOTS set the pictures are saved as playthrough-<stage>.png; the committed copies are in
    // docs/parity. The intro and menu stages are ParitySceneTests'.
    public class PlaythroughTests
    {
        private const int Width = 1440;
        private const int Height = 1080;

        private RenderTexture target;
        private PanelSettings clone;
        private DuelController controller;
        private DuelSession session;
        private DuelVerdict verdict;
        private double now;

        [UnitySetUp]
        public IEnumerator LoadDuel()
        {
            DuelModeHandoff.Clear();
            LoadingHandoff.Clear();
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<DuelController>();
            Assert.That(controller, Is.Not.Null);
            session = controller.GetComponent<DuelSession>();
            verdict = controller.GetComponent<DuelVerdict>();
            controller.Settings = DuelSceneTests.FastSettings();
            verdict.Leave = _ => { };
            verdict.Pointer = () => null;

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                if (System.Environment.GetEnvironmentVariable("QUICKDUEL_ALLOW_NO_GRAPHICS") == "1")
                    Assert.Ignore("No graphics device to draw with (QUICKDUEL_ALLOW_NO_GRAPHICS=1).");
                Assert.Fail("No graphics device to draw with: run the PlayMode tests without -nographics (or set QUICKDUEL_ALLOW_NO_GRAPHICS=1).");
            }
            var document = controller.GetComponentInChildren<UIDocument>();
            Assert.That(document, Is.Not.Null, "the scene draws no UI document");
            target = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            clone = Object.Instantiate(document.panelSettings);
            clone.targetTexture = target;
            document.panelSettings = clone;
        }

        [TearDown]
        public void Release()
        {
            LoadingHandoff.Clear();
            DuelModeHandoff.Clear();
            if (clone != null) { clone.targetTexture = null; Object.Destroy(clone); clone = null; }
            if (target != null) { target.Release(); Object.Destroy(target); target = null; }
        }

        private static IEnumerator Frames(int count = 4)
        {
            for (var i = 0; i < count; i++) yield return null;
        }

        private void Shoot(string stage)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            Screenshots.Save("playthrough-" + stage, pixels);
            var all = pixels.GetPixels32();
            var lit = all.Count(p => p.r + p.g + p.b > 30);
            var colours = all.Select(p => (p.r >> 4) << 8 | (p.g >> 4) << 4 | (p.b >> 4)).Distinct().Count();
            Object.Destroy(pixels);
            Assert.That(lit, Is.GreaterThan(all.Length / 10), $"{stage}: only {lit} of {all.Length} pixels are lit");
            Assert.That(colours, Is.GreaterThan(12), $"{stage}: only {colours} distinct colours");
        }

        private IEnumerator HoldAt(int tick)
        {
            controller.Clock = () => now;
            now = controller.EndedAt.Value + (tick + 0.5) / RoundFade.TicksPerSecond;
            yield return Frames(2);
        }

        private IEnumerator LeftWinsARound()
        {
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            controller.Strike(Side.Left, controller.CueAt + 0.15);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved);
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Left));
        }

        private IEnumerator TheComputerWinsARound()
        {
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved);
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Right));
        }

        [UnityTest]
        public IEnumerator EveryModeIsPlayableAndDrawn(
            [Values(DuelMode.LocalPvp, DuelMode.VsAiEasy, DuelMode.VsAiNormal, DuelMode.VsAiHard)] DuelMode mode)
        {
            Assert.That(session.SetMode(mode), Is.True);
            Assert.That(session.Mode, Is.EqualTo(mode));
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Countdown);
            yield return Frames();
            Assert.That(session.ScoreText(Side.Right), Does.StartWith(mode == DuelMode.LocalPvp ? "Right" : "AI"));
            Shoot("mode-" + mode);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            Assert.That(session.AiPlaysRight, Is.EqualTo(mode != DuelMode.LocalPvp));
            controller.Strike(Side.Left, controller.CueAt + 0.10);
            if (mode == DuelMode.LocalPvp) controller.Strike(Side.Right, controller.CueAt + 0.25);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved);
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Left));
        }

        [UnityTest]
        public IEnumerator ACleanWinIsDrawn()
        {
            Assert.That(session.SetMode(DuelMode.VsAiNormal), Is.True);
            yield return LeftWinsARound();
            Assert.That(controller.Result.Outcome, Is.EqualTo(DuelOutcome.FasterStrike));
            yield return Frames(10);
            Shoot("clean-win");
        }

        [UnityTest]
        public IEnumerator APrematureStrikeLosesAndIsDrawn()
        {
            Assert.That(session.SetMode(DuelMode.VsAiNormal), Is.True);
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Standoff);
            controller.Strike(Side.Left, controller.Now);
            Assert.That(controller.Result.Outcome, Is.EqualTo(DuelOutcome.EarlyStrike));
            Assert.That(controller.Result.Winner, Is.EqualTo(Side.Right));
            yield return Frames(10);
            Shoot("premature-strike");
        }

        [UnityTest]
        public IEnumerator ARoundNobodyStrikesInIsDrawn()
        {
            Assert.That(session.SetMode(DuelMode.LocalPvp), Is.True);
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved, 5f);
            Assert.That(controller.Result.Outcome, Is.EqualTo(DuelOutcome.NoStrike));
            yield return Frames(10);
            Shoot("no-strike");
        }

        [UnityTest]
        public IEnumerator AWonMatchEndsOnTheVictoryScreen()
        {
            Assert.That(session.SetMode(DuelMode.VsAiNormal), Is.True);
            yield return LeftWinsARound();
            yield return HoldAt(RoundFade.ResetTick);
            controller.Clock = () => Time.realtimeSinceStartupAsDouble;
            yield return LeftWinsARound();
            Assert.That(controller.Match.IsOver, Is.True);
            yield return HoldAt(RoundFade.ResetTick);
            Assert.That(verdict.Shown, Is.True);
            Assert.That(verdict.Screen.Kind, Is.EqualTo(VerdictKind.Victory));
            yield return Frames();
            Shoot("verdict-victory");
        }

        [UnityTest]
        public IEnumerator ALostMatchEndsOnTheGameOverScreen()
        {
            Assert.That(session.SetMode(DuelMode.VsAiNormal), Is.True);
            yield return TheComputerWinsARound();
            yield return HoldAt(RoundFade.ResetTick);
            controller.Clock = () => Time.realtimeSinceStartupAsDouble;
            yield return TheComputerWinsARound();
            Assert.That(controller.Match.IsOver, Is.True);
            yield return HoldAt(RoundFade.ResetTick);
            Assert.That(verdict.Shown, Is.True);
            Assert.That(verdict.Screen.Kind, Is.EqualTo(VerdictKind.GameOver));
            yield return Frames();
            Shoot("verdict-gameover");
        }
    }
}
