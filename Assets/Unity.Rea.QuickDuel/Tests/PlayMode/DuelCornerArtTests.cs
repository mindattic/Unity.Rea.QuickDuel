using System.Collections;
using NUnit.Framework;
using Unity.Rea.QuickDuel.Core;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    // obj_cornerart_left and obj_cornerart_right (UQD-T43): on start both corner arts slide in from off the screen, closing
    // 1 + 0.05 of the distance left a step, until they are in place. The controller's clock is held and stepped by hand
    // from the moment the scene started, so each step is looked at on its own.
    public class DuelCornerArtTests
    {
        private DuelController controller;
        private DuelView view;
        private DuelCornerArt corners;
        private double now;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Duel", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<DuelController>();
            view = controller.GetComponent<DuelView>();
            corners = controller.GetComponent<DuelCornerArt>();
            Assert.That(corners, Is.Not.Null, "Duel scene has no DuelCornerArt.");
            now = corners.StartedAt;
            controller.Clock = () => now;
        }

        private IEnumerator At(int tick)
        {
            now = corners.StartedAt + (tick + 0.5) / CornerArt.TicksPerSecond;
            yield return null;
            yield return null;
        }

        // The original's 480-high room on the 1080-high stage.
        private const float PixelScale = 1080f / 480f;

        private static float Px(double originalPixels) => (float)originalPixels * PixelScale;

        [Test]
        public void TheSceneCarriesTheTwoCornerArtsTheOriginalDrew()
        {
            Assert.That(corners.LeftArt, Is.Not.Null);
            Assert.That(corners.RightArt, Is.Not.Null);
            Assert.That(corners.LeftArt.name, Does.StartWith("spr_cornerart_left2"));
            Assert.That(corners.RightArt.name, Does.StartWith("spr_cornerart_right2"));
            Assert.That(corners.Left.style.backgroundImage.value.sprite, Is.SameAs(corners.LeftArt));
            Assert.That(corners.Right.style.backgroundImage.value.sprite, Is.SameAs(corners.RightArt));
            Assert.That(corners.Left.style.width.value.value, Is.EqualTo(Px(CornerArt.SpriteWidth)));
            Assert.That(corners.Left.style.height.value.value, Is.EqualTo(Px(CornerArt.SpriteHeight)));
        }

        [Test]
        public void TheArtsDrawOverTheRoomAndUnderTheLetterbox()
        {
            var root = view.Root;
            Assert.That(corners.Left.parent, Is.SameAs(root));
            Assert.That(corners.Right.parent, Is.SameAs(root));
            var blood = controller.GetComponent<DuelBlood>();
            Assert.That(root.IndexOf(corners.Left), Is.GreaterThan(root.IndexOf(view.Stage)));
            Assert.That(root.IndexOf(corners.Left), Is.GreaterThan(root.IndexOf(blood.SplatterLayer)), "over the splatters (-100)");
            Assert.That(root.IndexOf(corners.Left), Is.GreaterThan(root.IndexOf(blood.FlashBox)), "Draw GUI, over the flash");
            Assert.That(root.IndexOf(corners.Right), Is.LessThan(root.IndexOf(view.LetterboxTop)), "under the letterbox (-10000)");
        }

        [UnityTest]
        public IEnumerator BothArtsSlideInFromOffTheScreenUntilInPlace()
        {
            yield return At(0);
            Assert.That(corners.LeftX, Is.EqualTo(-152 + 1 + 152 * 0.05).Within(1e-9), "one step before the first draw");
            Assert.That(corners.RightX, Is.EqualTo(640 - 1 - 152 * 0.05).Within(1e-9));
            Assert.That(corners.InPosition, Is.False);
            Assert.That(corners.Left.style.left.value.value, Is.EqualTo(Px(corners.LeftX)).Within(1e-3f));
            Assert.That(corners.Right.style.right.value.value, Is.EqualTo(Px(488 - corners.RightX)).Within(1e-3f));

            yield return At(10);
            Assert.That(corners.LeftX, Is.EqualTo(CornerArt.LeftXAt(10)));
            Assert.That(corners.LeftX, Is.LessThan(0));
            Assert.That(corners.RightX - 488, Is.EqualTo(-corners.LeftX).Within(1e-9), "mirror images");
            Screenshots.Capture("corner-art-sliding");

            yield return At(CornerArt.InPositionSteps - 1);
            Assert.That(corners.InPosition, Is.True);
            Assert.That(corners.LeftX, Is.Zero);
            Assert.That(corners.RightX, Is.EqualTo(488));
            Assert.That(corners.Left.style.left.value.value, Is.Zero);
            Assert.That(corners.Right.style.right.value.value, Is.Zero);

            yield return At(600);
            Assert.That(corners.LeftX, Is.Zero, "it stays put");
            Screenshots.Capture("corner-art-in-place");
        }
    }
}
