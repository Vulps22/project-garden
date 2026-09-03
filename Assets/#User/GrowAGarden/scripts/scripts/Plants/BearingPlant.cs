using Fusion;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// A plant that stays put and yields something else: a pumpkin vine, a tomato, an apple tree.
    /// The produce is a separate object that hangs off a socket and is carried away; the plant
    /// itself is never touched.
    ///
    /// This is what the old ViningPlantSeed was trying to be. It had to pin the vine's world
    /// transform every frame to cancel out the root's motion, because the fruit a player carried
    /// and the vine that stayed behind were the same GameObject. Two objects removes the fight
    /// rather than winning it — the plant is positioned once and never written again.
    /// </summary>
    public abstract class BearingPlant : Plant
    {
        [Tooltip("What hangs off this plant. Must also be listed on SceneNetworking.")]
        [SerializeField] private NetworkObject _producePrefab;

        [Tooltip("Where produce can hang. One socket for a pumpkin, several for a tomato.")]
        [SerializeField] protected ProduceSlot[] _produceSlots = new ProduceSlot[0];

        /// <summary>
        /// Fills every empty socket. Master only.
        ///
        /// The plant keeps the produce it spawned so it can react when that produce is taken —
        /// but only on the master, which is the only client that decides anything about it.
        /// Proxies learn the outcome from the plant's state broadcast.
        /// </summary>
        public void Bear()
        {
            foreach (ProduceSlot slot in _produceSlots)
            {
                if (!slot.IsEmpty || slot.socket == null) continue;

                Produce produce = SpawnProduce(_producePrefab, slot.socket.position, slot.socket.rotation);
                if (produce == null) continue;

                produce.Init(seedDefinition, null, System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                slot.produceId = produce.NetworkId;
                slot.harvested = false;

                ProduceSlot captured = slot;
                produce.Harvested += () => OnHarvested(captured);
            }

            broadcastState();
        }

        protected override void OnFullyGrown()
        {
            Logger.Info($"OnFullyGrown() '{gameObject.name}' — grown; bearing into {_produceSlots.Length} socket(s)");
            Bear();
        }

        /// <summary>
        /// Runs on the master when a produce this plant bore is taken. Empties the socket, then
        /// asks the subclass what that means — the one thing single- and multi-harvest plants
        /// genuinely disagree about.
        /// </summary>
        private void OnHarvested(ProduceSlot slot)
        {
            if (!SceneNetworking.IsMasterClient) return;

            slot.produceId = 0;
            slot.harvested = true;
            broadcastState();

            OnProduceHarvested(slot);
        }

        /// <summary>Wither, or bear again.</summary>
        protected abstract void OnProduceHarvested(ProduceSlot slot);

        /// <summary>
        /// Unharvested produce belongs to the garden, so it goes when the garden does. Anything a
        /// player already took is theirs and is left alone — it is not in a socket any more.
        /// </summary>
        protected override void OnUprooted()
        {
            foreach (ProduceSlot slot in _produceSlots)
            {
                if (slot.IsEmpty) continue;
                Produce produce = FindProduce(slot.produceId);
                slot.produceId = 0;
                if (produce != null) produce.Uproot();
            }
        }

        private Produce FindProduce(uint rawId)
        {
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (runner == null || rawId == 0) return null;
            return runner.TryFindObject(new NetworkId { Raw = rawId }, out NetworkObject obj) && obj != null
                ? obj.GetComponent<Produce>()
                : null;
        }

        /// <summary>True once every socket has yielded at least once.</summary>
        protected bool AllSlotsHarvested()
        {
            foreach (ProduceSlot slot in _produceSlots)
                if (!slot.harvested) return false;
            return true;
        }

        // Sockets ride along in the state payload so a proxy knows which are full, and a late
        // joiner does not have to be told twice.
        protected override int GetExtraBroadcastStateSize() =>
            BytesWriter.ByteSize + _produceSlots.Length * (BytesWriter.IntSize + BytesWriter.ByteSize);

        protected override void OnWriteBroadcastState(BytesWriter writer)
        {
            writer.AddByte((byte)_produceSlots.Length);
            foreach (ProduceSlot slot in _produceSlots)
            {
                writer.AddInt((int)slot.produceId);
                writer.AddByte(slot.harvested ? (byte)1 : (byte)0);
            }
        }

        protected override void OnReadBroadcastState(BytesReader reader)
        {
            int count = reader.NextByte();
            for (int i = 0; i < count; i++)
            {
                uint id = (uint)reader.NextInt();
                bool harvested = reader.NextByte() == 1;
                if (i >= _produceSlots.Length) continue;
                _produceSlots[i].produceId = id;
                _produceSlots[i].harvested = harvested;
            }
        }
    }
}
