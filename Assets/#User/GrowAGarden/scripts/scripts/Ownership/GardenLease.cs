using System.Collections.Generic;
using SomniumSpace.Bridge.Components;
using SomniumSpace.Bridge.Player;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Tidies up after a player who has left, but not straight away.
    ///
    /// Cleanup is a 120 second lease rather than an instant sweep, because a dropout is
    /// indistinguishable from leaving and losing a garden to a flaky connection is a miserable way
    /// to find that out. Come back inside the window and everything is exactly where you left it.
    ///
    /// This is why ownership is a replicated fact rather than Fusion state authority: Fusion offers
    /// DestroyWhenStateAuthorityLeaves, which fires the instant authority departs and cannot wait
    /// for anything. A lease is not expressible as a flag.
    ///
    /// Long-term persistence across sessions is explicitly out of scope.
    /// </summary>
    public class GardenLease : MonoBehaviour
    {
        [SerializeField] private SomniumPlayersContainer _players;

        [Tooltip("Seconds a departed player's garden is held for them before it is cleared.")]
        [SerializeField] private float _leaseSeconds = 120f;

        /// <summary>
        /// Raised on every client the moment a player leaves — not when their lease expires.
        ///
        /// Anything holding that player's id as a *holder* must drop it at once. That is a
        /// correction rather than cleanup: they are demonstrably not holding anything, and leaving
        /// their id in place makes SingleHolderFilter refuse the object to everyone for the rest of
        /// the session. Every client sees the departure locally, so this needs no message.
        /// </summary>
        public static event System.Action<string> PlayerGone;

        private readonly Dictionary<string, float> _expiries = new Dictionary<string, float>();
        private PlantSlot[] _slots;

        private void Awake()
        {
            if (_players == null)
            {
                Logger.Error($"Awake() '{gameObject.name}' — no SomniumPlayersContainer; gardens will never be cleaned up");
                return;
            }
            _players.PlayerRemoved.AddListener(OnPlayerRemoved);
            _players.PlayerAdded.AddListener(OnPlayerAdded);
        }

        private void OnDestroy()
        {
            if (_players == null) return;
            _players.PlayerRemoved.RemoveListener(OnPlayerRemoved);
            _players.PlayerAdded.RemoveListener(OnPlayerAdded);
        }

        private void OnPlayerRemoved(ISomniumPlayer player)
        {
            string id = player?.Properties?.Id;
            if (string.IsNullOrEmpty(id)) return;

            PlayerGone?.Invoke(id);

            if (!SceneNetworking.IsMasterClient) return;
            _expiries[id] = Time.time + _leaseSeconds;
            Logger.Info($"OnPlayerRemoved() '{gameObject.name}' — '{id}' left; garden held for {_leaseSeconds}s");
        }

        /// <summary>They came back inside the window. The lease is simply forgotten.</summary>
        private void OnPlayerAdded(ISomniumPlayer player)
        {
            string id = player?.Properties?.Id;
            if (string.IsNullOrEmpty(id)) return;
            if (_expiries.Remove(id))
                Logger.Info($"OnPlayerAdded() '{gameObject.name}' — '{id}' returned in time; garden kept");
        }

        private void Update()
        {
            if (_expiries.Count == 0 || !SceneNetworking.IsMasterClient) return;

            List<string> expired = null;
            foreach (KeyValuePair<string, float> entry in _expiries)
            {
                if (Time.time < entry.Value) continue;
                expired ??= new List<string>();
                expired.Add(entry.Key);
            }

            if (expired == null) return;
            foreach (string id in expired)
            {
                _expiries.Remove(id);
                Clear(id);
            }
        }

        /// <summary>
        /// Frees every plot the player held, destroys what stands in them, and removes any seeds
        /// they bought and left lying about. Master only — this despawns things.
        /// </summary>
        private void Clear(string playerId)
        {
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
                seed.Discard();
                seeds++;
            }

            Logger.Info($"Clear() '{gameObject.name}' — lease expired for '{playerId}': {plots} plots freed, {seeds} seeds removed");
        }
    }
}
