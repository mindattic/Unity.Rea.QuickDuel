using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Unity.Rea.QuickDuel
{
    // Whether the player is typing into a text field (the online menu's address, UQD-T21), so the duel's own
    // keys -- Space and the Ctrl keys to strike (and the host's Space for the next round) -- leave the keystroke to the field.
    public static class UiFocus
    {
        public static bool IsTyping
        {
            get
            {
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (selected == null)
                    return false;
                var field = selected.GetComponent<InputField>();
                return field != null && field.isFocused;
            }
        }

        // The pointer is over a clickable UI element (the duel's own drawing takes no raycasts, so only menus count).
        // A raycast of its own, not IsPointerOverGameObject, which warns when asked from inside an input callback.
        public static bool PointerOverUi
        {
            get
            {
                var events = EventSystem.current;
                var pointer = Pointer.current;
                if (events == null || pointer == null)
                    return false;
                Hits.Clear();
                events.RaycastAll(new PointerEventData(events) { position = pointer.position.ReadValue() }, Hits);
                return Hits.Count > 0;
            }
        }

        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
    }
}
