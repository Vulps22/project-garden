using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The shed's interior trigger. Carrying a held deed in here is what claims a Plot.
    ///
    /// Mirrors Seed.OnTriggerEnter exactly: only the holder's own client ever sees IsHeld true for
    /// an object it is carrying, so this only ever fires RequestTake() on that one client, and
    /// every other client's trigger correctly does nothing. The master's PlotLeaseManager decides
    /// the rest from there — see PlotLeaseManager.OnDeedTakeRequested.
    /// </summary>
    public class DeedClaimZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out CollectibleEntity deed)) return;
            if (deed.collectibleId != "deed") return;
            if (!deed.IsHeld) return;

            deed.RequestTake();
        }
    }
}
