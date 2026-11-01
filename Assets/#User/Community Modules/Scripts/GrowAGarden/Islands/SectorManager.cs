using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vulps.GrowAGarden.Islands
{
    /// <summary>Which sector every player is in; the table authority is handed out from. Each client announces its own.</summary>
    public class SectorManager : MonoBehaviour
    {
        private const byte MESSAGE_ID = 2;

        [Tooltip("SceneManager's messenger. Message id 2 on it; EconomyManager uses 0, WorldSeed 1, ShopKeeper 3-6.")]
        [SerializeField] private NetworkMessenger _bridge;

        [Tooltip("The IslandField's transform. Sector 0 is centred here.")]
        [SerializeField] private Transform _origin;

        [Tooltip("Width of a sector, in metres.")]
        [SerializeField] private float _sectorSize = 1000f;

        [Tooltip("Outside the local sector, how far from the player islands are certain to be loaded. Keep well inside IslandField's view radius.")]
        [SerializeField] private float _groundRadius = 600f;

        [Tooltip("Seconds between checks of the local player's sector.")]
        [SerializeField] private float _checkInterval = 0.25f;

        private static SectorManager _instance;
        private static readonly Dictionary<int, Vector2Int> _sectors = new Dictionary<int, Vector2Int>();
        private float _nextCheck;
        private int _announcedPeer = -1;

        /// <summary>Raised on every client when any player's sector changes, or a player arrives or leaves.</summary>
        public static event Action SectorsChanged;

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

        /// <summary>Whether this client certainly has the islands loaded at a world point: in its own sector, or close to the player.</summary>
        public static bool HasGroundAt(Vector3 world)
        {
            if (!HasLocalSector) return false;
            if (SectorOf(world) == LocalSector) return true;
            Vector3? me = LocalPosition();
            if (me == null || _instance == null) return false;
            float x = world.x - me.Value.x, z = world.z - me.Value.z;
            return x * x + z * z <= _instance._groundRadius * _instance._groundRadius;
        }

        /// <summary>The peer that should simulate things in a sector: the lowest peer id there, or -1 when nobody is.</summary>
        public static int PickFor(Vector2Int sector)
        {
            int pick = -1;
            foreach (KeyValuePair<int, Vector2Int> entry in _sectors)
                if (entry.Value == sector && (pick < 0 || entry.Key < pick)) pick = entry.Key;
            return pick;
        }

        /// <summary>Whether a peer is known to be in a sector.</summary>
        public static bool IsPeerIn(int peerId, Vector2Int sector) =>
            _sectors.TryGetValue(peerId, out Vector2Int at) && at == sector;

        /// <summary>This client's peer id, or -1 before it has one.</summary>
        public static int LocalPeerId => WorldBridge.LocalPeerId;

        /// <summary>The local player's position: head, else a camera; null before either exists.</summary>
        public static Vector3? LocalPosition()
        {
            Transform head = PlayerBridge.LocalHead;
            if (head != null) return head.position;
            if (Camera.main != null) return Camera.main.transform.position;
            return null;
        }

        private void Awake()
        {
            _instance = this;
            _sectors.Clear();
            HasLocalSector = false;
        }

        private void Start()
        {
            _bridge.MessageToAll += OnMessageToAll;
            PlayerBridge.OtherPlayerJoined += Announce;
            PlayerBridge.PeerLeft += OnPeerLeft;
        }

        private void OnDestroy()
        {
            if (_bridge != null) _bridge.MessageToAll -= OnMessageToAll;
            PlayerBridge.OtherPlayerJoined -= Announce;
            PlayerBridge.PeerLeft -= OnPeerLeft;
            if (_instance == this) _instance = null;
            _sectors.Clear();
            HasLocalSector = false;
            SectorsChanged = null;
        }

        private void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + _checkInterval;

            Vector3? me = LocalPosition();
            if (me == null) return;

            Vector2Int sector = SectorOf(me.Value);
            int peer = LocalPeerId;
            bool moved = !HasLocalSector || sector != LocalSector;
            if (!moved && peer == _announcedPeer) return;

            LocalSector = sector;
            HasLocalSector = true;
            if (moved) Logger.Info($"Update() '{gameObject.name}' — now in sector {sector.x},{sector.y}");

            if (peer < 0)
            {
                SectorsChanged?.Invoke();
                return;
            }
            _sectors[peer] = sector;
            Announce();
            if (moved) SectorsChanged?.Invoke();
        }

        /// <summary>Tells everyone which sector the local player is in.</summary>
        private void Announce()
        {
            int peer = LocalPeerId;
            if (peer < 0 || !HasLocalSector || _bridge == null || !_bridge.CanSend) return;

            _announcedPeer = peer;
            var writer = new BytesWriter(BytesWriter.IntSize * 3);
            writer.AddInt(peer);
            writer.AddInt(LocalSector.x);
            writer.AddInt(LocalSector.y);
            _bridge.SendToAll(MESSAGE_ID, writer.Data);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
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
            if (_groundRadius < 0f) _groundRadius = 0f;
            if (_checkInterval < 0.05f) _checkInterval = 0.05f;
        }
    }
}
