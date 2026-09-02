using System;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// One place on a bearing plant where a produce can hang.
    ///
    /// The array of these is what makes a pumpkin, a tomato and an apple tree one mechanism rather
    /// than three: a pumpkin has one, a tomato several, an apple tree many.
    ///
    /// It holds the produce's Fusion id rather than a reference, the same way every other
    /// cross-object message in this project addresses things — a reference would be master-only,
    /// and this has to survive a state broadcast.
    /// </summary>
    [Serializable]
    public class ProduceSlot
    {
        [Tooltip("Where the produce sits. The plant does not move, so this is set once and never written again.")]
        public Transform socket;

        /// <summary>Fusion id of the produce hanging here, or 0 when the socket is empty.</summary>
        [NonSerialized] public uint produceId;

        /// <summary>True once something borne here has been taken.</summary>
        [NonSerialized] public bool harvested;

        public bool IsEmpty => produceId == 0;
    }
}
