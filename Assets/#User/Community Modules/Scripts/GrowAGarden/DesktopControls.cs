using UnityEngine;
using UnityEngine.InputSystem;

namespace Vulps.GrowAGarden
{
    /// <summary>
    /// A 2D player's keys, listened to rather than bound: the client still jumps and walks on its own, and we only
    /// hear the same presses. F is the client's interact key too: we hear it without taking it from the client
    /// (Vulps, 2026-11-11). Z, 1 and 2 are keys the client leaves free. Keyboard only; a gamepad is out of
    /// scope. See docs/2d-players.md.
    /// </summary>
    public static class DesktopControls
    {
        /// <summary>Jump (Space) went down this frame.</summary>
        public static bool JumpPressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        /// <summary>F (interact) went down this frame. Somnium's own F still fires: this only listens.</summary>
        public static bool InteractPressed => Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;

        /// <summary>Z (eat, or a splif to the mouth) went down this frame.</summary>
        public static bool EatPressed => Keyboard.current != null && Keyboard.current.zKey.wasPressedThisFrame;

        /// <summary>Tab (the client's own tablet key) went down this frame.</summary>
        public static bool TabletPressed => Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;

        /// <summary>The hand picked this frame: 0 for the 1 key, 1 for the 2 key, -1 for neither.</summary>
        public static int HandPressed
        {
            get
            {
                Keyboard keys = Keyboard.current;
                if (keys == null) return -1;
                if (keys.digit1Key.wasPressedThisFrame || keys.numpad1Key.wasPressedThisFrame) return 0;
                if (keys.digit2Key.wasPressedThisFrame || keys.numpad2Key.wasPressedThisFrame) return 1;
                return -1;
            }
        }

        /// <summary>The walking direction held now: x right, y forward, at most length 1. WASD or the arrows.</summary>
        public static Vector2 Move
        {
            get
            {
                Vector2 move = Vector2.zero;
                Keyboard keys = Keyboard.current;
                if (keys != null)
                {
                    if (keys.wKey.isPressed || keys.upArrowKey.isPressed) move.y += 1f;
                    if (keys.sKey.isPressed || keys.downArrowKey.isPressed) move.y -= 1f;
                    if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) move.x += 1f;
                    if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) move.x -= 1f;
                }

                return Vector2.ClampMagnitude(move, 1f);
            }
        }

        /// <summary>Where the player looks from and towards: the client's camera, or null before it exists.</summary>
        public static Transform View => Camera.main != null ? Camera.main.transform : null;
    }
}
