using System;
using Fusion;
using SomniumSpace.Bridge.Components;
using SomniumSpace.Bridge.Player;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Somnium's player list, avatar rig and locomotion, behind this game's own vocabulary. This
    /// is the only file allowed to name ISomniumPlayer, SomniumPlayersContainer, or
    /// SceneNetworking's master-client flag.
    ///
    /// Managers call this. Components and orchestrators call PlayerManager, which calls this — see
    /// Layers in CLAUDE.md. Nothing here decides anything or remembers anything: it answers
    /// questions and forwards instructions, and the grace clock, the pending table and the
    /// departure events all live one layer up in PlayerManager.
    ///
    /// **A MonoBehaviour, where WorldBridge is a static class.** Somnium publishes its player list
    /// as UnityEvents, and something has to be alive to add and remove listeners. The accessors
    /// are static regardless, because there is one of these in the scene and anything wanting a
    /// player wants it from wherever it happens to be.
    ///
    /// The events below are plain C# statics, so a subscriber may attach in its own Awake without
    /// caring whether this component has woken up yet.
    /// </summary>
    public class PlayerBridge : MonoBehaviour
    {
        [SerializeField] private SomniumPlayersContainer _players;

        /// <summary>Somnium admitted a player. Every client.</summary>
        public static event Action<PlayerIdentity> PlayerAdded;

        /// <summary>Somnium dropped a player. Every client, immediately.</summary>
        public static event Action<PlayerIdentity> PlayerRemoved;

        /// <summary>Somnium admitted the local player.</summary>
        public static event Action<PlayerIdentity> LocalPlayerAdded;

        /// <summary>A remote peer entered the room and can now receive RPCs. Push it state.</summary>
        public static event Action OtherPlayerJoined;

        /// <summary>The local player is now the one who decides things for the world.</summary>
        public static event Action BecameWorldMaster
        {
            add { SceneNetworking.OnBecomeWorldMaster += value; }
            remove { SceneNetworking.OnBecomeWorldMaster -= value; }
        }

        /// <summary>
        /// Whether there is a player list behind this at all — false if the component is missing
        /// from the scene or its container was never wired. Nothing is reported to anyone when
        /// this is false, so PlayerManager says so loudly rather than running silently blind.
        /// </summary>
        public static bool IsPresent => _container != null;

        /// <summary>
        /// Whether the local player is the one who decides things for the world.
        ///
        /// Scene-wide ownership, not per-object Fusion state authority — the two are not the same
        /// question and conflating them is what this project's authority model warns about.
        /// </summary>
        public static bool IsMaster => SceneNetworking.IsMasterClient;

        /// <summary>The player with this id, or <see cref="PlayerIdentity.None"/> if nobody in the
        /// session has it — including a player this client has simply not been told about yet.</summary>
        public static PlayerIdentity GetPlayer(string somniumId)
        {
            if (_container == null || string.IsNullOrEmpty(somniumId)) return PlayerIdentity.None;
            return Identify(_container.GetPlayerByID(somniumId));
        }

        /// <summary>The local player, or <see cref="PlayerIdentity.None"/> until they have spawned.</summary>
        public static PlayerIdentity LocalPlayer =>
            _container == null ? PlayerIdentity.None : Identify(_container.LocalPlayer);

        /// <summary>
        /// The local player's avatar transforms. Check <see cref="PlayerRig.IsUsable"/> — the rig
        /// is built in pieces after the player joins, so this is empty for a while and then is not.
        /// </summary>
        public static PlayerRig LocalRig
        {
            get
            {
                var body = LocalSdkPlayer()?.References?.Body;
                if (body == null) return default;
                return new PlayerRig(body.Root, body.Head, body.LeftHand, body.RightHand);
            }
        }

        /// <summary>
        /// The local player's headset transform, or null until they've spawned.
        ///
        /// **Resolved on demand, not captured once.** Taking this at the moment Somnium adds the
        /// local player yields null whenever the avatar rig is not built yet — which it frequently
        /// is not — and a captured null is never retried, so every reader gets null for the rest of
        /// the session with nothing logged.
        ///
        /// The cached transform going Unity-null when the rig is torn down is wanted here rather
        /// than the hazard it usually is: a destroyed head reads as null, so the next read resolves
        /// the new one.
        /// </summary>
        public static Transform LocalHead
        {
            get
            {
                if (_localHead != null) return _localHead;
                _localHead = LocalSdkPlayer()?.References?.Body?.Head;
                return _localHead;
            }
        }

        /// <summary>Whether Somnium will accept locomotion instructions for the local player.</summary>
        public static bool HasLocalMotion => LocalSdkPlayer()?.Features?.Motion != null;

        /// <summary>
        /// Turns off Somnium's own flight and glide for the local player, so something of ours can
        /// drive them instead. Returns false if Somnium exposes no motion to take.
        /// </summary>
        public static bool SuppressSomniumLocomotion()
        {
            var motion = LocalSdkPlayer()?.Features?.Motion;
            if (motion == null) return false;

            motion.SetFlyModeDisableState(true);
            motion.SetGlideDisableState(true);
            return true;
        }

        /// <summary>
        /// Scales the gravity Somnium applies to the local player: 1 for normal, 0 to hold them up.
        /// Returns false if there is no motion to set.
        /// </summary>
        public static bool SetLocalGravityScale(float scale)
        {
            var motion = LocalSdkPlayer()?.Features?.Motion;
            if (motion == null) return false;

            motion.SetGravity(scale, scale);
            return true;
        }

        /// <summary>
        /// Scales Somnium's own locomotion speed for the local player: 1 for normal, 0 to take
        /// their walking away. Returns false if there is no motion to set.
        /// </summary>
        public static bool SetLocalMovementScale(float scale)
        {
            var motion = LocalSdkPlayer()?.Features?.Motion;
            if (motion == null) return false;

            motion.SetMovementSpeed(scale, scale, scale);
            return true;
        }

        /// <summary>
        /// Moves the local player. Asynchronous — <paramref name="arrived"/> is the moment they are
        /// actually there, which is what anything following a teleport should wait for. Returns
        /// false, without calling back, if there is no motion to drive.
        /// </summary>
        public static bool TeleportLocalPlayer(Vector3 position, Vector3 eulerAngles, Action arrived)
        {
            var motion = LocalSdkPlayer()?.Features?.Motion;
            if (motion == null) return false;

            motion.DoTeleportToPoint(position, eulerAngles, arrived);
            return true;
        }

        private static ISomniumPlayer LocalSdkPlayer() => _container == null ? null : _container.LocalPlayer;

        private static PlayerIdentity Identify(ISomniumPlayer player)
        {
            string id = player?.Properties?.Id;
            if (string.IsNullOrEmpty(id)) return PlayerIdentity.None;
            return new PlayerIdentity(id, player.Properties.NickName);
        }

        /// <summary>
        /// Static, because the accessors are: one of these in the scene, and everything that wants
        /// a player wants it from anywhere.
        /// </summary>
        private static SomniumPlayersContainer _container;

        private static Transform _localHead;

        private void Awake()
        {
            SceneNetworking.OnOtherPlayerJoined += OnOtherPlayerJoined;

            if (_players == null)
            {
                Logger.Error($"Awake() '{gameObject.name}' — no SomniumPlayersContainer; no player will ever be seen to arrive or leave");
                return;
            }

            _container = _players;
            _players.PlayerAdded.AddListener(OnSdkPlayerAdded);
            _players.PlayerRemoved.AddListener(OnSdkPlayerRemoved);
            _players.LocalPlayerAdded.AddListener(OnSdkLocalPlayerAdded);
        }

        private void OnDestroy()
        {
            SceneNetworking.OnOtherPlayerJoined -= OnOtherPlayerJoined;
            _container = null;

            // Statics outlive a scene when domain reload is off, and a head from the last session
            // is worse than none: it is a live-looking transform belonging to a world that is gone.
            _localHead = null;

            if (_players == null) return;
            _players.PlayerAdded.RemoveListener(OnSdkPlayerAdded);
            _players.PlayerRemoved.RemoveListener(OnSdkPlayerRemoved);
            _players.LocalPlayerAdded.RemoveListener(OnSdkLocalPlayerAdded);
        }

        private void OnSdkPlayerAdded(ISomniumPlayer player)
        {
            PlayerIdentity who = Identify(player);
            if (!who.Exists) return;

            // Temporary: ordering probe against OtherPlayerJoined. Delete once read in-world.
            Logger.Info($"OnSdkPlayerAdded() '{gameObject.name}' — '{who.Id}' t={Time.realtimeSinceStartup:F3} frame={Time.frameCount}");
            PlayerAdded?.Invoke(who);
        }

        /// <summary>A remote peer joined the Fusion room.</summary>
        private void OnOtherPlayerJoined(PlayerRef player)
        {
            // Temporary: ordering probe against OnSdkPlayerAdded. Delete once read in-world.
            Logger.Info($"OnOtherPlayerJoined() '{gameObject.name}' — peer={player.PlayerId} t={Time.realtimeSinceStartup:F3} frame={Time.frameCount}");
            OtherPlayerJoined?.Invoke();
        }

        private void OnSdkPlayerRemoved(ISomniumPlayer player)
        {
            PlayerIdentity who = Identify(player);
            if (who.Exists) PlayerRemoved?.Invoke(who);
        }

        private void OnSdkLocalPlayerAdded(ISomniumPlayer player)
        {
            PlayerIdentity who = Identify(player);
            if (!who.Exists) return;

            // Seeded, not depended on. LocalHead re-resolves for itself when the rig was not ready
            // at this moment, which is the whole reason it is not a plain field.
            _localHead = player.References?.Body?.Head;

            LocalPlayerAdded?.Invoke(who);
        }

        private void OnValidate()
        {
            if (_players == null) _players = GetComponent<SomniumPlayersContainer>();
        }
    }
}
