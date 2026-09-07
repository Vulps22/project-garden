using System.Linq;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Lights the DeedHut's fireplace — flame, smoke and the crackle audio all live under one
    /// GameObject precisely so this can flip them together — but only for a player who belongs
    /// to a claimed Plot. This is deliberately **not networked**: two players standing in the
    /// same hut can see a different fire, because whether *you* belong to a Plot is a fact about
    /// you, not about the room. See plot-ownership.md's "everyone else sees nothing" table for
    /// the same idea applied to the shed's contents.
    ///
    /// Lives on DeedHut, not on Fireplace itself — a component cannot safely SetActive(false) its
    /// own GameObject and still expect to hear about a later claim, since OnDisable would
    /// unsubscribe it from the very events that would turn it back on.
    ///
    /// "Belongs to" means owns or is an accepted teammate — see IsLocalPlayerAssociatedWith.
    /// </summary>
    public class PlotFireToggle : MonoBehaviour
    {
        [SerializeField] private GameObject _fireplace;
        [SerializeField] private PlotLeaseManager[] _plots;

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
                plot.OwnerChanged += OnOwnerChanged;
                plot.TeammatesChanged += OnOwnerChanged;
            }
            PlayerManager.LocalPlayerJoined += OnLocalPlayerJoined;

            Refresh();
        }

        private void OnDisable()
        {
            foreach (PlotLeaseManager plot in _plots)
            {
                if (plot == null) continue;
                plot.OwnerChanged -= OnOwnerChanged;
                plot.TeammatesChanged -= OnOwnerChanged;
            }
            PlayerManager.LocalPlayerJoined -= OnLocalPlayerJoined;
        }

        private void OnOwnerChanged(PlotLeaseManager plot) => Refresh();
        private void OnLocalPlayerJoined(string playerId, string playerName) => Refresh();

        private void Refresh()
        {
            if (_fireplace == null) return;

            bool lit = _plots.Any(IsLocalPlayerAssociatedWith);
            if (_fireplace.activeSelf != lit) _fireplace.SetActive(lit);
        }

        private bool IsLocalPlayerAssociatedWith(PlotLeaseManager plot)
        {
            if (plot == null || !plot.IsClaimed) return false;
            string localId = PlayerManager.LocalPlayerId;
            return plot.OwnerId == localId || plot.TeammateIds.Contains(localId);
        }

        private void OnValidate()
        {
            if (_fireplace == null)
            {
                Transform found = transform.Find("Fireplace");
                if (found != null) _fireplace = found.gameObject;
            }
        }
    }
}
