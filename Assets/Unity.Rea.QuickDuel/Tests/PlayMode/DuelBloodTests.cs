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
    // gameclock_aftermath's blood (UQD-T40): the emitter's drops, the splatters and the white flash.
    public class DuelBloodTests
    {
        private DuelController controller;
        private DuelView view;
        private DuelBlood blood;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindFirstObjectByType<DuelController>();
            view = controller.GetComponent<DuelView>();
            blood = controller.GetComponent<DuelBlood>();
            Assert.That(blood, Is.Not.Null, "Duel scene has no DuelBlood.");
            var settings = DuelSceneTests.FastSettings();
            settings.StrikeTimeoutSeconds = 5;
            controller.Settings = settings;
        }

        [Test]
        public void TheSceneCarriesTheOriginalDropsSplattersAndFlash()
        {
            Assert.That(blood.Drops, Has.Count.EqualTo(2));
            Assert.That(blood.Drops.All(s => s != null));
            Assert.That(blood.Splatters, Has.Count.EqualTo(10));
            Assert.That(blood.Splatters.All(s => s.Sprite != null));
            Assert.That(blood.Flash, Is.Not.Null);
            // spr_splatter1's bbox_left 3, bbox_top 19, bbox_right 90, bbox_bottom 77; spr_splatter4's 0, 8, 95, 85.
            Assert.That(blood.Splatters[0].Bounds, Is.EqualTo(new RectInt(3, 19, 88, 59)));
            Assert.That(blood.Splatters[3].Bounds, Is.EqualTo(new RectInt(0, 8, 96, 78)));
        }

        [Test]
        public void TheLayersKeepTheOriginalsDepths()
        {
            // Drops (depth 1) behind the fighters and text (0); splatters (-100), then the flash (-1000), over them; the
            // letterbox (-10000) over all.
            Assert.That(view.Stage.IndexOf(blood.DropLayer), Is.EqualTo(0));
            var root = view.Root;
            var stage = root.IndexOf(view.Stage);
            // The ambient layer over the fighters (depth -1, UQD-T49) sits between the stage and the splatters.
            var splatters = root.IndexOf(blood.SplatterLayer);
            Assert.That(splatters, Is.EqualTo(stage + 2));
            Assert.That(root.IndexOf(blood.FlashBox), Is.EqualTo(splatters + 1));
            Assert.That(root.IndexOf(root.Q("LetterboxTop")), Is.GreaterThan(splatters + 1));
            Assert.That(blood.SplatterLayer.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(blood.FlashBox.pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        [UnityTest]
        public IEnumerator AStrikeSpillsBloodSplattersAndAFlash()
        {
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            Assert.That(blood.Spray, Is.Null, "no blood before the cut");
            Assert.That(blood.Field.Splatters, Is.Empty);
            var cue = controller.CueAt;
            controller.Strike(Side.Left, cue + 0.15);
            controller.Strike(Side.Right, cue + 0.25);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Resolved));

            Assert.That(blood.FlashAlpha, Is.EqualTo(0.6f).Within(1e-4f), "the flash's first step");
            Assert.That(blood.Spray.Emitting, Is.True);
            Assert.That(blood.Field.Splatters.Count, Is.InRange(6, 12));
            foreach (var splatter in blood.Field.Splatters)
            {
                var element = blood.ElementOf(splatter);
                Assert.That(element, Is.Not.Null);
                Assert.That(element.style.opacity.value, Is.EqualTo(0.95f).Within(0.01f));
            }

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(blood.FlashAlpha, Is.EqualTo(0f), "ten blinks in a third of a second");
            Assert.That(blood.Spray.Spurted, Is.GreaterThan(10), "a drop every 2 steps");
            Assert.That(blood.DropsDrawn, Is.EqualTo(blood.Spray.Drops.Count));
            Assert.That(blood.DropsDrawn, Is.GreaterThan(0));
            Screenshots.Capture("07-blood");

            // A click on a splatter's bounding box wipes it quickly; one beside it does not.
            yield return null;
            var target = blood.Field.Splatters[0];
            var box = blood.HitBox(target);
            Assert.That(box.width, Is.GreaterThan(0f));
            blood.Touch(box.center, held: true, pressed: true);
            Assert.That(target.Wiped, Is.True);
            var others = blood.Field.Splatters.Where(s => s != target && !s.Wiped).ToList();
            blood.Touch(new Vector2(-10000f, -10000f), held: true, pressed: true);
            Assert.That(others.All(s => !s.Wiped), "a click on nothing wipes nothing");
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(blood.Field.Splatters, Has.No.Member(target), "wiped at 0.01 a step");
            Assert.That(blood.ElementOf(target), Is.Null);

            // The next round's fade destroys the emitter; the splatters stay, fading on their own.
            var spray = blood.Spray;
            Assert.That(controller.BeginRound(), Is.True);
            yield return null;
            Assert.That(spray.Emitting, Is.False);
            Assert.That(blood.Field.Splatters, Is.Not.Empty, "nothing but their fade removes splatters");
        }

        [UnityTest]
        public IEnumerator ARoundNobodyStruckSpillsNothing()
        {
            var settings = DuelSceneTests.FastSettings();
            settings.StrikeTimeoutSeconds = 0.2;
            controller.Settings = settings;
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved);
            Assert.That(controller.Result.Outcome, Is.EqualTo(DuelOutcome.NoStrike));
            Assert.That(blood.Spray, Is.Null);
            Assert.That(blood.Field.Splatters, Is.Empty);
            Assert.That(blood.FlashAlpha, Is.EqualTo(0f));
        }

        // The panel drawn into a texture, before and after the cut: the splatters put blood-red pixels on screen.
        [UnityTest]
        public IEnumerator TheBloodIsDrawnOnScreen()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device to draw with.");

            var document = controller.GetComponentInChildren<UIDocument>();
            var target = new RenderTexture(960, 540, 0, RenderTextureFormat.ARGB32);
            var panel = Object.Instantiate(document.panelSettings);
            panel.targetTexture = target;
            document.panelSettings = panel;
            for (var i = 0; i < 3; i++) yield return null;
            var before = BloodRed(Read(target));

            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            controller.Strike(Side.Left, controller.CueAt + 0.15);
            controller.Strike(Side.Right, controller.CueAt + 0.25);
            yield return new WaitForSecondsRealtime(0.5f);
            var pixels = Read(target);
            Screenshots.Save("07-blood", pixels);
            var after = BloodRed(pixels);
            Assert.That(after, Is.GreaterThan(before + 500), $"blood-red pixels {before} before the cut, {after} after");

            Object.Destroy(pixels);
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

        private static int BloodRed(Texture2D pixels)
        {
            var n = 0;
            foreach (var p in pixels.GetPixels32())
                if (p.r > 90 && p.g < 40 && p.b < 40 && p.r > 2 * (p.g + p.b))
                    n++;
            return n;
        }
    }
}
