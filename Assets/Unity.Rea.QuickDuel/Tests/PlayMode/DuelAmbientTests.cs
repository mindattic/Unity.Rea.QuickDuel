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
    // The arenas' ambient emitters (UQD-T49): the arena on screen plays its effect, drawn at the original's depths, frozen
    // while the duel waits on the first strike.
    public class DuelAmbientTests
    {
        private DuelController controller;
        private DuelView view;
        private DuelAmbient ambient;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindFirstObjectByType<DuelController>();
            view = controller.GetComponent<DuelView>();
            ambient = controller.GetComponent<DuelAmbient>();
            Assert.That(ambient, Is.Not.Null, "Duel scene has no DuelAmbient.");
            var settings = DuelSceneTests.FastSettings();
            settings.StrikeTimeoutSeconds = 5;
            controller.Settings = settings;
        }

        [Test]
        public void TheSceneCarriesEveryEffectsSpritesAndEachArenasEffect()
        {
            foreach (AmbientKind kind in System.Enum.GetValues(typeof(AmbientKind)))
                foreach (var id in AmbientKinds.SpritesOf(kind))
                {
                    var look = ambient.LookOf(id);
                    Assert.That(look, Is.Not.Null, id);
                    Assert.That(look.Frames.All(f => f != null), id);
                }
            Assert.That(ambient.LookOf(AmbientField.Butterfly).Frames, Has.Count.EqualTo(6));
            Assert.That(ambient.LookOf(AmbientField.Sand1).Origin, Is.EqualTo(new Vector2(2, 2)));

            var arenas = view.Arenas.ToDictionary(a => a.Id, a => a.Ambient);
            Assert.That(arenas["desert"], Is.EqualTo(AmbientKind.Sand));
            Assert.That(arenas["pastelfields"], Is.EqualTo(AmbientKind.Butterfly));
            Assert.That(arenas["snow"], Is.EqualTo(AmbientKind.Snow));
            Assert.That(arenas["fumaroles"], Is.EqualTo(AmbientKind.Fog));
            Assert.That(arenas["aridplains"], Is.EqualTo(AmbientKind.Fly));
            Assert.That(arenas["dustyjunction"], Is.EqualTo(AmbientKind.None));
        }

        [UnityTest]
        public IEnumerator TheArenaOnScreenPlaysItsOwnEffect()
        {
            yield return null;
            Assert.That(view.Arena, Is.Not.Null);
            Assert.That(ambient.Field.Kind, Is.EqualTo(view.Arena.Ambient));
        }

        [Test]
        public void TheLayersKeepTheOriginalsDepths()
        {
            // Butterflies, flies and fireflies (depth 0) just behind the fighters, over the blood drops (1); sand, snow, fog,
            // rain and leaves (-1) over the fighters and under the splatters (-100).
            var stage = view.Stage;
            var fighter = view.FighterElement(Side.Left).parent;
            Assert.That(stage.IndexOf(ambient.BackLayer), Is.EqualTo(stage.IndexOf(fighter) - 1));
            var blood = controller.GetComponent<DuelBlood>();
            Assert.That(stage.IndexOf(blood.DropLayer), Is.LessThan(stage.IndexOf(ambient.BackLayer)));
            var root = view.Root;
            Assert.That(root.IndexOf(ambient.FrontLayer), Is.EqualTo(root.IndexOf(view.Stage) + 1));
            Assert.That(root.IndexOf(blood.SplatterLayer), Is.GreaterThan(root.IndexOf(ambient.FrontLayer)));
        }

        [UnityTest]
        public IEnumerator EveryEffectDrawsOnItsLayer()
        {
            foreach (AmbientKind kind in System.Enum.GetValues(typeof(AmbientKind)))
            {
                ambient.Override = kind;
                yield return null;
                Assert.That(ambient.Field.Kind, Is.EqualTo(kind));
                yield return new WaitForSecondsRealtime(kind == AmbientKind.Snow || kind == AmbientKind.Leaf ? 2.5f : 0.6f);
                yield return null;
                var front = AmbientKinds.InFront(kind);
                var drawn = front ? ambient.FrontDrawn : ambient.BackDrawn;
                var other = front ? ambient.BackDrawn : ambient.FrontDrawn;
                if (kind == AmbientKind.None)
                {
                    Assert.That(ambient.Field.Particles, Is.Empty);
                    Assert.That(ambient.FrontDrawn + ambient.BackDrawn, Is.Zero);
                    continue;
                }
                Assert.That(ambient.Field.Particles, Is.Not.Empty, kind.ToString());
                Assert.That(drawn, Is.GreaterThan(0), kind + " drew nothing");
                Assert.That(other, Is.Zero, kind + " drew on the wrong layer");
            }
            ambient.Override = null;
            yield return null;
            Assert.That(ambient.Field.Kind, Is.EqualTo(view.Arena.Ambient));
        }

        // The panel drawn into a texture: every effect puts its particles on screen over the same still arena.
        [UnityTest]
        public IEnumerator EveryEffectIsDrawnOnScreen()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device to draw with.");

            var document = controller.GetComponentInChildren<UIDocument>();
            var target = new RenderTexture(960, 540, 0, RenderTextureFormat.ARGB32);
            var panel = Object.Instantiate(document.panelSettings);
            panel.targetTexture = target;
            document.panelSettings = panel;
            ambient.Override = AmbientKind.None;
            yield return new WaitForSecondsRealtime(1f);
            var still = Read(target);

            foreach (AmbientKind kind in System.Enum.GetValues(typeof(AmbientKind)))
            {
                if (kind == AmbientKind.None) continue;
                ambient.Override = kind;
                yield return new WaitForSecondsRealtime(kind == AmbientKind.Snow || kind == AmbientKind.Leaf ? 3f : 1f);
                var pixels = Read(target);
                Screenshots.Save("12-ambient-" + AmbientKinds.NameOf(kind), pixels);
                var changed = Changed(still, pixels);
                // A fly or a firefly is one original pixel; everything else covers far more.
                var least = kind == AmbientKind.Fly || kind == AmbientKind.Firefly ? 8 : 200;
                Assert.That(changed, Is.GreaterThan(least), $"{kind} changed {changed} pixels");
                Object.Destroy(pixels);
            }
            Object.Destroy(still);
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

        private static int Changed(Texture2D a, Texture2D b)
        {
            var pa = a.GetPixels32();
            var pb = b.GetPixels32();
            var n = 0;
            for (var i = 0; i < pa.Length; i++)
                if (System.Math.Abs(pa[i].r - pb[i].r) + System.Math.Abs(pa[i].g - pb[i].g) + System.Math.Abs(pa[i].b - pb[i].b) > 30)
                    n++;
            return n;
        }

        [UnityTest]
        public IEnumerator NothingMovesWhileTheDuelWaitsOnTheFirstStrike()
        {
            ambient.Override = AmbientKind.Snow;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            yield return null;
            Assert.That(ambient.Frozen, Is.True);
            var created = ambient.Field.Created;
            var before = ambient.Field.Particles.Select(p => (p.X, p.Y)).ToList();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(ambient.Field.Created, Is.EqualTo(created));
            Assert.That(ambient.Field.Particles.Select(p => (p.X, p.Y)), Is.EqualTo(before));

            controller.Strike(Side.Left, controller.CueAt + 0.15);
            controller.Strike(Side.Right, controller.CueAt + 0.25);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(ambient.Frozen, Is.False);
            Assert.That(ambient.Field.Created, Is.GreaterThan(created), "the snow falls on after the cut");
        }
    }
}
