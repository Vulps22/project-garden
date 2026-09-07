using TMPro;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The Named Deed above the DeedHut's fireplace — reads, never decides. It watches every
    /// Plot's OwnerId and shows whoever most recently claimed or relinquished one, resolving the
    /// id to a display name through EconomyManager's balance table (already synced to everyone,
    /// so no lookup of our own is needed).
    ///
    /// There is only one sign for four Plots, so "most recent" is order-of-arrival across four
    /// independent broadcasts — fine for a decorative plaque, since nothing here is ever read
    /// back as a fact. Two clients could show a different name for a moment if two claims land
    /// in the same frame; both settle on the same name once the broadcasts are done arriving.
    /// </summary>
    public class PlotClaimSignage : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _nameText;
        [SerializeField] private PlotLeaseManager[] _plots;
        [SerializeField] private string _unclaimedText = "";

        private void Awake()
        {
            if (_plots == null || _plots.Length == 0)
                _plots = UnityEngine.Object.FindObjectsByType<PlotLeaseManager>(FindObjectsSortMode.None);
        }

        private void OnEnable()
        {
            foreach (PlotLeaseManager plot in _plots)
            {
                if (plot != null) plot.OwnerChanged += OnOwnerChanged;
            }
        }

        private void OnDisable()
        {
            foreach (PlotLeaseManager plot in _plots)
            {
                if (plot != null) plot.OwnerChanged -= OnOwnerChanged;
            }
        }

        private void OnOwnerChanged(PlotLeaseManager plot)
        {
            if (_nameText == null) return;

            if (!plot.IsClaimed)
            {
                _nameText.text = _unclaimedText;
                return;
            }

            string name = EconomyManager.Instance != null
                ? EconomyManager.Instance.GetPlayer(plot.OwnerId)?.GetPlayerName()
                : null;

            _nameText.text = string.IsNullOrEmpty(name) ? plot.OwnerId : name;
        }

        private void OnValidate()
        {
            if (_nameText == null) _nameText = GetComponentInChildren<TextMeshPro>(true);
        }
    }
}
