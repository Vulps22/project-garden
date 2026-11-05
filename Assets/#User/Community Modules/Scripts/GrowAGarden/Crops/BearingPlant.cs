using Fusion;
using UnityEngine;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Crops
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

        [Tooltip("Where produce can hang. One socket for a pumpkin, several for a tomato. " +
                 "Leave empty to use every child named ProduceSocket.")]
        [SerializeField] private Transform[] _produceSockets = new Transform[0];

        /// <summary>Built at Awake from _produceSockets — see ProduceSlot for why.</summary>
        protected ProduceSlot[] _produceSlots = new ProduceSlot[0];

        private int _harvestCount;

        /// <summary>
        /// How many times each socket bears before it is spent; the plant withers once all are. This
        /// plant's own number, starting from its crop's default, so a plant can later be rolled
        /// longer- or shorter-lived than its kind. Read late: the definition sets its numbers in
        /// OnEnable, which can run after this plant's Awake.
        /// </summary>
        public int HarvestCount
        {
            get
            {
                if (_harvestCount <= 0) _harvestCount = Mathf.Max(1, seedDefinition != null ? seedDefinition.harvestCount : 1);
                return _harvestCount;
            }
        }

        // Sockets whose produce this client listens to for its harvest: only the master hears Harvested.
        private readonly System.Collections.Generic.HashSet<ProduceSlot> _watched = new System.Collections.Generic.HashSet<ProduceSlot>();

        protected override void Awake()
        {
            base.Awake();
            BuildSlots();
            PlayerManager.BecameWorldMaster += OnBecameWorldMaster;
        }

        protected override void OnDestroy()
        {
            PlayerManager.BecameWorldMaster -= OnBecameWorldMaster;
            base.OnDestroy();
        }

        /// <summary>A new master listens to what the old one bore; fruit taken in between counts as harvested now.</summary>
        private void OnBecameWorldMaster()
        {
            foreach (ProduceSlot slot in _produceSlots)
            {
                if (slot.IsEmpty || _watched.Contains(slot)) continue;
                Produce produce = FindProduce(slot.produceId);
                if (produce == null || produce.IsHarvested)
                {
                    Logger.Info($"OnBecameWorldMaster() '{gameObject.name}' — socket's produce {slot.produceId} was taken during the handover");
                    OnHarvested(slot);
                    continue;
                }
                Watch(slot, produce);
            }
        }

        private void Watch(ProduceSlot slot, Produce produce)
        {
            if (!_watched.Add(slot)) return;
            produce.Harvested += () => OnHarvested(slot);
        }

        /// <summary>
        /// Turns the authored sockets into slots, and finds them by name if none were authored.
        ///
        /// The fallback is not tidiness — it is insurance. A bearing plant with no sockets grows a
        /// full vine and then silently yields nothing, which reads in-world as "growth is broken"
        /// and costs an upload to work out. Finding a child named ProduceSocket is exactly what a
        /// human would do when the array turns up empty, so the plant does it and says so.
        /// </summary>
        private void BuildSlots()
        {
            Transform[] sockets = _produceSockets;

            if (sockets == null || sockets.Length == 0)
            {
                var found = new System.Collections.Generic.List<Transform>();
                foreach (Transform child in transform)
                    if (child.name.StartsWith("ProduceSocket")) found.Add(child);
                sockets = found.ToArray();

                if (sockets.Length > 0)
                    Logger.Warn($"BuildSlots() '{gameObject.name}' — no sockets were authored; found {sockets.Length} by name");
            }

            if (sockets.Length == 0)
            {
                Logger.Error($"BuildSlots() '{gameObject.name}' — no produce sockets at all; this plant will grow and never bear");
                return;
            }

            _produceSlots = new ProduceSlot[sockets.Length];
            for (int i = 0; i < sockets.Length; i++) _produceSlots[i] = new ProduceSlot(sockets[i]);
        }

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
                if (!slot.IsEmpty || slot.socket == null || slot.harvestedCount >= HarvestCount) continue;

                Produce produce = SpawnProduce(_producePrefab, slot.socket.position, slot.socket.rotation);
                if (produce == null) continue;

                produce.Init(null, -1, RipenStart(), _plot);
                slot.produceId = produce.NetworkId;
                Watch(slot, produce);
            }

            broadcastState();
        }

        protected override void OnGrownTouched()
        {
            foreach (ProduceSlot slot in _produceSlots)
            {
                if (slot.IsEmpty) continue;
                Produce produce = WorldManager.Find<Produce>(slot.produceId);
                if (produce != null) produce.RipenNow();
            }
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
            if (!PlayerManager.IsMaster) return;

            _watched.Remove(slot);
            slot.produceId = 0;
            slot.harvestedCount++;
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
            return WorldManager.Find<Produce>(rawId);
        }

        /// <summary>True once every socket has been harvested its plant's HarvestCount times.</summary>
        protected bool AllSlotsSpent()
        {
            foreach (ProduceSlot slot in _produceSlots)
                if (slot.harvestedCount < HarvestCount) return false;
            return true;
        }

        // Sockets ride along in the state payload so a proxy knows which are full, and a late
        // joiner does not have to be told twice.
        protected override int GetExtraBroadcastStateSize() =>
            BytesWriter.ByteSize * 2 + _produceSlots.Length * (BytesWriter.IntSize + BytesWriter.ByteSize);

        protected override void OnWriteBroadcastState(BytesWriter writer)
        {
            writer.AddByte((byte)Mathf.Min(HarvestCount, 255));
            writer.AddByte((byte)_produceSlots.Length);
            foreach (ProduceSlot slot in _produceSlots)
            {
                writer.AddInt((int)slot.produceId);
                writer.AddByte((byte)Mathf.Min(slot.harvestedCount, 255));
            }
        }

        protected override void OnReadBroadcastState(BytesReader reader)
        {
            _harvestCount = Mathf.Max(1, reader.NextByte());
            int count = reader.NextByte();
            for (int i = 0; i < count; i++)
            {
                uint id = (uint)reader.NextInt();
                int harvested = reader.NextByte();
                if (i >= _produceSlots.Length) continue;
                _produceSlots[i].produceId = id;
                _produceSlots[i].harvestedCount = harvested;
            }
        }
    }
}
