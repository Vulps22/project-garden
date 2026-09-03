using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// One place on a bearing plant where a produce can hang, and what is currently hanging there.
    ///
    /// **Runtime only, deliberately.** This used to be a [Serializable] class authored as an array
    /// on the prefab, and the array arrived empty in the exported Somnium world — a pumpkin grew
    /// its vine and then reported "bearing into 0 socket(s)", while every plain-typed field on the
    /// same prefab (the body, the max scale, the produce prefab) came through untouched. The
    /// Editor deserialised the same asset correctly every time, so the cause is somewhere in the
    /// bundle export and not visible from here.
    ///
    /// Rather than keep guessing at it, the authored data is now a plain Transform[] — the one
    /// kind of array there is no doubt about — and this is built from it at Awake. Nothing about
    /// bearing changed; only what the prefab is asked to remember.
    /// </summary>
    public class ProduceSlot
    {
        /// <summary>Where the produce sits. The plant does not move, so this is read once.</summary>
        public readonly Transform socket;

        /// <summary>Fusion id of the produce hanging here, or 0 when the socket is empty.</summary>
        public uint produceId;

        /// <summary>True once something borne here has been taken.</summary>
        public bool harvested;

        public ProduceSlot(Transform socket) => this.socket = socket;

        public bool IsEmpty => produceId == 0;
    }
}
