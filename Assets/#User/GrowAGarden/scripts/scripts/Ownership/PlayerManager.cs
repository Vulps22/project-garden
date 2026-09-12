using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// What the rest of the game asks about players, and the only thing that decides when a player
    /// who left is not coming back.
    ///
    /// Everything downstream answers one question — "what do I clear up for this id?" — rather than
    /// each keeping its own clock. Before this, GardenLease owned both the timer and the cleanup,
    /// and anything else needing to tidy up after a departure would have had to own a second timer
    /// that agreed with the first.
    ///
    /// **Somnium is not named here.** The SDK's player list, avatar rig and locomotion sit behind
    /// <see cref="PlayerBridge"/> one layer down; this is the callsite everything else uses. Several
    /// members below forward a single line to it and nothing more, which is the job — when a guard,
    /// a cache or a correction is needed later it lands in one method rather than in every seed,
    /// plant and point that asked.
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

        /// <summary>Whether the local player is the one who decides things for the world.</summary>
        public static bool IsMaster => PlayerBridge.IsMaster;

        /// <summary>A remote peer entered the room and can now receive RPCs. Push it state.</summary>
        public static event System.Action OtherPlayerJoined
        {
            add { PlayerBridge.OtherPlayerJoined += value; }
            remove { PlayerBridge.OtherPlayerJoined -= value; }
        }

        /// <summary>The local player is now the one who decides things for the world.</summary>
        public static event System.Action BecameWorldMaster
        {
            add { PlayerBridge.BecameWorldMaster += value; }
            remove { PlayerBridge.BecameWorldMaster -= value; }
        }

        /// <summary>
        /// The player with this id, or <see cref="PlayerIdentity.None"/> if nobody in the session
        /// has it.
        ///
        /// Straight through to Somnium's own list rather than a roster of our own. It already keeps
        /// one, it is authoritative, and every client has the same view of it with no message sent
        /// — so a name or an identity is a local read, not a fact that has to arrive.
        ///
        /// Ownership code used to ask EconomyManager's balance table for this. That table answers a
        /// different question — who has money — and is cleared and rebuilt from scratch on every
        /// broadcast, so a lookup could come back empty for reasons that had nothing to do with
        /// whether the player was standing there.
        /// </summary>
        public static PlayerIdentity GetPlayer(string somniumId) => PlayerBridge.GetPlayer(somniumId);

        /// <summary>The local player, or <see cref="PlayerIdentity.None"/> until they have spawned.</summary>
        public static PlayerIdentity GetLocalPlayer() => PlayerBridge.LocalPlayer;

        /// <summary>
        /// The local player's headset transform, or null until they've spawned. Anything that needs
        /// "where is the local player right now" — CloudGenerator's spawn anchor, say — reads this.
        /// It re-resolves itself, so a null now does not mean null forever.
        /// </summary>
        public static Transform LocalPlayerHead => PlayerBridge.LocalHead;

        /// <summary>
        /// The local player's avatar transforms. Check <see cref="PlayerRig.IsUsable"/>: the rig is
        /// assembled after the player joins, so this is empty for a while and then is not.
        /// </summary>
        public static PlayerRig LocalPlayerRig => PlayerBridge.LocalRig;

        /// <summary>Whether Somnium will accept locomotion instructions for the local player.</summary>
        public static bool CanDriveLocalPlayer => PlayerBridge.HasLocalMotion;

        /// <summary>
        /// Turns Somnium's own flight and glide off for the local player so ours can drive them
        /// instead. False if Somnium exposes no motion to take.
        /// </summary>
        public static bool SuppressSomniumLocomotion() => PlayerBridge.SuppressSomniumLocomotion();

        /// <summary>Scales gravity on the local player: 1 normal, 0 to hold them up. False if there
        /// is no motion to set.</summary>
        public static bool SetLocalGravityScale(float scale) => PlayerBridge.SetLocalGravityScale(scale);

        /// <summary>Scales Somnium's walking speed for the local player: 1 normal, 0 to take it
        /// away. False if there is no motion to set.</summary>
        public static bool SetLocalMovementScale(float scale) => PlayerBridge.SetLocalMovementScale(scale);

        /// <summary>
        /// Moves the local player. Asynchronous — <paramref name="arrived"/> is when they are
        /// actually there, and anything that follows a teleport belongs in it. False, with no
        /// callback, if there is no motion to drive.
        /// </summary>
        public static bool TeleportLocalPlayer(Vector3 position, Vector3 eulerAngles, System.Action arrived)
            => PlayerBridge.TeleportLocalPlayer(position, eulerAngles, arrived);

        /// <summary>Departed ids, and the time each stops being reprievable.</summary>
        private readonly Dictionary<string, float> _pending = new Dictionary<string, float>();

        private void Awake()
        {
            PlayerBridge.PlayerAdded += OnPlayerAdded;
            PlayerBridge.PlayerRemoved += OnPlayerRemoved;
            PlayerBridge.LocalPlayerAdded += OnLocalPlayerAdded;
        }

        /// <summary>
        /// Reports a missing bridge. In Start, not Awake: the bridge registers itself in its own
        /// Awake and the order between the two is not ours to decide.
        /// </summary>
        private void Start()
        {
            if (PlayerBridge.IsPresent) return;
            Logger.Error($"Start() '{gameObject.name}' — no PlayerBridge in the scene; no player will ever be seen to arrive or leave, and nothing will be cleaned up after one");
        }

        private void OnDestroy()
        {
            PlayerBridge.PlayerAdded -= OnPlayerAdded;
            PlayerBridge.PlayerRemoved -= OnPlayerRemoved;
            PlayerBridge.LocalPlayerAdded -= OnLocalPlayerAdded;

            // Statics outlive a scene when domain reload is off, and an id from the last session is
            // worse than none.
            LocalPlayerId = null;
        }

        private void OnPlayerAdded(PlayerIdentity player)
        {
            // Back inside the window: the pending destroy is simply forgotten. Non-masters clear it
            // too, so nobody is left holding a stale deadline for a player who is standing here.
            if (_pending.Remove(player.Id))
                Logger.Info($"OnPlayerAdded() '{gameObject.name}' — '{player.Id}' returned in time; nothing cleared");

            PlayerJoined?.Invoke(player.Id, player.Name);
        }

        private void OnLocalPlayerAdded(PlayerIdentity player)
        {
            LocalPlayerId = player.Id;
            LocalPlayerJoined?.Invoke(player.Id, player.Name);
        }

        private void OnPlayerRemoved(PlayerIdentity player)
        {
            PlayerLeft?.Invoke(player.Id);

            // Every client, deliberately — not just the master. This deadline is what lets whoever
            // is master when it expires do the cleanup, including a master that took over after
            // the player left.
            _pending[player.Id] = Time.time + _graceSeconds;
            Logger.Info($"OnPlayerRemoved() '{gameObject.name}' — '{player.Id}' left; held for {_graceSeconds}s");
        }

        private void Update()
        {
            if (_pending.Count == 0 || !IsMaster) return;

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
    }
}
