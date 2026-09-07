using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// A physical marker: where a seed goes and a plant grows. Owns no state and no NetworkBridge
    /// of its own any more — that used to mean 24 separate NetworkObjects per Plot, one per slot,
    /// which is what drove the ~100 lines of ReassignNullObjectsAuthority churn per master
    /// transfer that plot-ownership.md's "structural half" flagged. All of that moved up to
    /// PlotStateManager, which owns the Plot's single shared NetworkObject/NetworkBridge instead.
    ///
    /// This class does not even know its own index among the Plot's 24 slots — PlotStateManager
    /// injects itself via AttachTo() at Awake and resolves the index internally. A slot that
    /// tracked its own index would need it authored per-instance (24 error-prone Editor edits
    /// across every Plot) or derived from hierarchy order duplicated in two places; letting the
    /// one class that already enumerates every slot own the index too means there is nothing here
    /// to get out of sync.
    /// </summary>
    public class PlantSlot : MonoBehaviour
    {
        private PlotStateManager _plot;

        /// <summary>Called once by PlotStateManager.Awake() for every slot it finds.</summary>
        internal void AttachTo(PlotStateManager plot) => _plot = plot;

        /// <summary>
        /// Offers something to this plot. Called by the holder's own client, because only that
        /// machine knows its hand is on the thing being offered — see Seed.OnTriggerEnter.
        /// </summary>
        public void RequestPlant(IPlantable plantable) => _plot?.RequestPlant(this, plantable);
    }
}
