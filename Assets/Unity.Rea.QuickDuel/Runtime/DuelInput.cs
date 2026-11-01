using Unity.Rea.QuickDuel.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.Rea.QuickDuel
{
    // The original's strike keys (obj_game, UQD-T69). Against the computer, Space or the left mouse button strikes for
    // the player (the left fighter). In Versus, Left Ctrl strikes for the left fighter and Right Ctrl for the right, and
    // the mouse strikes for nobody. During the startup a click begins the round at once instead, in either mode, as
    // obj_game's global left press did (DuelStartup, UQD-T66). A touchscreen press is the mouse's. Not the original's:
    // gamepad 1's South strikes for the left fighter and gamepad 2's for the right (and skips the startup, as the click
    // does). Online (no original either) every strike key strikes for this machine's seat, and the host's Space or
    // Start begins the next round. Each strike carries its input event's own timestamp (CallbackContext.time, on
    // the Time.realtimeSinceStartup timeline), so the frame that happens to process it doesn't change the
    // reaction time.
    [RequireComponent(typeof(DuelController))]
    public sealed class DuelInput : MonoBehaviour
    {
        private DuelController controller;
        private DuelStartup startup;
        private DuelFade fade;
        private DuelVerdict verdict;
        private InputAction spaceKey;
        private InputAction leftCtrl;
        private InputAction rightCtrl;
        private InputAction padStrike;
        private InputAction pointerStrike;
        private InputAction nextRound;

        // False while the computer plays the right side (is_singleplayer); set by DuelSession from the mode.
        public bool RightIsHuman { get; set; } = true;

        // is_singleplayer: a local duel against the computer.
        private bool VsComputer => !controller.IsOnline && !RightIsHuman;

        // A local two-player duel.
        private bool Versus => !controller.IsOnline && RightIsHuman;

        private void Awake()
        {
            controller = GetComponent<DuelController>();
            startup = GetComponent<DuelStartup>();

            spaceKey = new InputAction("SpaceStrike", InputActionType.Button, "<Keyboard>/space");
            leftCtrl = new InputAction("LeftCtrlStrike", InputActionType.Button, "<Keyboard>/leftCtrl");
            rightCtrl = new InputAction("RightCtrlStrike", InputActionType.Button, "<Keyboard>/rightCtrl");

            // Pass-through so two pads pressing together each report, rather than one action
            // disambiguating them away.
            padStrike = new InputAction("PadStrike", InputActionType.PassThrough, "<Gamepad>/buttonSouth");

            // <Pointer>/press covers Mouse's left button and Touchscreen's contact alike.
            pointerStrike = new InputAction("PointerStrike", InputActionType.Button, "<Pointer>/press");

            nextRound = new InputAction("NextRound", InputActionType.Button);
            nextRound.AddBinding("<Keyboard>/space");
            nextRound.AddBinding("<Gamepad>/start");

            // Keys typed into the online menu's address field, and clicks on its buttons, are the menu's (UQD-T21).
            // obj_game's Space: if (is_singleplayer) action_attack(0).
            spaceKey.performed += ctx => { if (!Versus && !UiFocus.IsTyping) controller.Strike(Side.Left, ctx.time); };
            // obj_game's keyboard event: if (!is_singleplayer) vk_lcontrol -> action_attack(0), vk_rcontrol -> action_attack(1).
            leftCtrl.performed += ctx => { if (!VsComputer && !UiFocus.IsTyping) controller.Strike(Side.Left, ctx.time); };
            rightCtrl.performed += ctx => { if (!VsComputer && !UiFocus.IsTyping) controller.Strike(Side.Right, ctx.time); };
            padStrike.performed += OnPadStrike;
            // obj_game's global left press: startup ? countdown : (is_singleplayer ? action_attack(0)).
            pointerStrike.performed += ctx =>
            {
                if (UiFocus.PointerOverUi || Skipped() || Versus) return;
                controller.Strike(Side.Left, ctx.time);
            };
            // Online only: the host's Start or Space begins the next round, unless Select is held with Start — that
            // chord is DuelQuit's. A local round begins by itself (DuelStartup).
            nextRound.performed += ctx =>
            {
                if (!controller.IsOnline)
                    return;
                if (ctx.control.device is Gamepad pad && pad.selectButton.isPressed)
                    return;
                if (ctx.control.device is Keyboard && UiFocus.IsTyping)
                    return;
                // The aftermath plays out and the loading fade resets the battle before the next round (UQD-T42).
                if (fade == null) fade = GetComponent<DuelFade>();
                if (fade != null && fade.Pending)
                    return;
                // The verdict screen's button, not Space or Start, leaves a finished match (UQD-T48).
                if (verdict == null) verdict = GetComponent<DuelVerdict>();
                if (verdict != null && verdict.Shown)
                    return;
                controller.BeginRound();
            };
        }

        private void OnEnable()
        {
            spaceKey.Enable();
            leftCtrl.Enable();
            rightCtrl.Enable();
            padStrike.Enable();
            pointerStrike.Enable();
            nextRound.Enable();
        }

        private void OnDisable()
        {
            spaceKey.Disable();
            leftCtrl.Disable();
            rightCtrl.Disable();
            padStrike.Disable();
            pointerStrike.Disable();
            nextRound.Disable();
        }

        private void OnDestroy()
        {
            spaceKey.Dispose();
            leftCtrl.Dispose();
            rightCtrl.Dispose();
            padStrike.Dispose();
            pointerStrike.Dispose();
            nextRound.Dispose();
        }

        // The startup's skip (obj_game's global left press in "startup"): true when it began the round.
        private bool Skipped() => startup != null && startup.TrySkip();

        private void OnPadStrike(InputAction.CallbackContext ctx)
        {
            if (!ctx.ReadValueAsButton())
                return;
            if (SideOfGamepad(ctx.control.device).HasValue && Skipped())
                return;
            var side = SideOfGamepad(ctx.control.device);
            if (side.HasValue && (side.Value == Side.Left || RightIsHuman))
                controller.Strike(side.Value, ctx.time);
        }

        // Gamepad 1 (the first connected) is the left duelist, gamepad 2 the right; any others sit out.
        public static Side? SideOfGamepad(InputDevice device)
        {
            var pads = Gamepad.all;
            if (pads.Count > 0 && pads[0] == device) return Side.Left;
            if (pads.Count > 1 && pads[1] == device) return Side.Right;
            return null;
        }
    }
}
