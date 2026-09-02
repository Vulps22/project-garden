namespace GrowAGarden
{
    /// <summary>
    /// Declares what physical mode an object should currently be in.
    ///
    /// Exists so that <see cref="KinematicController"/> can ask the one thing that actually
    /// knows the answer, instead of caching a value that goes stale. Before this, three
    /// separate writers each set Rigidbody.isKinematic from their own stale copy:
    /// PlantSeed wrote it directly, NetworkGrabbable cached it once in Awake and
    /// re-asserted that forever, and XRGrabInteractable snapshots it on grab and restores the
    /// snapshot on release. That was survivable only while every seed was kinematic in every
    /// state; it stops being survivable as soon as the intended value changes at runtime.
    /// </summary>
    public interface IKinematicSource
    {
        /// <summary>True when the object should not be simulated by physics right now.</summary>
        bool ShouldBeKinematic { get; }

        /// <summary>
        /// True when gravity should pull on it. Only meaningful while non-kinematic. Kept here
        /// rather than written by whichever behaviour happens to want it, so gravity does not
        /// repeat the multiple-stale-writers problem that isKinematic had.
        /// </summary>
        bool ShouldUseGravity { get; }
    }
}
