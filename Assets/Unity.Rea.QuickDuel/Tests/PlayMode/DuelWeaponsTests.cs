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
    // obj_weapon (UQD-T41): the loser's weapon thrown from the cut, landing with a hit-ground sound.
    public class DuelWeaponsTests
    {
        private DuelController controller;
        private DuelView view;
        private DuelWeapons weapons;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindFirstObjectByType<DuelController>();
            view = controller.GetComponent<DuelView>();
            weapons = controller.GetComponent<DuelWeapons>();
            Assert.That(weapons, Is.Not.Null, "Duel scene has no DuelWeapons.");
            var settings = DuelSceneTests.FastSettings();
            settings.StrikeTimeoutSeconds = 5;
            controller.Settings = settings;
        }

        [Test]
        public void TheSceneCarriesTheShieldAndTheFourHitGroundSounds()
        {
            Assert.That(weapons.Shield, Is.Not.Null);
            Assert.That(weapons.ShieldGround, Is.Not.Null);
            Assert.That(weapons.HitGround, Has.Count.EqualTo(4));
            Assert.That(weapons.HitGround.All(c => c != null));
            Assert.That(view.Fighters.All(f => f.Weapon != null && f.GroundWeapon != null));
        }

        [UnityTest]
        public IEnumerator TheLosersWeaponIsThrownSpinsAndLandsWithASound()
        {
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            Assert.That(weapons.Toss, Is.Null, "nothing thrown before the cut");
            var cue = controller.CueAt;
            controller.Strike(Side.Left, cue + 0.15);
            controller.Strike(Side.Right, cue + 0.25);
            Assert.That(controller.Phase, Is.EqualTo(DuelPhase.Resolved));

            var toss = weapons.Toss;
            Assert.That(toss, Is.Not.Null);
            var loser = view.FighterIn(Side.Right);
            Assert.That(toss.LoserSide, Is.EqualTo(Side.Right));
            Assert.That(toss.Loser, Is.EqualTo(loser.Id));
            Assert.That(toss.Pieces, Has.Count.EqualTo(WeaponToss.PiecesOf(loser.Id).Count));
            var front = toss.Pieces.Count - 1;
            Assert.That(weapons.SpriteOf(front), Is.SameAs(loser.Weapon));
            // Depth -1: over the fighters and the text on the stage.
            Assert.That(view.Stage.IndexOf(weapons.Layer), Is.EqualTo(view.Stage.childCount - 1));
            Assert.That(weapons.ElementOf(front).style.scale.value.value.x, Is.EqualTo(-1f), "a right loser's weapon is mirrored");

            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(toss.Pieces[front].Angle, Is.Not.EqualTo(0), "it spins in the air");
            Assert.That(toss.Pieces[front].X, Is.LessThan(WeaponToss.OriginX), "the left won: thrown to the left");

            yield return DuelSceneTests.WaitUntil(() => toss.Landed, 4f);
            yield return null;
            Assert.That(weapons.Played, Has.Count.EqualTo(toss.Pieces.Count), "one hit-ground sound a piece");
            Assert.That(weapons.Played.All(s => s >= 0 && s < 4));
            Assert.That(weapons.SpriteOf(front), Is.SameAs(loser.GroundWeapon));
            Assert.That(toss.Pieces.All(p => p.Angle == 0 && p.Y >= p.LandY));

            // The next round's fade destroys every obj_weapon.
            Assert.That(controller.BeginRound(), Is.True);
            yield return null;
            Assert.That(weapons.Toss, Is.Null);
            Assert.That(weapons.ElementOf(0).style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        // The panel drawn into a texture once the weapon has landed, with its layer shown and hidden: the weapon puts
        // pixels on screen.
        [UnityTest]
        public IEnumerator TheWeaponIsDrawnOnScreen()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device to draw with.");

            var document = controller.GetComponentInChildren<UIDocument>();
            var target = new RenderTexture(960, 540, 0, RenderTextureFormat.ARGB32);
            var panel = Object.Instantiate(document.panelSettings);
            panel.targetTexture = target;
            document.panelSettings = panel;

            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Strike);
            controller.Strike(Side.Right, controller.CueAt + 0.15);
            controller.Strike(Side.Left, controller.CueAt + 0.25);
            yield return new WaitForSecondsRealtime(0.25f);
            var flying = Read(target);
            Screenshots.Save("08-weapon-thrown", flying);
            yield return DuelSceneTests.WaitUntil(() => weapons.Toss.Landed, 4f);
            for (var i = 0; i < 3; i++) yield return null;
            var shown = Read(target);
            Screenshots.Save("08-weapon-landed", shown);
            weapons.Layer.style.display = DisplayStyle.None;
            for (var i = 0; i < 3; i++) yield return null;
            var hidden = Read(target);
            var a = shown.GetPixels32();
            var b = hidden.GetPixels32();
            var differ = 0;
            for (var i = 0; i < a.Length; i++)
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 30)
                    differ++;
            Assert.That(differ, Is.GreaterThan(200), $"{differ} pixels differ with the weapon shown and hidden");

            Object.Destroy(flying);
            Object.Destroy(shown);
            Object.Destroy(hidden);
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

        [UnityTest]
        public IEnumerator ARoundNobodyStruckThrowsNothing()
        {
            var settings = DuelSceneTests.FastSettings();
            settings.StrikeTimeoutSeconds = 0.2;
            controller.Settings = settings;
            Assert.That(controller.BeginRound(), Is.True);
            yield return DuelSceneTests.WaitUntil(() => controller.Phase == DuelPhase.Resolved);
            Assert.That(controller.Result.Outcome, Is.EqualTo(DuelOutcome.NoStrike));
            Assert.That(weapons.Toss, Is.Null);
            Assert.That(weapons.Played, Is.Empty);
        }
    }
}
