using Fusion;
using SomniumSpace.Network.Bridge;
using System.Collections;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// A plot. Decides what may be planted in it, spawns the plant, and remembers whose it is.
    ///
    /// The decision lives here, on the master, rather than on the seed, because planting is a fact
    /// and facts come from the master. It used to happen on whichever client held the seed's state
    /// authority — the buyer — which is why plants could land in the wrong plot: two clients each
    /// resolved an overlapping trigger their own way. The master now spawns the plant *at* the plot
    /// it chose, so there is no trigger left to disagree about.
    /// </summary>
    public class PlantSlot : MonoBehaviour
    {
        [SerializeField] NetworkBridge _networkBridge;

        [Tooltip("How long the plot waits to be given ownership of a seed before giving up on " +
                 "clearing it away. A safety net, not a normal path.")]
        [SerializeField] private float _authorityTimeout = 2f;

        public bool IsOccupied { get; private set; }

        /// <summary>
        /// Whose plot this is, or null when free.
        ///
        /// Deliberately not Fusion state authority. Authority transfers on hover, is the master's
        /// channel for acting on objects at all, and vanishes the moment a player disconnects —
        /// three reasons it cannot answer "whose is this". This is a fact, decided by the master
        /// and replicated, exactly as IHeldObject.HolderId is one level down.
        /// </summary>
        public string OwnerId { get; private set; }

        /// <summary>
        /// What is standing here — a plant while it grows, then a rooted produce once the plant has
        /// become the crop. Master-side bookkeeping for the garden lease.
        /// </summary>
        public IPlotOccupant Occupant { get; private set; }

        /// <summary>
        /// Hands the plot from one occupant to the next without ever freeing it. A rooted plant
        /// despawns the moment it is grown, and if the ground went free in that gap a player could
        /// sow a seed into a plot that visibly still holds a carrot.
        /// </summary>
        public void SetOccupant(IPlotOccupant occupant) => Occupant = occupant;

        private void Start()
        {
            if (_networkBridge == null)
            {
                Logger.Error($"Start() '{gameObject.name}' — _networkBridge is NULL! RPC messages won't work!");
                return;
            }
            _networkBridge.OnMessageToAll += OnMessageToAll;
        }

        private void OnDestroy()
        {
            if (_networkBridge != null) _networkBridge.OnMessageToAll -= OnMessageToAll;
        }

        /// <summary>True when this player may plant here — their plot, or nobody's.</summary>
        public bool CanBeUsedBy(string playerId) =>
            !IsOccupied && (string.IsNullOrEmpty(OwnerId) || OwnerId == playerId);

        // ── Planter's side ────────────────────────────────────────────────────────

        /// <summary>
        /// Offers something to this plot. Called by the holder's own client, because only that
        /// machine knows its hand is on the thing being offered.
        ///
        /// Sent on the plot's bridge rather than the seed's, and names the seed by NetworkId, the
        /// way SellPoint already names what is being sold. Keyed on IPlantable rather than on Seed
        /// so that planting something which is not a seed later costs a prefab, not a refactor.
        /// </summary>
        public void RequestPlant(IPlantable plantable)
        {
            if (plantable == null || plantable.NetworkId == 0) return;

            string owner = plantable.OwnerId ?? string.Empty;
            int size = BytesWriter.IntSize + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(owner);
            var writer = new BytesWriter(size);
            writer.AddInt((int)plantable.NetworkId);
            writer.AddString(owner);
            _networkBridge.RPC_SendMessageToAll((byte)PlantSlotMessageType.PlantRequest, writer.Data);
        }

        // ── Plot's side ───────────────────────────────────────────────────────────

        /// <summary>Master client only — the plot is the only one who accepts a planting.</summary>
        private void OnPlantRequested(byte[] data)
        {
            if (!SceneNetworking.IsMasterClient) return;

            var reader = new BytesReader(data);
            if (!reader.IsValid) return;
            uint plantableId = (uint)reader.NextInt();
            string ownerId = reader.NextString();

            IPlantable plantable = FindPlantable(plantableId);
            if (plantable == null)
            {
                Logger.Warn($"OnPlantRequested() '{gameObject.name}' — nothing found with NetworkId={plantableId}");
                return;
            }

            if (!plantable.CanBePlanted)
            {
                Logger.Warn($"OnPlantRequested() '{gameObject.name}' — '{plantableId}' is not in a plantable state; refused");
                return;
            }

            if (!CanBeUsedBy(ownerId))
            {
                Logger.Warn($"OnPlantRequested() '{gameObject.name}' — occupied={IsOccupied} owner='{OwnerId}' refuses '{ownerId}'");
                return;
            }

            Plant plant = SpawnPlant(plantable.PlantPrefab);
            if (plant == null) return;

            SetOccupied(true, ownerId);
            Occupant = plant;
            plant.Init(this, System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            // Announced to everyone so the seed leaves every hand at once; the despawn follows
            // once this client has taken ownership of it.
            var writer = new BytesWriter(BytesWriter.IntSize);
            writer.AddInt((int)plantableId);
            _networkBridge.RPC_SendMessageToAll((byte)PlantSlotMessageType.Planted, writer.Data);

            Logger.Info($"OnPlantRequested() '{gameObject.name}' — planted '{plant.name}' for '{ownerId}'");
            StartCoroutine(TakeOwnershipAndDespawn(plantableId));
        }

        /// <summary>
        /// Removes the seed that became this plant.
        ///
        /// Despawn silently does nothing without state authority, and the planter owns the seed by
        /// this point — NetworkGrabbable took authority as their hand approached. So the plot asks
        /// for it and then acts, which is the same shape SellPoint uses to take a plant off a
        /// seller. Latency is free: the seed is already invisible in every hand.
        /// </summary>
        private IEnumerator TakeOwnershipAndDespawn(uint plantableId)
        {
            NetworkObject obj = FindObject(plantableId);
            if (obj == null) yield break;

            if (!obj.HasStateAuthority)
            {
                obj.RequestStateAuthority();
                float waited = 0f;
                while (!obj.HasStateAuthority && waited < _authorityTimeout)
                {
                    yield return null;
                    waited += Time.deltaTime;
                }
            }

            if (!obj.HasStateAuthority)
            {
                Logger.Warn($"TakeOwnershipAndDespawn() '{gameObject.name}' — could not take ownership of the seed within {_authorityTimeout}s; it will linger invisible");
                yield break;
            }

            SceneNetworking.NetworkRunnerRef.Despawn(obj);
        }

        private Plant SpawnPlant(NetworkObject prefab)
        {
            if (prefab == null)
            {
                Logger.Error($"SpawnPlant() '{gameObject.name}' — the seed names no plant prefab");
                return null;
            }

            SceneNetworking net = SceneNetworking.Instance;
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (net == null || runner == null) return null;

            if (!net.NetworkPrefabs.TryGetValue(prefab, out NetworkPrefabId prefabId))
            {
                Logger.Error($"SpawnPlant() '{gameObject.name}' — '{prefab.name}' is not registered on SceneNetworking");
                return null;
            }

            NetworkObject spawned;
            try
            {
                spawned = runner.Spawn(prefabId, transform.position, transform.rotation, null, null,
                                       NetworkSpawnFlags.SharedModeStateAuthMasterClient);
            }
            catch (System.Exception e)
            {
                Logger.Error($"SpawnPlant() '{gameObject.name}' — Fusion threw spawning '{prefab.name}': {e.Message}");
                return null;
            }

            if (spawned == null) return null;

            // Explicit, for the same reason SpawnProduce is: the pose handed to Spawn() is local
            // to the spawner and not networked.
            spawned.transform.SetPositionAndRotation(transform.position, transform.rotation);

            Plant plant = spawned.GetComponent<Plant>();
            if (plant == null) Logger.Error($"SpawnPlant() '{gameObject.name}' — '{spawned.name}' has no Plant component");
            return plant;
        }

        // ── Occupancy ─────────────────────────────────────────────────────────────

        /// <summary>Claims the plot for a player. Master only; announced to everyone.</summary>
        public void SetOccupied(bool occupied, string ownerId)
        {
            IsOccupied = occupied;
            OwnerId = occupied ? ownerId : null;
            if (!occupied) Occupant = null;
            AnnounceOccupancy();
        }

        /// <summary>
        /// Whatever was standing here has gone. The plot keeps its owner: a player who has planted
        /// here once keeps the plot until their lease expires, so nobody can take the ground out
        /// from under them between harvesting one crop and sowing the next.
        /// </summary>
        public void Release()
        {
            IsOccupied = false;
            Occupant = null;
            AnnounceOccupancy();
        }

        /// <summary>Hands the plot back entirely. Used by the garden lease when a player is gone.</summary>
        public void ClearOwner()
        {
            IsOccupied = false;
            Occupant = null;
            OwnerId = null;
            AnnounceOccupancy();
        }

        private void AnnounceOccupancy()
        {
            if (!SceneNetworking.IsMasterClient || _networkBridge == null) return;

            string owner = OwnerId ?? string.Empty;
            int size = BytesWriter.ByteSize + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(owner);
            var writer = new BytesWriter(size);
            writer.AddByte(IsOccupied ? (byte)1 : (byte)0);
            writer.AddString(owner);
            _networkBridge.RPC_SendMessageToAll((byte)PlantSlotMessageType.OccupationChanged, writer.Data);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((PlantSlotMessageType)id)
            {
                case PlantSlotMessageType.OccupationChanged:
                    var reader = new BytesReader(data);
                    if (!reader.IsValid) return;
                    IsOccupied = reader.NextByte() == 1;
                    string owner = reader.NextString();
                    OwnerId = string.IsNullOrEmpty(owner) ? null : owner;
                    break;
                case PlantSlotMessageType.PlantRequest:
                    OnPlantRequested(data);
                    break;
                case PlantSlotMessageType.Planted:
                    var plantedReader = new BytesReader(data);
                    if (!plantedReader.IsValid) return;
                    FindPlantable((uint)plantedReader.NextInt())?.ApplyPlanted();
                    break;
                default:
                    break;
            }
        }

        private NetworkObject FindObject(uint rawId)
        {
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (runner == null || rawId == 0) return null;
            return runner.TryFindObject(new NetworkId { Raw = rawId }, out NetworkObject obj) ? obj : null;
        }

        private IPlantable FindPlantable(uint rawId)
        {
            NetworkObject obj = FindObject(rawId);
            return obj == null ? null : obj.GetComponent<IPlantable>();
        }

        private void OnValidate()
        {
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkBridge>();
        }

        enum PlantSlotMessageType : byte
        {
            OccupationChanged = 0,
            PlantRequest = 1,
            Planted = 2
        }
    }
}
