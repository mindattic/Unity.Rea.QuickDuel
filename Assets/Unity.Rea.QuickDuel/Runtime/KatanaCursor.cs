using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

namespace Unity.Rea.QuickDuel
{
    // A scene whose UI Toolkit tree the katana cursor hangs off (UQD-T46): the main menu and the duel.
    public interface IKatanaCursorHost
    {
        VisualElement Root { get; }

        // The element the cursor draws immediately under, null for over everything on Root.
        VisualElement CursorCeiling { get; }
    }

    // obj_mouse (UQD-T46): spr_katana_cursor in place of the system cursor, in the rooms that placed it (mainmenu and
    // gamescreen). The original's Global Game Settings hid the system cursor (option_showcursor False) and obj_mouse moved
    // to (mouse_x, mouse_y) each step and drew itself in Draw GUI at depth -5000, its sprite's origin (0, 0) at the
    // blade's tip: the pointer's hotspot is the tip. It is drawn at the original's pixel scale, over everything the
    // room drew (the corner arts at -101 among them) and under the letterbox and the loading fades (-10000).
    //
    // Not the original's: a gamepad had no part in it. While a gamepad is the device last used (a button pressed or a
    // stick pushed) the katana is hidden, as a pointer nothing is driving; it returns the moment the mouse moves or
    // clicks. Which device is active carries from scene to scene.
    public sealed class KatanaCursor : MonoBehaviour
    {
        // spr_katana_cursor. Written by the scene builders.
        [SerializeField] private Sprite sprite;

        private IKatanaCursorHost host;
        private VisualElement element;
        private Vector2? lastMousePosition;

        public void SetSprite(Sprite katana) => sprite = katana;

        public Sprite Sprite => sprite;

        // The drawn katana, for tests and screenshots.
        public VisualElement Element => element;

        // The katana is drawn: a mouse is present and no gamepad has been used since it last moved or clicked.
        public bool Shown => element != null && element.style.display == DisplayStyle.Flex;

        // A gamepad is the device last used, here or in an earlier scene.
        public static bool PadActive { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => PadActive = false;

        // For tests: forget which device was used last.
        public static void ResetActiveDevice() => PadActive = false;

        private void OnEnable() => UnityEngine.Cursor.visible = false;

        private void OnDisable() => UnityEngine.Cursor.visible = true;

        private void LateUpdate()
        {
            if (element == null && !Attach())
                return;
            Observe();
            UnityEngine.Cursor.visible = false;
            KeepUnderCeiling();
            Draw();
        }

        private bool Attach()
        {
            host ??= GetComponent<IKatanaCursorHost>();
            var root = host?.Root;
            if (root == null)
                return false;
            element = new VisualElement { name = "KatanaCursor", pickingMode = PickingMode.Ignore };
            element.style.position = Position.Absolute;
            var size = sprite != null ? sprite.rect.size : Vector2.zero;
            element.style.width = size.x * OutlinedText.PixelScale;
            element.style.height = size.y * OutlinedText.PixelScale;
            element.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            element.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
            root.Add(element);
            var mouse = Mouse.current;
            lastMousePosition = mouse != null ? mouse.position.ReadValue() : (Vector2?)null;
            return true;
        }

        // The device used last: any gamepad button (the d-pad and the sticks' directions among them) pressed this frame
        // makes the pad active, the mouse moving or any of its buttons pressed makes the mouse active again.
        private void Observe()
        {
            foreach (var pad in Gamepad.all)
            {
                if (!pad.wasUpdatedThisFrame)
                    continue;
                foreach (var control in pad.allControls)
                {
                    if (control is ButtonControl button && button.wasPressedThisFrame)
                    {
                        PadActive = true;
                        break;
                    }
                }
            }

            var mouse = Mouse.current;
            if (mouse == null)
                return;
            var position = mouse.position.ReadValue();
            var moved = lastMousePosition.HasValue && position != lastMousePosition.Value;
            lastMousePosition = position;
            if (moved || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                PadActive = false;
        }

        // Directly under the ceiling (the letterbox, the menu's loading fade), so anything hung off the tree since
        // (the corner arts) stays under the katana.
        private void KeepUnderCeiling()
        {
            var root = host.Root;
            var ceiling = host.CursorCeiling;
            if (ceiling != null && ceiling.parent == root)
            {
                if (root.IndexOf(element) != root.IndexOf(ceiling) - 1)
                {
                    element.RemoveFromHierarchy();
                    root.Insert(root.IndexOf(ceiling), element);
                }
            }
            else if (element.parent != root || root.IndexOf(element) != root.childCount - 1)
            {
                root.Add(element);
            }
        }

        // The sprite's top-left corner, the blade's tip, at the pointer.
        private void Draw()
        {
            var mouse = Mouse.current;
            var shown = mouse != null && !PadActive && element.panel != null;
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown)
                return;
            var screen = mouse.position.ReadValue();
            var panel = RuntimePanelUtils.ScreenToPanel(element.panel, new Vector2(screen.x, Screen.height - screen.y));
            var local = host.Root.WorldToLocal(panel);
            element.style.left = local.x;
            element.style.top = local.y;
        }
    }
}
