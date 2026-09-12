using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// A player's avatar transforms: where they stand, where they look, where they reach.
    ///
    /// Somnium builds the rig in pieces and it is not finished when the player is admitted to the
    /// session, so a rig can come back unusable and the caller is expected to ask again next frame
    /// rather than cache a null.
    /// </summary>
    public readonly struct PlayerRig
    {
        public Transform Root { get; }

        /// <summary>The headset, which may be null after the hands exist. Fall back to Root.</summary>
        public Transform Head { get; }

        public Transform LeftHand { get; }
        public Transform RightHand { get; }

        /// <summary>
        /// Everything a caller can rely on is here. Head is deliberately not part of this — it
        /// arrives separately and every reader of it already tolerates its absence.
        /// </summary>
        public bool IsUsable => Root != null && LeftHand != null && RightHand != null;

        public PlayerRig(Transform root, Transform head, Transform leftHand, Transform rightHand)
        {
            Root = root;
            Head = head;
            LeftHand = leftHand;
            RightHand = rightHand;
        }
    }
}
