using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The shed's other decision point, alongside DeedClaimZone on the same trigger volume: an
    /// application scroll carried in here — already dropped on some Plot, per its
    /// AssociatedPlotId — asks that Plot to accept it. Same one-client IsHeld gate as
    /// DeedClaimZone; PlotApplication's own master-only handling decides the rest.
    /// </summary>
    public class ApplicationAcceptZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out CollectibleEntity ce) || ce.collectibleId != "blank_scroll") return;
            if (!ce.IsHeld) return;
            if (!other.TryGetComponent(out PlotApplication app) || app.AssociatedPlotId == 0) return;

            app.RequestAccept();
        }
    }
}
