using Fusion;
using Fusion.Addons.Physics;
using SomniumSpace.Network.Bridge;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Owns all 24 of a Plot's PlantSlots through the Plot's one shared NetworkObject/NetworkBridge
    /// — the same one PlotLeaseManager already uses, not a second one. This is what used to be 24
    /// separate NetworkObjects, one per slot, each with its own bridge and its own broadcasting;
    /// see PlantSlot's docstring for why that was wrong, and this project's own logs for what it
    /// cost (~100 lines of ReassignNullObjectsAuthority churn per master transfer).
    ///
    /// Everything here scoped to slot occupancy today. plot-ownership.md's roadmap step envisions
    /// this class growing to own whatever else is a fact about the whole Plot rather than about
    /// one slot — fences, say — which is exactly why it is not called PlantSlotManager.
    /// </summary>
    public class PlotStateManager : MonoBehaviour
    {
        [SerializeField] private NetworkBridge _networkBridge;

        [Tooltip("The Plot's ownership authority. Planting checks this: an unclaimed Plot allows " +
                 "no planting at all, and a claimed one allows only its owner.")]
        [SerializeField] private PlotLeaseManager _lease;


        private PlantSlotState[] _slots = System.Array.Empty<PlantSlotState>();
        private Dictionary<PlantSlot, int> _slotIndices;

        public uint NetworkId => _networkBridge?.Object == null ? 0u : _networkBridge.Object.Id.Raw;

        private void Awake()
        {
            PlantSlot[] slots = GetComponentsInChildren<PlantSlot>(true);
            _slots = new PlantSlotState[slots.Length];
            _slotIndices = new Dictionary<PlantSlot, int>(slots.Length);

            for (int i = 0; i < slots.Length; i++)
            {
                _slots[i] = new PlantSlotState(slots[i].transform);
                _slotIndices[slots[i]] = i;
                slots[i].AttachTo(this);
            }
        }

        private void Start()
        {
            if (_networkBridge == null)
            {
                Logger.Error($"Start() '{gameObject.name}' — _networkBridge is NULL! Slot state will never leave this client!");
                return;
            }
            _networkBridge.OnMessageToAll += OnMessageToAll;
            SceneNetworking.OnOtherPlayerJoined += OnOtherPlayerJoined;
        }

        private void OnDestroy()
        {
            if (_networkBridge != null) _networkBridge.OnMessageToAll -= OnMessageToAll;
            SceneNetworking.OnOtherPlayerJoined -= OnOtherPlayerJoined;
        }

        /// <summary>
        /// A late joiner never saw any of this Plot's past occupancy broadcasts — Fusion does not
        /// replay RPCs — so without this its shadow copy of every slot reads "empty" regardless of
        /// what is actually growing. Harmless while the master stays the master, but a real
        /// correctness bug the moment host migration ever hands that client the role: it would
        /// make planting decisions off a copy it never actually received.
        /// </summary>
        private void OnOtherPlayerJoined(PlayerRef player)
        {
            if (PlayerManager.IsMaster) BroadcastFullState();
        }

        public Transform AnchorFor(int slotIndex) =>
            (slotIndex >= 0 && slotIndex < _slots.Length) ? _slots[slotIndex].Anchor : null;

        // ── Planter's side ────────────────────────────────────────────────────────

        /// <summary>Called by a PlantSlot's own RequestPlant — see that class. Resolves which of
        /// this Plot's slots it is before sending, so proxies never need to know slot identity at
        /// all beyond the index.</summary>
        public void RequestPlant(PlantSlot slot, IPlantable plantable)
        {
            if (plantable == null || plantable.NetworkId == 0) return;
            if (_slotIndices == null || !_slotIndices.TryGetValue(slot, out int slotIndex)) return;

            string owner = plantable.OwnerId ?? string.Empty;
            int size = BytesWriter.ByteSize + BytesWriter.IntSize + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(owner);
            var writer = new BytesWriter(size);
            writer.AddByte((byte)slotIndex);
            writer.AddInt((int)plantable.NetworkId);
            writer.AddString(owner);
            _networkBridge.RPC_SendMessageToAll((byte)PlotMessageType.PlantRequest, writer.Data);
        }

        // ── Plot's side ───────────────────────────────────────────────────────────

        /// <summary>Whether this player may plant in this Plot at all. Strict: an unclaimed Plot
        /// allows no planting, matching plot-ownership.md's stated rule exactly. The owner or any
        /// accepted teammate may use it — plot-ownership.md's requirement list, "only the claimant
        /// and their teammates may plant or harvest there," which this had not yet caught up to
        /// when teammates were added.</summary>
        private bool CanBeUsedBy(string playerId) =>
            _lease != null && _lease.IsClaimed
            && (_lease.OwnerId == playerId || _lease.TeammateIds.Contains(playerId));

        /// <summary>Master client only — moved verbatim from PlantSlot.OnPlantRequested, now
        /// addressed by slot index instead of being the slot itself.</summary>
        private void OnPlantRequested(byte[] data)
        {
            if (!PlayerManager.IsMaster) return;

            var reader = new BytesReader(data);
            if (!reader.IsValid) return;
            int slotIndex = reader.NextByte();
            uint plantableId = (uint)reader.NextInt();
            string ownerId = reader.NextString();

            if (slotIndex < 0 || slotIndex >= _slots.Length)
            {
                Logger.Warn($"OnPlantRequested() '{gameObject.name}' — slot index {slotIndex} out of range");
                return;
            }

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

            if (_slots[slotIndex].IsOccupied)
            {
                Logger.Warn($"OnPlantRequested() '{gameObject.name}' — slot {slotIndex} is already occupied; refused");
                return;
            }

            if (!CanBeUsedBy(ownerId))
            {
                Logger.Warn($"OnPlantRequested() '{gameObject.name}' — claimed={_lease != null && _lease.IsClaimed} owner='{(_lease == null ? "<no lease>" : _lease.OwnerId)}' refuses '{ownerId}'");
                return;
            }

            Plant plant = SpawnPlant(plantable.PlantPrefab, _slots[slotIndex].Anchor);
            if (plant == null) return;

            _slots[slotIndex].IsOccupied = true;
            _slots[slotIndex].Occupant = plant;
            plant.Init(this, slotIndex, System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            AnnounceOccupancy(slotIndex);

            // Announced to everyone so the seed leaves every hand at once; the despawn follows
            // once this client has taken ownership of it.
            var writer = new BytesWriter(BytesWriter.ByteSize + BytesWriter.IntSize);
            writer.AddByte((byte)slotIndex);
            writer.AddInt((int)plantableId);
            _networkBridge.RPC_SendMessageToAll((byte)PlotMessageType.Planted, writer.Data);

            Logger.Info($"OnPlantRequested() '{gameObject.name}' — planted '{plant.name}' at slot {slotIndex} for '{ownerId}'");
            StartCoroutine(TakeOwnershipAndDespawn(plantableId));
        }

        /// <summary>Moved verbatim from PlantSlot — see there for why the plot has to ask for the
        /// seed rather than assume it already owns it.</summary>
        private IEnumerator TakeOwnershipAndDespawn(uint plantableId)
        {
            NetworkObject obj = FindObject(plantableId);
            if (obj == null) yield break;

            bool granted = false;
            yield return WorldBridge.TakeAuthority(obj, r => granted = r);

            if (!granted)
            {
                Logger.Warn($"TakeOwnershipAndDespawn() '{gameObject.name}' — could not take ownership of the seed within {WorldBridge.AuthorityTimeout}s; it will linger invisible");
                yield break;
            }

            WorldBridge.Despawn(obj, $"TakeOwnershipAndDespawn() '{gameObject.name}'");
        }

        private Plant SpawnPlant(NetworkObject prefab, Transform anchor)
        {
            NetworkObject spawned = WorldBridge.Spawn(prefab, anchor.position, anchor.rotation, $"SpawnPlant() '{gameObject.name}'");
            if (spawned == null) return null;

            Plant plant = spawned.GetComponent<Plant>();
            if (plant == null) Logger.Error($"SpawnPlant() '{gameObject.name}' — '{spawned.name}' has no Plant component");
            return plant;
        }

        // ── Occupancy ─────────────────────────────────────────────────────────────

        /// <summary>Hands a slot from one occupant to the next without ever freeing it — see
        /// RootedPlant.OnFullyGrown. A rooted plant despawns the moment it is grown, and if the
        /// slot went free in that gap a player could sow into ground that visibly still holds a
        /// carrot.</summary>
        public void SetOccupant(int slotIndex, IPlotOccupant occupant)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return;
            _slots[slotIndex].Occupant = occupant;
        }

        /// <summary>Whatever was standing here has gone. Called by Plant.End()/Produce's harvest
        /// path — always from an already master-gated call site, matching the old PlantSlot.Release
        /// contract exactly (it never gated itself either).</summary>
        public void Release(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return;
            _slots[slotIndex].IsOccupied = false;
            _slots[slotIndex].Occupant = null;
            AnnounceOccupancy(slotIndex);
        }

        /// <summary>Frees every occupied slot, uprooting what stands in each. Called by
        /// GardenLease when this Plot's owner is gone for good.</summary>
        public void UprootAll()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsOccupied) continue;
                _slots[i].Occupant?.Uproot();
                _slots[i].IsOccupied = false;
                _slots[i].Occupant = null;
                AnnounceOccupancy(i);
            }
        }

        /// <summary>Incremental — one slot changed. Not a full resend of all 24; that is
        /// BroadcastFullState's job, for a late joiner only.</summary>
        private void AnnounceOccupancy(int slotIndex)
        {
            if (!PlayerManager.IsMaster || _networkBridge == null) return;

            var writer = new BytesWriter(BytesWriter.ByteSize * 2);
            writer.AddByte((byte)slotIndex);
            writer.AddByte(_slots[slotIndex].IsOccupied ? (byte)1 : (byte)0);
            _networkBridge.RPC_SendMessageToAll((byte)PlotMessageType.SlotOccupancyChanged, writer.Data);
        }

        /// <summary>All 24 slots as one 3-byte bitmask, per plot-ownership.md's own sizing note —
        /// no count prefix needed since the slot count is fixed, unlike EconomyManager's variable
        /// player table.</summary>
        private void BroadcastFullState()
        {
            if (!PlayerManager.IsMaster || _networkBridge == null) return;

            byte b0 = 0, b1 = 0, b2 = 0;
            for (int i = 0; i < _slots.Length && i < 24; i++)
            {
                if (!_slots[i].IsOccupied) continue;
                int bit = i % 8;
                switch (i / 8)
                {
                    case 0: b0 |= (byte)(1 << bit); break;
                    case 1: b1 |= (byte)(1 << bit); break;
                    default: b2 |= (byte)(1 << bit); break;
                }
            }

            var writer = new BytesWriter(3);
            writer.AddByte(b0);
            writer.AddByte(b1);
            writer.AddByte(b2);
            _networkBridge.RPC_SendMessageToAll((byte)PlotMessageType.FullStateSync, writer.Data);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((PlotMessageType)id)
            {
                case PlotMessageType.SlotOccupancyChanged:
                    var occReader = new BytesReader(data);
                    int occIndex = occReader.NextByte();
                    bool occupied = occReader.NextByte() == 1;
                    if (occIndex >= 0 && occIndex < _slots.Length) _slots[occIndex].IsOccupied = occupied;
                    break;

                case PlotMessageType.PlantRequest:
                    OnPlantRequested(data);
                    break;

                case PlotMessageType.Planted:
                    var plantedReader = new BytesReader(data);
                    plantedReader.NextByte();   // slot index — not needed here, the object finds itself
                    uint plantableId = (uint)plantedReader.NextInt();
                    FindPlantable(plantableId)?.ApplyPlanted();
                    break;

                case PlotMessageType.FullStateSync:
                    var fsReader = new BytesReader(data);
                    byte fb0 = (byte)fsReader.NextByte();
                    byte fb1 = (byte)fsReader.NextByte();
                    byte fb2 = (byte)fsReader.NextByte();
                    for (int i = 0; i < _slots.Length && i < 24; i++)
                    {
                        int bit = i % 8;
                        byte b = i / 8 == 0 ? fb0 : i / 8 == 1 ? fb1 : fb2;
                        _slots[i].IsOccupied = (b & (1 << bit)) != 0;
                    }
                    break;

                default:
                    break;   // OwnerChanged belongs to PlotLeaseManager, on the same shared bridge
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
            if (_lease == null) _lease = GetComponent<PlotLeaseManager>();
        }
    }
}
