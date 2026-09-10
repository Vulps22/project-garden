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
        /// <summary>
        /// This player has just carried their deed through the door. Local by construction: the
        /// IsHeld check below is true on exactly one machine, so this only ever fires on the
        /// client that did it — no filtering needed by whoever listens.
        ///
        /// Announced as well as acted on because it is the moment the narrator introduces the hut,
        /// and the claim itself is decided by the master several hundred milliseconds later.
        /// </summary>
        public static event System.Action DeedCarriedIn;

        private void OnDestroy() => DeedCarriedIn = null;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out CollectibleEntity deed)) return;
            if (deed.collectibleId != "deed") return;
            if (!deed.IsHeld) return;

            deed.RequestTake();
            DeedCarriedIn?.Invoke();
        }
    }
}
