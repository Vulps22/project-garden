using UnityEngine;
using UnityEngine.InputSystem;

namespace Vulps.GrowAGarden
{
    /// <summary>
    /// A 2D player's jump and walking keys, listened to rather than bound: the client still jumps
    /// and walks on them, and we only hear the same presses. Keyboard only; a gamepad is out of scope.
    /// </summary>
    public static class DesktopControls
    {
        /// <summary>Jump (Space) went down this frame.</summary>
        public static bool JumpPressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

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
