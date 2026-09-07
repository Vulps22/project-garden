using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Clears up the ground after a player who is not coming back.
    ///
    /// It owns no clock. <see cref="PlayerManager"/> decides when a departure becomes permanent and
    /// raises PlayerDestroyed; this answers only "what do I clear up for that id?". It used to own
    /// both, which meant anything else needing to tidy up after a departure would have had to keep
    /// a second timer that agreed with this one.
    ///
    /// The immediate half of a departure — dropping a departed player's HolderId — is not here
    /// either. Seed and Produce take that from PlayerManager.PlayerLeft themselves, because it is a
    /// correction each object makes to its own state rather than anything this needs to coordinate.
    ///
    /// Long-term persistence across sessions is explicitly out of scope.
    ///
    /// Naming: this is no longer a lease now the timer has moved out — it is plot cleanup, and
    /// terminology.md records the rename as pending. Left alone here so a behaviour change and a
    /// scene-touching rename do not land in the same upload.
    /// </summary>
    public class GardenLease : MonoBehaviour
    {
        private void Awake()
        {
            PlayerManager.PlayerDestroyed += OnPlayerDestroyed;
        }

        private void OnDestroy()
        {
            PlayerManager.PlayerDestroyed -= OnPlayerDestroyed;
        }

        /// <summary>
        /// Frees every Plot the player held, uproots what stands in every one of its slots, and
        /// removes any seeds they bought and left lying about.
        ///
        /// Master only — this despawns things — and PlayerManager already raises PlayerDestroyed on
        /// the master alone. The guard stays because that is a promise made elsewhere, and a
        /// despawn without authority fails silently.
        ///
        /// Ownership is Plot-wide now, not per-slot — PlantSlot no longer carries an OwnerId at
        /// all, so there is nothing left to loop over slot-by-slot. Clearing a departed player's
        /// Plot means uprooting every one of its 24 slots together, via the Plot's own
        /// PlotStateManager, then relinquishing the claim.
        /// </summary>
        private void OnPlayerDestroyed(string playerId)
        {
            if (!SceneNetworking.IsMasterClient) return;

            int seeds = 0;
            foreach (Seed seed in FindObjectsByType<Seed>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (seed == null || seed.OwnerId != playerId || seed.InShop) continue;
                // Count what actually went, not what was asked to go. Discard() is a no-op without
                // state authority, and a seed whose owner has just left is the case most likely to
                // have none.
                if (seed.Discard()) seeds++;
            }

            int leases = 0;
            foreach (PlotLeaseManager lease in FindObjectsByType<PlotLeaseManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (lease == null) continue;

                // RemoveTeammate no-ops on a Plot the departed player was never on, so this runs
                // for every Plot rather than tracking who is whose teammate first — same reasoning
                // as looping every Seed above rather than only ones known to be theirs.
                lease.RemoveTeammate(playerId);

                if (lease.OwnerId != playerId) continue;

                PlotStateManager state = lease.GetComponent<PlotStateManager>();
                if (state != null) state.UprootAll();

                lease.Relinquish();
                leases++;
            }

            Logger.Info($"OnPlayerDestroyed() '{gameObject.name}' — cleared '{playerId}': {leases} plot leases relinquished, {seeds} seeds removed");
        }
    }
}
