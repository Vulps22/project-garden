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
        private PlantSlot[] _slots;

        private void Awake()
        {
            PlayerManager.PlayerDestroyed += OnPlayerDestroyed;
        }

        private void OnDestroy()
        {
            PlayerManager.PlayerDestroyed -= OnPlayerDestroyed;
        }

        /// <summary>
        /// Frees every plot the player held, destroys what stands in them, and removes any seeds
        /// they bought and left lying about.
        ///
        /// Master only — this despawns things — and PlayerManager already raises PlayerDestroyed on
        /// the master alone. The guard stays because that is a promise made elsewhere, and a
        /// despawn without authority fails silently.
        /// </summary>
        private void OnPlayerDestroyed(string playerId)
        {
            if (!SceneNetworking.IsMasterClient) return;

            _slots ??= FindObjectsByType<PlantSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int plots = 0;
            foreach (PlantSlot slot in _slots)
            {
                if (slot == null || slot.OwnerId != playerId) continue;
                if (slot.Occupant != null) slot.Occupant.Uproot();
                slot.ClearOwner();
                plots++;
            }

            int seeds = 0;
            foreach (Seed seed in FindObjectsByType<Seed>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (seed == null || seed.OwnerId != playerId || seed.InShop) continue;
                // Count what actually went, not what was asked to go. Discard() is a no-op without
                // state authority, and a seed whose owner has just left is the case most likely to
                // have none.
                if (seed.Discard()) seeds++;
            }

            Logger.Info($"OnPlayerDestroyed() '{gameObject.name}' — cleared '{playerId}': {plots} plots freed, {seeds} seeds removed");
        }
    }
}
