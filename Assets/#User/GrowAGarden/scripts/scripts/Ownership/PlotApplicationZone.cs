using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// A Plot's own trigger for "an application scroll was dropped here" — separate from
    /// DeedClaimZone's job (claiming the Plot itself) and from ApplicationAcceptZone's (accepting
    /// the application in the hut). Mirrors DeedClaimZone exactly: only the holder's own client
    /// ever sees IsHeld true for an object it is carrying, so this only ever fires on that one
    /// client, and PlotApplication's own master-only handling does the rest.
    /// </summary>
    public class PlotApplicationZone : MonoBehaviour
    {
        [SerializeField] private PlotLeaseManager _plot;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out CollectibleEntity ce) || ce.collectibleId != "blank_scroll") return;
            if (!ce.IsHeld) return;
            if (!other.TryGetComponent(out PlotApplication app)) return;

            app.RequestAssociatePlot(_plot.NetworkId);
        }

        private void OnValidate()
        {
            if (_plot == null) _plot = GetComponentInParent<PlotLeaseManager>();
        }
    }
}
