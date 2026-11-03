using System;
using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.DevTools
{
    /// <summary>
    /// Developer access granted for this session: the owner (Vulps) gives or takes the debug tools from anyone in the
    /// world, from the Developer Market's Access panel. A grant is a fact, so the master keeps the list: the owner asks,
    /// the master checks the ask came from the owner and announces the whole list to everyone, and again to each late
    /// joiner. Grants last until the instance ends. See docs/dev-tools.md.
    /// </summary>
    public class DeveloperAccess : MonoBehaviour
    {
        private const byte ASK_ID = 9;
        private const byte LIST_ID = 10;

        [Tooltip("SceneManager's messenger. Message ids 9-10 on it; DeveloperMarket has 7-8.")]
        [SerializeField] private NetworkMessenger _bridge;

        private static DeveloperAccess _instance;
        private static readonly HashSet<string> _granted = new HashSet<string>();
        private static readonly Dictionary<string, string> _present = new Dictionary<string, string>();   // id → name, everyone here now

        /// <summary>The granted list changed, or someone came or went: anything showing it redraws.</summary>
        public static event Action Changed;

        /// <summary>Whether a player has been granted developer access this session (the owner needs none).</summary>
        public static bool IsGranted(string playerId) => playerId != null && _granted.Contains(playerId);

        /// <summary>Everyone in the world now, id and name, the owner included.</summary>
        public static IEnumerable<KeyValuePair<string, string>> Present => _present;

        /// <summary>The owner asks to give (true) or take (false) a player's access. Only the owner's client asks.</summary>
        public static void Ask(string playerId, bool grant)
        {
            if (_instance == null || !Developer.IsLocalOwner || _instance._bridge == null || !_instance._bridge.CanSend) return;
            if (string.IsNullOrEmpty(playerId) || playerId == Developer.ID) return;
            string me = PlayerManager.LocalPlayerId ?? string.Empty;
            var writer = new BytesWriter(sizeof(short) * 2 + System.Text.Encoding.UTF8.GetByteCount(me)
                                         + System.Text.Encoding.UTF8.GetByteCount(playerId) + BytesWriter.ByteSize);
            writer.AddAutoString(me);
            writer.AddAutoString(playerId);
            writer.AddByte(grant ? (byte)1 : (byte)0);
            _instance._bridge.SendToAll(ASK_ID, writer.Data);
            Logger.Info($"Ask() 'DeveloperAccess' — asked to {(grant ? "give" : "take")} developer access {(grant ? "to" : "from")} '{playerId}'");
        }

        private void Awake()
        {
            _instance = this;
            _granted.Clear();
            _present.Clear();
        }

        private void Start()
        {
            if (_bridge != null) _bridge.MessageToAll += OnMessageToAll;
            PlayerManager.PlayerJoined += OnPlayerJoined;
            PlayerManager.PlayerLeft += OnPlayerLeft;
            PlayerManager.OtherPlayerJoined += OnOtherPlayerJoined;
        }

        private void OnDestroy()
        {
            if (_bridge != null) _bridge.MessageToAll -= OnMessageToAll;
            PlayerManager.PlayerJoined -= OnPlayerJoined;
            PlayerManager.PlayerLeft -= OnPlayerLeft;
            PlayerManager.OtherPlayerJoined -= OnOtherPlayerJoined;
            if (_instance == this) _instance = null;
            Changed = null;
        }

        private void OnPlayerJoined(string id, string name)
        {
            if (string.IsNullOrEmpty(id)) return;
            _present[id] = string.IsNullOrEmpty(name) ? id : name;
            Changed?.Invoke();
        }

        // A grant outlives a dropout: it is for the session, and someone who rejoins keeps it.
        private void OnPlayerLeft(string id)
        {
            if (id != null && _present.Remove(id)) Changed?.Invoke();
        }

        /// <summary>A late joiner hears the list from the master.</summary>
        private void OnOtherPlayerJoined()
        {
            if (PlayerManager.IsMaster && _granted.Count > 0) Announce();
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            if (id == ASK_ID) OnAsked(data);
            else if (id == LIST_ID) OnList(data);
        }

        /// <summary>Master only. An ask from anyone but the owner is refused out loud.</summary>
        private void OnAsked(byte[] data)
        {
            if (!PlayerManager.IsMaster) return;
            var reader = new BytesReader(data);
            string requester = reader.NextAutoString();
            string player = reader.NextAutoString();
            bool grant = reader.NextByte() == 1;
            if (requester != Developer.ID) { Logger.Warn($"OnAsked() '{gameObject.name}' — '{requester}' is not the owner; refused"); return; }
            if (string.IsNullOrEmpty(player) || player == Developer.ID) return;
            bool changed = grant ? _granted.Add(player) : _granted.Remove(player);
            Logger.Info($"OnAsked() '{gameObject.name}' — developer access {(grant ? "given to" : "taken from")} '{player}'{(changed ? "" : " (already so)")}; {_granted.Count} granted");
            Announce();
        }

        private void Announce()
        {
            if (_bridge == null || !_bridge.CanSend) return;
            int size = BytesWriter.IntSize;
            foreach (string id in _granted) size += sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(id);
            var writer = new BytesWriter(size);
            writer.AddInt(_granted.Count);
            foreach (string id in _granted) writer.AddAutoString(id);
            _bridge.SendToAll(LIST_ID, writer.Data);
        }

        private void OnList(byte[] data)
        {
            var reader = new BytesReader(data);
            int count = reader.NextInt();
            _granted.Clear();
            for (int i = 0; i < count; i++) _granted.Add(reader.NextAutoString());
            Logger.Info($"OnList() '{gameObject.name}' — {count} granted developer access; this player {(Developer.IsLocal ? "has" : "has not")}");
            Changed?.Invoke();
        }
    }
}
