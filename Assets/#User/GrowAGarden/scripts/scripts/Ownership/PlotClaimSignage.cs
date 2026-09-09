using System.Linq;
using TMPro;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The Named Deed above the DeedHut's fireplace — reads, never decides, and is local to the
    /// viewer, same as everything else in this hut. It shows the LOCAL player's own name if they
    /// own one of the four Plots, and nothing otherwise. OwnerId itself is a real, replicated
    /// fact (the master broadcasts it), but what this sign renders from that fact is a per-viewer
    /// derivation, not a shared plaque — two people standing in the same hut can see a different
    /// sign, same as the wall roster and the fireplace, per plot-ownership.md's "everyone else
    /// sees nothing" table. It previously showed whoever most recently claimed ANY Plot to EVERY
    /// viewer, which was wrong — this hut's contents are never a shared broadcast display.
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
                if (plot == null) continue;
                plot.OwnerChanged += OnPlotChanged;
            }
            PlayerManager.LocalPlayerJoined += OnLocalPlayerJoined;

            Refresh();
        }

        private void OnDisable()
        {
            foreach (PlotLeaseManager plot in _plots)
            {
                if (plot == null) continue;
                plot.OwnerChanged -= OnPlotChanged;
            }
            PlayerManager.LocalPlayerJoined -= OnLocalPlayerJoined;
        }

        private void OnPlotChanged(PlotLeaseManager plot) => Refresh();
        private void OnLocalPlayerJoined(string playerId, string playerName) => Refresh();

        private void Refresh()
        {
            if (_nameText == null) return;

            string localId = PlayerManager.LocalPlayerId;
            bool ownsAPlot = _plots.Any(p => p != null && p.IsClaimed && p.OwnerId == localId);

            if (!ownsAPlot)
            {
                _nameText.text = _unclaimedText;
                return;
            }

            string name = PlayerManager.GetLocalPlayer()?.Properties?.NickName;

            _nameText.text = string.IsNullOrEmpty(name) ? localId : name;
        }

        private void OnValidate()
        {
            if (_nameText == null) _nameText = GetComponentInChildren<TextMeshPro>(true);
        }
    }
}
