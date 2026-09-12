using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Runs the response to a player arriving and to one leaving for good: gives them a balance,
    /// and clears their plots and seeds when they do not come back.
    ///
    /// Managers are called from here rather than subscribing, so the order between them is stated.
    /// Components still subscribe to PlayerManager directly.
    /// </summary>
    public class PlayerLifecycle : MonoBehaviour
    {
        private GardenLease _lease;

        private void Awake()
        {
            _lease = FindFirstObjectByType<GardenLease>(FindObjectsInactive.Include);
            if (_lease == null)
                Logger.Error($"Awake() '{gameObject.name}' — no GardenLease in the scene; a departed player's plots and seeds will never be cleared");
        }

        private void OnEnable()
        {
            PlayerManager.PlayerJoined += OnPlayerJoined;
            PlayerManager.PlayerDestroyed += OnPlayerDestroyed;
        }

        private void OnDisable()
        {
            PlayerManager.PlayerJoined -= OnPlayerJoined;
            PlayerManager.PlayerDestroyed -= OnPlayerDestroyed;
        }

        private void OnPlayerJoined(string playerId, string playerName)
        {
            if (!PlayerManager.IsMaster) return;

            EconomyManager economy = EconomyManager.Instance;
            if (economy == null)
            {
                Logger.Error($"OnPlayerJoined() '{gameObject.name}' — no EconomyManager; '{playerId}' has no balance");
                return;
            }

            economy.EnsureBalance(playerId, playerName);
        }

        /// <summary>
        /// Frees the player's plots and seeds, then drops their balance. Plots first, so cleanup
        /// can still read what they owned.
        /// </summary>
        private void OnPlayerDestroyed(string playerId)
        {
            if (!PlayerManager.IsMaster) return;

            if (_lease != null) _lease.ReleaseFor(playerId);

            EconomyManager economy = EconomyManager.Instance;
            if (economy != null) economy.RemovePlayer(playerId);
        }
    }
}
