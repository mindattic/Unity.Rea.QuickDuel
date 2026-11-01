using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Rea.QuickDuel.Tests
{
    // obj_mouse (UQD-T46): spr_katana_cursor replaces the system cursor in the main menu and the duel, its tip at the
    // pointer, drawn at the original's pixel scale over the room and under the letterbox and loading fades, and hidden
    // while a gamepad is the device last used.
    public class KatanaCursorTests : InputTestFixture
    {
        private const float PixelScale = 1080f / 480f;

        [SetUp]
        public override void Setup()
        {
            base.Setup();
            KatanaCursor.ResetActiveDevice();
        }

        [TearDown]
        public override void TearDown()
        {
            KatanaCursor.ResetActiveDevice();
            LoadingHandoff.Clear();
            DuelModeHandoff.Clear();
            base.TearDown();
        }

        private static IEnumerator Load(string scene)
        {
            LoadingHandoff.Clear();
            DuelModeHandoff.Clear();
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static KatanaCursor Cursor()
        {
            var cursor = Object.FindAnyObjectByType<KatanaCursor>();
            Assert.That(cursor, Is.Not.Null, "The scene has no KatanaCursor.");
            Assert.That(cursor.Element, Is.Not.Null, "The katana was never drawn.");
            return cursor;
        }

        [UnityTest]
        public IEnumerator TheMenuAndTheDuelDrawTheKatanaInPlaceOfTheSystemCursor([Values("MainMenu", "Duel")] string scene)
        {
            InputSystem.AddDevice<Mouse>();
            yield return Load(scene);
            var cursor = Cursor();
            Assert.That(cursor.Sprite, Is.Not.Null);
            Assert.That(cursor.Sprite.name, Does.StartWith("spr_katana_cursor"));
            Assert.That(cursor.Element.style.backgroundImage.value.sprite, Is.SameAs(cursor.Sprite));
            Assert.That(cursor.Element.style.width.value.value, Is.EqualTo(40 * PixelScale));
            Assert.That(cursor.Element.style.height.value.value, Is.EqualTo(40 * PixelScale));
            Assert.That(cursor.Element.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(UnityEngine.Cursor.visible, Is.False, "the system cursor is hidden");
            Assert.That(cursor.Shown, Is.True);
        }

        [UnityTest]
        public IEnumerator TheBladesTipIsAtThePointer([Values("MainMenu", "Duel")] string scene)
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            yield return Load(scene);
            var cursor = Cursor();
            var screen = new Vector2(Screen.width * 0.3f, Screen.height * 0.6f);
            Set(mouse.position, screen);
            yield return null;
            yield return null;

            var root = cursor.GetComponent<IKatanaCursorHost>().Root;
            var expected = root.WorldToLocal(RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y)));
            Assert.That(cursor.Element.style.left.value.value, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(cursor.Element.style.top.value.value, Is.EqualTo(expected.y).Within(0.01f));
            Assert.That(cursor.Element.worldBound.position.x, Is.EqualTo(root.LocalToWorld(expected).x).Within(0.5f), "the sprite's origin, the tip, is the hotspot");
            Assert.That(cursor.Element.worldBound.position.y, Is.EqualTo(root.LocalToWorld(expected).y).Within(0.5f));
            Screenshots.Capture("katana-cursor-" + scene);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InTheDuelTheKatanaIsOverTheCornerArtsAndUnderTheLetterbox()
        {
            InputSystem.AddDevice<Mouse>();
            yield return Load("Duel");
            var cursor = Cursor();
            var view = cursor.GetComponent<DuelView>();
            var corners = cursor.GetComponent<DuelCornerArt>();
            var root = view.Root;
            Assert.That(root.IndexOf(cursor.Element), Is.EqualTo(root.IndexOf(view.LetterboxTop) - 1));
            Assert.That(root.IndexOf(cursor.Element), Is.GreaterThan(root.IndexOf(corners.Left)));
            Assert.That(root.IndexOf(cursor.Element), Is.GreaterThan(root.IndexOf(corners.Right)));
        }

        [UnityTest]
        public IEnumerator InTheMenuTheKatanaIsOverTheButtonsAndUnderTheLoadingFade()
        {
            InputSystem.AddDevice<Mouse>();
            yield return Load("MainMenu");
            var cursor = Cursor();
            var menu = cursor.GetComponent<MainMenuController>();
            var root = menu.Root;
            Assert.That(root.IndexOf(cursor.Element), Is.EqualTo(root.IndexOf(menu.Overlay) - 1));
            Assert.That(root.IndexOf(cursor.Element), Is.GreaterThan(root.IndexOf(menu.Room)));
        }

        [UnityTest]
        public IEnumerator AGamepadHidesTheKatanaAndTheMouseBringsItBack()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var pad = InputSystem.AddDevice<Gamepad>();
            yield return Load("MainMenu");
            var cursor = Cursor();
            Assert.That(cursor.Shown, Is.True);

            Press(pad.dpad.down);
            yield return null;
            yield return null;
            Release(pad.dpad.down);
            Assert.That(KatanaCursor.PadActive, Is.True);
            Assert.That(cursor.Shown, Is.False, "hidden while the gamepad is active");
            Assert.That(UnityEngine.Cursor.visible, Is.False, "the system cursor stays hidden too");

            // The device used last carries into the next scene.
            yield return Load("Duel");
            cursor = Cursor();
            Assert.That(cursor.Shown, Is.False);

            Set(mouse.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;
            yield return null;
            Assert.That(KatanaCursor.PadActive, Is.False);
            Assert.That(cursor.Shown, Is.True, "the mouse moving brings it back");

            Press(pad.buttonSouth);
            yield return null;
            yield return null;
            Release(pad.buttonSouth);
            Assert.That(cursor.Shown, Is.False);

            Press(mouse.rightButton);
            yield return null;
            yield return null;
            Release(mouse.rightButton);
            Assert.That(cursor.Shown, Is.True, "a click brings it back");
        }
    }
}
