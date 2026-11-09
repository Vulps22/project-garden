using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vulps.GrowAGarden.Islands
{
    /// <summary>Which sector every player is in; the table authority is handed out from. Each client announces its own.</summary>
    public class SectorManager : MonoBehaviour
    {
        private const byte MESSAGE_ID = 2;
        private const byte OBJECT_MOVED_ID = 14;

        [Tooltip("SceneManager's messenger. Message ids 2 and 14 on it; EconomyManager uses 0, WorldSeed 1, ShopKeeper 3-6.")]
        [SerializeField] private NetworkMessenger _bridge;

        [Tooltip("The IslandField's transform. Sector 0 is centred here.")]
        [SerializeField] private Transform _origin;

        [Tooltip("Width of a sector, in metres.")]
        [SerializeField] private float _sectorSize = 1000f;

        private static SectorManager _instance;
        private static readonly Dictionary<int, Vector2Int> _sectors = new Dictionary<int, Vector2Int>();
        private static readonly Dictionary<uint, Action<Vector2Int>> _objects = new Dictionary<uint, Action<Vector2Int>>();

        /// <summary>Raised on every client when any player's sector changes, or a player arrives or leaves.</summary>
        public static event Action SectorsChanged;

        /// <summary>Raised when the local player moves into another sector.</summary>
        public static event Action LocalSectorChanged;

        /// <summary>Whether sector maths can be done yet: the manager and its origin exist.</summary>
        public static bool IsReady => _instance != null && _instance._origin != null;

        /// <summary>Whether this client knows where it is yet.</summary>
        public static bool HasLocalSector { get; private set; }

        /// <summary>The sector the local player is in.</summary>
        public static Vector2Int LocalSector { get; private set; }

        /// <summary>Width of a sector, in metres.</summary>
        public static float SectorSize => _instance != null ? _instance._sectorSize : 1000f;

        /// <summary>Where sector 0's centre is, in world space.</summary>
        public static Vector3 Origin => _instance != null && _instance._origin != null ? _instance._origin.position : Vector3.zero;

        /// <summary>The sector a world point is in.</summary>
        public static Vector2Int SectorOf(Vector3 world)
        {
            Vector3 local = world - Origin;
            return new Vector2Int(Mathf.RoundToInt(local.x / SectorSize), Mathf.RoundToInt(local.z / SectorSize));
        }

        /// <summary>A sector's centre, in world space, at the origin's height.</summary>
        public static Vector3 SectorCentre(Vector2Int sector) => Origin + new Vector3(sector.x * SectorSize, 0f, sector.y * SectorSize);

        /// <summary>Whether this client has the ground at a world point loaded: an island cell it has placed, or a Garden.</summary>
        public static bool HasGroundAt(Vector3 world) => IslandField.HasGroundAt(world);

        /// <summary>An object hears, on every client, the sector it was reported in (<see cref="ReportObjectSector"/>).</summary>
        public static void RegisterObject(uint networkId, Action<Vector2Int> moved) { if (networkId != 0) _objects[networkId] = moved; }
        public static void UnregisterObject(uint networkId) => _objects.Remove(networkId);

        /// <summary>
        /// The client simulating an object says which sector it has come to rest in, so the right player can take it over.
        /// Sent only when that sector changes, from the object's own events (released, landed, recalled).
        /// </summary>
        public static void ReportObjectSector(uint networkId, Vector2Int sector)
        {
            if (_instance == null || _instance._bridge == null || !_instance._bridge.CanSend || networkId == 0) return;
            var writer = new BytesWriter(BytesWriter.IntSize * 3);
            writer.AddInt((int)networkId);
            writer.AddInt(sector.x);
            writer.AddInt(sector.y);
            _instance._bridge.SendToAll(OBJECT_MOVED_ID, writer.Data);
        }

        /// <summary>The peer that should simulate things in a sector: the lowest peer id there, or -1 when nobody is.</summary>
        public static int PickFor(Vector2Int sector)
        {
            int pick = -1;
            foreach (KeyValuePair<int, Vector2Int> entry in _sectors)
                if (entry.Value == sector && (pick < 0 || entry.Key < pick)) pick = entry.Key;
            return pick;
        }

        /// <summary>Whether the table knows where a peer is at all. A peer that has not announced yet is unknown, not absent.</summary>
        public static bool IsKnownPeer(int peerId) => _sectors.ContainsKey(peerId);

        /// <summary>Whether a peer is known to be in a sector.</summary>
        public static bool IsPeerIn(int peerId, Vector2Int sector) =>
            _sectors.TryGetValue(peerId, out Vector2Int at) && at == sector;

        /// <summary>This client's peer id, or -1 before it has one.</summary>
        public static int LocalPeerId => WorldBridge.LocalPeerId;

        private void Awake()
        {
            _instance = this;
            _sectors.Clear();
            _objects.Clear();
            HasLocalSector = false;
        }

        private void Start()
        {
            _bridge.MessageToAll += OnMessageToAll;
            PlayerBridge.OtherPlayerJoined += Announce;
            PlayerBridge.PeerLeft += OnPeerLeft;
            if (WorldBridge.IsNetworkReady) OnNetworkReady();
            else WorldBridge.NetworkReady += OnNetworkReady;
            PlayerTracker.Watch(SectorOf, OnPlayerMoved);
        }

        private void OnDestroy()
        {
            if (_bridge != null) { _bridge.MessageToAll -= OnMessageToAll; _bridge.OnSpawned -= OnBridgeSpawned; }
            PlayerBridge.OtherPlayerJoined -= Announce;
            PlayerBridge.PeerLeft -= OnPeerLeft;
            WorldBridge.NetworkReady -= OnNetworkReady;
            PlayerTracker.Unwatch(OnPlayerMoved);
            if (_instance == this) _instance = null;
            _sectors.Clear();
            _objects.Clear();
            HasLocalSector = false;
            SectorsChanged = null;
            LocalSectorChanged = null;
        }

        /// <summary>The player crossed into another sector (the tracker calls only then).</summary>
        private void OnPlayerMoved(Vector3 at)
        {
            LocalSector = SectorOf(at);
            HasLocalSector = true;
            Logger.Info($"OnPlayerMoved() '{gameObject.name}' — now in sector {LocalSector.x},{LocalSector.y}");
            int peer = LocalPeerId;
            if (peer >= 0) { _sectors[peer] = LocalSector; Announce(); }
            LocalSectorChanged?.Invoke();
            SectorsChanged?.Invoke();
        }

        /// <summary>This client has a peer id now: put itself in the table and tell everyone.</summary>
        private void OnNetworkReady()
        {
            int peer = LocalPeerId;
            if (peer < 0 || !HasLocalSector) return;
            _sectors[peer] = LocalSector;
            Announce();
            SectorsChanged?.Invoke();
        }

        private void OnBridgeSpawned()
        {
            _bridge.OnSpawned -= OnBridgeSpawned;
            Logger.Info($"OnBridgeSpawned() '{gameObject.name}' — messenger attached; announcing sector {LocalSector.x},{LocalSector.y}");
            Announce();
        }

        /// <summary>Tells everyone which sector the local player is in.</summary>
        private void Announce()
        {
            int peer = LocalPeerId;
            if (peer < 0 || !HasLocalSector || _bridge == null) return;
            if (!_bridge.CanSend)
            {
                // Still attaching (a late joiner): announce when the messenger spawns.
                _bridge.OnSpawned -= OnBridgeSpawned;
                _bridge.OnSpawned += OnBridgeSpawned;
                return;
            }

            var writer = new BytesWriter(BytesWriter.IntSize * 3);
            writer.AddInt(peer);
            writer.AddInt(LocalSector.x);
            writer.AddInt(LocalSector.y);
            _bridge.SendToAll(MESSAGE_ID, writer.Data);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            if (id == OBJECT_MOVED_ID)
            {
                var moved = new BytesReader(data);
                uint objectId = (uint)moved.NextInt();
                var at = new Vector2Int(moved.NextInt(), moved.NextInt());
                if (_objects.TryGetValue(objectId, out Action<Vector2Int> heard)) heard(at);
                return;
            }
            if (id != MESSAGE_ID) return;

            var reader = new BytesReader(data);
            int peer = reader.NextInt();
            var sector = new Vector2Int(reader.NextInt(), reader.NextInt());
            if (_sectors.TryGetValue(peer, out Vector2Int known) && known == sector) return;

            _sectors[peer] = sector;
            SectorsChanged?.Invoke();
        }

        private void OnPeerLeft(int peer)
        {
            if (!_sectors.Remove(peer)) return;
            Logger.Info($"OnPeerLeft() '{gameObject.name}' — peer {peer} left; sector table now holds {_sectors.Count}");
            SectorsChanged?.Invoke();
        }

        private void OnValidate()
        {
            if (_bridge == null) _bridge = GetComponent<NetworkMessenger>();
            if (_sectorSize < 10f) _sectorSize = 10f;
        }
    }
}
