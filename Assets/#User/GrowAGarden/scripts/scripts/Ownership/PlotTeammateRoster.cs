using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The wall of parchments — one viewer's own teammate list, derived locally from a fact
    /// everyone already has. Only the owner of a claimed Plot sees anything here; matches
    /// plot-ownership.md's per-viewer table ("a Plot owner sees ... their teammate list; everyone
    /// else sees nothing else"). A teammate seeing their own membership is that doc's still-open
    /// question, not built here.
    ///
    /// If a player somehow owns more than one of the four Plots, this just concatenates their
    /// teammates across all of them — an edge case not worth more design than that.
    /// </summary>
    public class PlotTeammateRoster : MonoBehaviour
    {
        [SerializeField] private PlotLeaseManager[] _plots;
        [SerializeField] private TeammateSlotDisplay[] _slots;

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
                plot.TeammatesChanged += OnPlotChanged;
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
                plot.TeammatesChanged -= OnPlotChanged;
            }
            PlayerManager.LocalPlayerJoined -= OnLocalPlayerJoined;
        }

        private void OnPlotChanged(PlotLeaseManager plot) => Refresh();
        private void OnLocalPlayerJoined(string playerId, string playerName) => Refresh();

        private void Refresh()
        {
            if (_slots == null || _slots.Length == 0) return;

            int slotIndex = 0;
            foreach (PlotLeaseManager plot in _plots)
            {
                if (plot == null || plot.OwnerId != PlayerManager.LocalPlayerId) continue;

                foreach (string teammateId in plot.TeammateIds)
                {
                    if (slotIndex >= _slots.Length) break;

                    string name = EconomyManager.Instance != null
                        ? EconomyManager.Instance.GetPlayer(teammateId)?.GetPlayerName()
                        : null;

                    _slots[slotIndex].Show(plot, teammateId, string.IsNullOrEmpty(name) ? teammateId : name);
                    slotIndex++;
                }
            }

            for (; slotIndex < _slots.Length; slotIndex++)
            {
                if (_slots[slotIndex] != null) _slots[slotIndex].Hide();
            }
        }

        private void OnValidate()
        {
            if (_plots == null || _plots.Length == 0)
                _plots = UnityEngine.Object.FindObjectsByType<PlotLeaseManager>(FindObjectsSortMode.None);
        }
    }
}
