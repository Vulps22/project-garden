using System.Collections.Generic;
using SomniumSpace.Bridge.Components;
using SomniumSpace.Bridge.Player;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The one point of contact with Somnium's player list, and the only thing that decides when a
    /// player who left is not coming back.
    ///
    /// Everything downstream answers one question — "what do I clear up for this id?" — rather than
    /// each keeping its own clock. Before this, GardenLease owned both the timer and the cleanup,
    /// and anything else needing to tidy up after a departure would have had to own a second timer
    /// that agreed with the first.
    ///
    /// **Two leave events, and they are not one event at two times.**
    ///
    /// <see cref="PlayerLeft"/> fires on every client the instant someone goes. It is a
    /// *correction*: they are demonstrably holding nothing, and leaving their id set as a holder
    /// makes SingleHolderFilter refuse that object to everyone for the rest of the session.
    ///
    /// <see cref="PlayerDestroyed"/> fires on the master alone, once the grace period expires, and
    /// means erase what was theirs. That is cleanup, and it must wait — a dropout is
    /// indistinguishable from leaving, and losing a garden to a flaky connection is a miserable way
    /// to find that out.
    ///
    /// **Every client runs the countdown; only the master acts on it.** Somnium raises
    /// PlayerRemoved on every peer, so each derives the same deadline from an event they all saw,
    /// and a master handover mid-countdown loses nothing. The timer this replaced was master-local
    /// state: when the master left, every pending cleanup went with it, and that player's plots
    /// stayed held and their name stayed on the balance board for the rest of the session.
    ///
    /// Known gap: a client that joins *after* someone left never saw the departure, so if it later
    /// becomes master that player is never destroyed. Closing it means the master announcing
    /// pending departures on late-join, which is the usual repair path and is not built yet.
    /// </summary>
    public class PlayerManager : MonoBehaviour
    {
        [SerializeField] private SomniumPlayersContainer _players;

        [Tooltip("Seconds after a player leaves before what they owned is cleared. A dropout is " +
                 "indistinguishable from leaving, so this is a grace period, not a delay.")]
        [SerializeField] private float _graceSeconds = 180f;

        /// <summary>A player is here. Every client.</summary>
        public static event System.Action<string, string> PlayerJoined;

        /// <summary>The local player is here, and <see cref="LocalPlayerId"/> is now set.</summary>
        public static event System.Action<string, string> LocalPlayerJoined;

        /// <summary>
        /// A player has gone. Every client, immediately. A correction, not cleanup — drop their
        /// HolderId at once. Nothing should be destroyed on this event.
        /// </summary>
        public static event System.Action<string> PlayerLeft;

        /// <summary>
        /// They did not come back. **Master only**, once the grace period expires. Destroying is
        /// allowed here and nowhere else.
        /// </summary>
        public static event System.Action<string> PlayerDestroyed;

        public static string LocalPlayerId { get; private set; }

        /// <summary>
        /// The local player's headset transform, or null until they've spawned. The one point of
        /// contact with Somnium's player list per the class docstring — anything that needs "where
        /// is the local player right now" (CloudGenerator's spawn anchor, say) reads this rather
        /// than holding its own SomniumPlayersContainer reference.
        /// </summary>
        public static Transform LocalPlayerHead { get; private set; }

        /// <summary>Departed ids, and the time each stops being reprievable.</summary>
        private readonly Dictionary<string, float> _pending = new Dictionary<string, float>();

        private void Awake()
        {
            if (_players == null)
            {
                Logger.Error($"Awake() '{gameObject.name}' — no SomniumPlayersContainer; nothing will ever be cleaned up after a player leaves");
                return;
            }
            _players.PlayerAdded.AddListener(OnPlayerAdded);
            _players.PlayerRemoved.AddListener(OnPlayerRemoved);
            _players.LocalPlayerAdded.AddListener(OnLocalPlayerAdded);
        }

        private void OnDestroy()
        {
            if (_players == null) return;
            _players.PlayerAdded.RemoveListener(OnPlayerAdded);
            _players.PlayerRemoved.RemoveListener(OnPlayerRemoved);
            _players.LocalPlayerAdded.RemoveListener(OnLocalPlayerAdded);
        }

        private void OnPlayerAdded(ISomniumPlayer player)
        {
            string id = player?.Properties?.Id;
            if (string.IsNullOrEmpty(id)) return;

            // Back inside the window: the pending destroy is simply forgotten. Non-masters clear it
            // too, so nobody is left holding a stale deadline for a player who is standing here.
            if (_pending.Remove(id))
                Logger.Info($"OnPlayerAdded() '{gameObject.name}' — '{id}' returned in time; nothing cleared");

            PlayerJoined?.Invoke(id, player.Properties.NickName);
        }

        private void OnLocalPlayerAdded(ISomniumPlayer player)
        {
            string id = player?.Properties?.Id;
            if (string.IsNullOrEmpty(id)) return;

            LocalPlayerId = id;
            LocalPlayerHead = player?.References?.Body?.Head;
            LocalPlayerJoined?.Invoke(id, player.Properties.NickName);
        }

        private void OnPlayerRemoved(ISomniumPlayer player)
        {
            string id = player?.Properties?.Id;
            if (string.IsNullOrEmpty(id)) return;

            PlayerLeft?.Invoke(id);

            // Every client, deliberately — not just the master. This deadline is what lets whoever
            // is master when it expires do the cleanup, including a master that took over after
            // the player left.
            _pending[id] = Time.time + _graceSeconds;
            Logger.Info($"OnPlayerRemoved() '{gameObject.name}' — '{id}' left; held for {_graceSeconds}s");
        }

        private void Update()
        {
            if (_pending.Count == 0 || !SceneNetworking.IsMasterClient) return;

            List<string> expired = null;
            foreach (KeyValuePair<string, float> entry in _pending)
            {
                if (Time.time < entry.Value) continue;
                expired ??= new List<string>();
                expired.Add(entry.Key);
            }

            if (expired == null) return;
            foreach (string id in expired)
            {
                _pending.Remove(id);
                Logger.Info($"Update() '{gameObject.name}' — grace expired for '{id}'; destroying");
                PlayerDestroyed?.Invoke(id);
            }
        }

        private void OnValidate()
        {
            if (_players == null) _players = GetComponent<SomniumPlayersContainer>();
        }
    }
}
