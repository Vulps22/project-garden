using UnityEngine;
using Vulps.GrowAGarden.Economy;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Orchestration
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
        private PlotCleanup _lease;

        private void Awake()
        {
            _lease = FindFirstObjectByType<PlotCleanup>(FindObjectsInactive.Include);
            if (_lease == null)
                Logger.Error($"Awake() '{gameObject.name}' — no PlotCleanup in the scene; a departed player's plots and seeds will never be cleared");
        }

        private void OnEnable()
        {
            PlayerManager.PlayerJoined += OnPlayerJoined;
            PlayerManager.PlayerDestroyed += OnPlayerDestroyed;
            EconomyManager.TableApplied += HoldThoseGone;
        }

        private void OnDisable()
        {
            PlayerManager.PlayerJoined -= OnPlayerJoined;
            PlayerManager.PlayerDestroyed -= OnPlayerDestroyed;
            EconomyManager.TableApplied -= HoldThoseGone;
        }

        /// <summary>
        /// A master that joined after someone left never saw them go, so would never clear up after them. The balance
        /// table lists everyone not yet cleared: anyone in it who is not here starts a grace period on this master.
        /// </summary>
        private void HoldThoseGone()
        {
            if (!PlayerManager.IsMaster || EconomyManager.Instance == null) return;
            foreach (string id in new System.Collections.Generic.List<string>(EconomyManager.Instance.PlayerIds))
                PlayerManager.HoldDeparted(id);
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
