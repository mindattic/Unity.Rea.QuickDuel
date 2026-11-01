using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Unity.Rea.QuickDuel
{
    // Escape in the duel, as obj_game had it (UQD-T65): room_goto(characterselect) at any moment, mid-round too. The
    // character select is dropped, and it stood between the main menu and the duel, so the duel leaves for the main menu
    // (whose own Escape still ends the game, obj_mainmenu's game_end). Gamepad Select held while Start is pressed does
    // the same, as the pad's Escape.
    //
    // Not under the verdict screen: the original's victory and gameover rooms had no Escape, so it does nothing there.
    // Not online either: online play has no original, and its Leave button is the way out of a session.
    [RequireComponent(typeof(DuelController))]
    public sealed class DuelQuit : MonoBehaviour
    {
        // The scene Escape leaves for.
        public const string MenuScene = "MainMenu";

        private DuelController controller;
        private DuelVerdict verdict;
        private InputAction escape;
        private InputAction padStart;

        // Loads a scene by name; replaced by tests so leaving doesn't tear down the test scene.
        public Action<string> Leave { get; set; } = name => SceneManager.LoadScene(name);

        private void Awake()
        {
            controller = GetComponent<DuelController>();
            verdict = GetComponent<DuelVerdict>();

            escape = new InputAction("LeaveEscape", InputActionType.Button, "<Keyboard>/escape");
            // Start alone is DuelInput's; read Select's live state directly (rather than a modifier composite) so
            // holding it with Start leaves instead, on either connected pad.
            padStart = new InputAction("LeavePadStart", InputActionType.Button, "<Gamepad>/start");

            escape.performed += _ => TryLeave();
            padStart.performed += ctx =>
            {
                if (ctx.control.device is Gamepad pad && pad.selectButton.isPressed)
                    TryLeave();
            };
        }

        private void OnEnable()
        {
            escape.Enable();
            padStart.Enable();
        }

        private void OnDisable()
        {
            escape.Disable();
            padStart.Disable();
        }

        private void OnDestroy()
        {
            escape.Dispose();
            padStart.Dispose();
        }

        // Leaves for the main menu at any moment of a local duel; false (and the duel stays) under the verdict screen
        // or online.
        public bool TryLeave()
        {
            if (controller.IsOnline || (verdict != null && verdict.Shown))
                return false;
            Leave?.Invoke(MenuScene);
            return true;
        }
    }
}
