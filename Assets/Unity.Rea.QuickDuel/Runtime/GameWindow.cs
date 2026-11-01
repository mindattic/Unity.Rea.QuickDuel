using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.Rea.QuickDuel
{
    // F1 toggles fullscreen (UQD-T50), as obj_intro, obj_mainmenu and obj_game each did with their F1 key press
    // (window_set_fullscreen(!window_get_fullscreen())): the window goes to the whole display, and back to the window it
    // was. Whatever the window's size or shape, every scene draws the original's 640 x 480 room letterboxed into it
    // (OriginalFrame), as the original's window kept its aspect (option_scale -1, keep aspect ratio).
    //
    // Each scene's own controller adds it in code (IntroController, MainMenuController, DuelView), as each of the
    // original's rooms carried the F1 handler in its own object; the window it goes back to is kept from scene to scene.
    public sealed class GameWindow : MonoBehaviour
    {
        private InputAction toggle;

        // The window's size the last time fullscreen was entered, given back when it is left; zero before.
        public static Vector2Int WindowedSize { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => WindowedSize = Vector2Int.zero;

        // The window when none has been seen: twice the original's room, as the player's default window.
        public static readonly Vector2Int DefaultWindow = new Vector2Int(OriginalFrame.RoomWidth * 2, OriginalFrame.RoomHeight * 2);

        // Reads whether the game is fullscreen and the window's and the display's sizes; tests replace them.
        public Func<bool> IsFullscreen { get; set; } = () => Screen.fullScreen;
        public Func<Vector2Int> CurrentSize { get; set; } = () => new Vector2Int(Screen.width, Screen.height);
        public Func<Vector2Int> DisplaySize { get; set; } = () =>
            new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);

        // Applies a size and mode to the window; tests replace it.
        public Action<int, int, FullScreenMode> Apply { get; set; } = (width, height, mode) => Screen.SetResolution(width, height, mode);

        // Adds the handler to `host` unless it has one.
        public static GameWindow On(GameObject host) =>
            host.TryGetComponent<GameWindow>(out var window) ? window : host.AddComponent<GameWindow>();

        private void Awake()
        {
            toggle = new InputAction("Fullscreen", InputActionType.Button, "<Keyboard>/f1");
            toggle.performed += _ => Toggle();
        }

        private void OnEnable() => toggle.Enable();
        private void OnDisable() => toggle.Disable();
        private void OnDestroy() => toggle.Dispose();

        // window_set_fullscreen(!window_get_fullscreen()).
        public void Toggle()
        {
            if (IsFullscreen())
            {
                var size = WindowedSize.x > 0 && WindowedSize.y > 0 ? WindowedSize : DefaultWindow;
                Apply(size.x, size.y, FullScreenMode.Windowed);
            }
            else
            {
                WindowedSize = CurrentSize();
                var display = DisplaySize();
                Apply(display.x, display.y, FullScreenMode.FullScreenWindow);
            }
        }
    }
}
