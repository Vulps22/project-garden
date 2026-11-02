using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Vulps.GrowAGarden.Economy;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden
{
    /// <summary>
    /// The master's half of every stall, keyed by <see cref="StallId"/> so the stall itself never
    /// has to be loaded on the master. A <see cref="BuyPoint"/> on any client asks for stock; the
    /// master rolls, spawns, sells and recycles here.
    ///
    /// Which seed stocks which stall, and which stalls have sold this cycle, are known on every
    /// client: the first from the seeds themselves, the second broadcast. A new master inherits
    /// both without a handover.
    /// </summary>
    public class ShopKeeper : MonoBehaviour
    {
        private const byte REQUEST_ID = 3;
        private const byte TAKEN_ID = 4;
        private const byte TAKEN_SET_ID = 5;
        private const byte RECYCLED_ID = 6;

        /// <summary>A stall that asked is not spawned for again within this many seconds, so several clients asking at once spawn once.</summary>
        private const float SPAWN_GAP_SECONDS = 1f;

        /// <summary>Same reasoning and values as before the split: the buyer's grab and authority arrive in no fixed order against the request.</summary>
        private const float TAKE_RETRY_SECONDS = 0.15f;
        private const float TAKE_RETRY_TIMEOUT_SECONDS = 1.5f;

        [Tooltip("SceneManager's messenger. Message ids 3-6 on it; EconomyManager uses 0, WorldSeed 1, SectorManager 2.")]
        [SerializeField] private NetworkMessenger _bridge;

        private static ShopKeeper _instance;
        private static readonly Dictionary<StallId, Seed> _stock = new Dictionary<StallId, Seed>();
        private static readonly HashSet<StallId> _taken = new HashSet<StallId>();

        private readonly Dictionary<StallId, float> _lastSpawn = new Dictionary<StallId, float>();
        private readonly HashSet<Seed> _resolving = new HashSet<Seed>();
        private readonly HashSet<StallId> _pinned = new HashSet<StallId>();

        /// <summary>The cycle turned: every stall may sell again. Every client.</summary>
        public static event Action Recycled;

        /// <summary>The seed stocking a stall, or null.</summary>
        public static Seed StockAt(StallId stall) => _stock.TryGetValue(stall, out Seed seed) && seed != null ? seed : null;

        /// <summary>Whether a stall has sold this cycle and will not restock until the next. Pinned stalls are never taken.</summary>
        public static bool IsTaken(StallId stall) => _taken.Contains(stall);

        /// <summary>Asks the master to stock a stall. Any client; the master ignores a stall that is stocked, sold out, or was just stocked.</summary>
        public static void RequestStock(StallId stall, Vector3 position, Quaternion rotation, float rarityMultiplier, string overrideSeedId)
        {
            if (_instance == null || _instance._bridge == null || !_instance._bridge.CanSend) return;

            string pinned = overrideSeedId ?? string.Empty;
            int size = StallId.Size + BytesWriter.Vector3Size + BytesWriter.QuaternionSize + BytesWriter.FloatSize
                     + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(pinned);
            var writer = new BytesWriter(size);
            stall.Write(writer);
            writer.AddVector3(position);
            writer.AddQuaternion(rotation);
            writer.AddFloat(rarityMultiplier);
            writer.AddAutoString(pinned);
            _instance._bridge.SendToAll(REQUEST_ID, writer.Data);
        }

        /// <summary>Called by a seed whenever its state changes: files it as its stall's stock while it is unsold, and drops it otherwise.</summary>
        public static void Track(Seed seed)
        {
            if (seed == null) return;
            if (!seed.HasStall || !seed.InShop || seed.IsDiscarded) { Untrack(seed); return; }

            if (_stock.TryGetValue(seed.Stall, out Seed current) && current != null && !current.IsDiscarded)
            {
                if (current == seed) return;
                Logger.Warn($"Track() 'ShopKeeper' — '{seed.name}' claims {seed.Stall}, already stocked by '{current.name}'; discarding the newcomer");
                seed.RequestDiscard();
                return;
            }
            if (current != null) Untrack(current);

            _stock[seed.Stall] = seed;
            seed.PurchaseRequested += OnPurchaseRequested;
        }

        /// <summary>Drops a seed from the stock table, if it is in it.</summary>
        public static void Untrack(Seed seed)
        {
            if (seed == null || !seed.HasStall) return;
            if (!_stock.TryGetValue(seed.Stall, out Seed current) || current != seed) return;

            _stock.Remove(seed.Stall);
            seed.PurchaseRequested -= OnPurchaseRequested;
        }

        private void Awake()
        {
            _instance = this;
            _stock.Clear();
            _taken.Clear();
        }

        private void Start()
        {
            _bridge.MessageToAll += OnMessageToAll;
            PlayerManager.OtherPlayerJoined += OnOtherPlayerJoined;
            WorldManager.StockRecycled += OnStockRecycled;
        }

        private void OnDestroy()
        {
            if (_bridge != null) _bridge.MessageToAll -= OnMessageToAll;
            PlayerManager.OtherPlayerJoined -= OnOtherPlayerJoined;
            WorldManager.StockRecycled -= OnStockRecycled;
            if (_instance == this) _instance = null;
            _stock.Clear();
            _taken.Clear();
            Recycled = null;
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch (id)
            {
                case REQUEST_ID: OnStockRequested(data); break;
                case TAKEN_ID: _taken.Add(StallId.Read(new BytesReader(data))); break;
                case TAKEN_SET_ID: ApplyTakenSet(data); break;
                case RECYCLED_ID:
                    _taken.Clear();
                    Recycled?.Invoke();
                    break;
            }
        }

        // ── Stocking ──────────────────────────────────────────────────────────────

        private void OnStockRequested(byte[] data)
        {
            if (!PlayerManager.IsMaster) return;

            var reader = new BytesReader(data);
            StallId stall = StallId.Read(reader);
            Vector3 position = reader.NextVector3();
            Quaternion rotation = reader.NextQuaternion();
            float rarity = reader.NextFloat();
            string pinned = reader.NextAutoString();

            bool isPinned = pinned.Length > 0;
            if (isPinned) _pinned.Add(stall);
            if ((!isPinned && _taken.Contains(stall)) || StockAt(stall) != null) return;
            if (_lastSpawn.TryGetValue(stall, out float last) && Time.time - last < SPAWN_GAP_SECONDS) return;
            if (!WorldManager.CanSpawn) return;
            _lastSpawn[stall] = Time.time;

            SeedDefinition definition = Pinned(pinned) ?? Roll(rarity);
            if (definition == null)
            {
                Logger.Warn($"OnStockRequested() '{gameObject.name}' — nothing to stock {stall} with");
                return;
            }

            NetworkObject spawned = WorldManager.Spawn(definition.seedPrefab, position, rotation, $"OnStockRequested() '{gameObject.name}' {stall}");
            Seed seed = spawned == null ? null : spawned.GetComponent<Seed>();
            if (seed == null)
            {
                if (spawned != null) Logger.Error($"OnStockRequested() '{gameObject.name}' — spawned '{spawned.name}' has no Seed component");
                return;
            }

            Logger.Info($"OnStockRequested() '{gameObject.name}' — stocked {stall} with '{definition.seedId}' at {position}");
            seed.PlaceInShop(position, rotation, stall);
            StartCoroutine(AnnounceStock(seed));
        }

        /// <summary>A pinned stall's crop, looked up by id in the world's catalogue; null when the stall is not pinned.</summary>
        private static SeedDefinition Pinned(string seedId)
        {
            if (string.IsNullOrEmpty(seedId)) return null;
            WorldManager world = WorldManager.Instance;
            if (world == null || world.Buyables == null) return null;
            foreach (SeedDefinition definition in world.Buyables)
                if (definition != null && definition.seedPrefab != null && definition.seedId == seedId) return definition;
            Logger.Warn($"Pinned() 'ShopKeeper' — pinned crop '{seedId}' is not in WorldManager's buyables; rolling instead");
            return null;
        }

        /// <summary>Weighted pick from the world's catalogue. Random is fine: the master decides and the spawned seed is the announcement.</summary>
        private static SeedDefinition Roll(float rarityMultiplier)
        {
            WorldManager world = WorldManager.Instance;
            if (world == null || world.Buyables == null || world.Buyables.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < world.Buyables.Count; i++) total += WeightOf(world.Buyables[i], rarityMultiplier);
            if (total <= 0f) return null;

            float pick = UnityEngine.Random.Range(0f, total);
            for (int i = 0; i < world.Buyables.Count; i++)
            {
                pick -= WeightOf(world.Buyables[i], rarityMultiplier);
                if (pick <= 0f) return world.Buyables[i];
            }

            for (int i = world.Buyables.Count - 1; i >= 0; i--)
                if (WeightOf(world.Buyables[i], rarityMultiplier) > 0f) return world.Buyables[i];
            return null;
        }

        /// <summary>A crop's chance at a stall: the multiplier lifts anything rarer than the staple's weight of 1.</summary>
        private static float WeightOf(SeedDefinition definition, float rarityMultiplier)
        {
            if (definition == null || definition.seedPrefab == null) return 0f;
            float weight = definition.spawnWeight;
            if (weight <= 0f) return 0f;
            if (weight >= 1f) return weight;

            // The rarer the crop, the more a rare stall favours it: weight × multiplier^(1 − weight). A flat
            // multiplier gave the barely-rare turnip the same boost as the rarest crop, so above x20 nearly
            // every island stall sold turnips.
            return weight * Mathf.Pow(Mathf.Max(1f, rarityMultiplier), 1f - weight);
        }

        /// <summary>Re-sends fresh stock's state: the spawn reaches other clients a few ticks after the first broadcast.</summary>
        private IEnumerator AnnounceStock(Seed seed)
        {
            float[] gaps = { 0.25f, 0.75f, 1f };
            foreach (float gap in gaps)
            {
                yield return new WaitForSeconds(gap);
                if (seed == null || !seed.InShop) yield break;
                seed.broadcastState();
            }
        }

        // ── The cycle ─────────────────────────────────────────────────────────────

        /// <summary>Clears every unsold seed and every sold-out mark; the stalls ask again for themselves.</summary>
        private void OnStockRecycled()
        {
            if (!PlayerManager.IsMaster) return;

            Logger.Info($"OnStockRecycled() '{gameObject.name}' — discarding {_stock.Count} unsold seed(s), reopening {_taken.Count} sold-out stall(s)");
            foreach (Seed seed in new List<Seed>(_stock.Values))
                if (seed != null) seed.RequestDiscard();

            _taken.Clear();
            if (_bridge.CanSend) _bridge.SendToAll(RECYCLED_ID, new byte[0]);
        }

        private void MarkTaken(StallId stall)
        {
            _taken.Add(stall);
            var writer = new BytesWriter(StallId.Size);
            stall.Write(writer);
            _bridge.SendToAll(TAKEN_ID, writer.Data);
        }

        /// <summary>A late joiner needs the sold-out list; the master sends the whole of it, which is small and cleared every cycle.</summary>
        private void OnOtherPlayerJoined()
        {
            if (!PlayerManager.IsMaster || !_bridge.CanSend) return;

            var writer = new BytesWriter(BytesWriter.IntSize + _taken.Count * StallId.Size);
            writer.AddInt(_taken.Count);
            foreach (StallId stall in _taken) stall.Write(writer);
            _bridge.SendToAll(TAKEN_SET_ID, writer.Data);
        }

        private static void ApplyTakenSet(byte[] data)
        {
            var reader = new BytesReader(data);
            int count = reader.NextInt();
            for (int i = 0; i < count; i++) _taken.Add(StallId.Read(reader));
        }

        // ── Deciding a sale ───────────────────────────────────────────────────────

        /// <summary>The holder asked to buy. Master only: it owns the economy.</summary>
        private static void OnPurchaseRequested(Seed seed)
        {
            if (_instance == null || !PlayerManager.IsMaster) return;
            if (seed == null || seed.IsBought || !seed.InShop) return;
            if (!seed.HasStall || StockAt(seed.Stall) != seed)
            {
                // Said out loud: a dropped request leaves the seed in the buyer's hand, unbought and unplantable.
                string stocked = !seed.HasStall ? "no stall" : StockAt(seed.Stall) == null ? "nothing" : $"'{StockAt(seed.Stall).name}'";
                _instance.Refuse(seed, $"is not its stall's stock here (the stall holds {stocked})");
                return;
            }
            if (!_instance._resolving.Add(seed)) return;

            _instance.StartCoroutine(_instance.ResolvePurchaseRequest(seed));
        }

        /// <summary>
        /// Waits briefly for the buyer's grab and authority to arrive, then charges or refuses.
        /// Any refusal puts the seed back without charging: an uncharged sale cannot be undone,
        /// whereas a player can pick a seed up again.
        /// </summary>
        private IEnumerator ResolvePurchaseRequest(Seed seed)
        {
            float waited = 0f;
            PlayerIdentity buyer = PlayerIdentity.None;
            while (waited < TAKE_RETRY_TIMEOUT_SECONDS)
            {
                if (seed == null || seed.IsBought || !seed.InShop) { _resolving.Remove(seed); yield break; }

                buyer = seed.GetGrabber();
                if (buyer.Exists && seed.HasKnownAuthority) break;

                yield return new WaitForSeconds(TAKE_RETRY_SECONDS);
                waited += TAKE_RETRY_SECONDS;
            }
            _resolving.Remove(seed);
            if (seed == null) yield break;

            if (!buyer.Exists) { Refuse(seed, "was requested by a buyer this client does not know yet"); yield break; }
            if (!seed.HasKnownAuthority) { Refuse(seed, "has no state authority"); yield break; }

            // Priced from the seed itself, so a cycle mid-sale never charges for a different crop.
            SeedDefinition sold = seed.seedDefinition;
            if (sold == null) { Refuse(seed, "has no definition, so it has no price"); yield break; }

            string buyerId = buyer.Id;
            PlayerBalance balance = EconomyManager.Instance == null ? null : EconomyManager.Instance.GetPlayer(buyerId);
            if (balance == null) { Refuse(seed, $"names a buyer with no balance on this client ('{buyerId}')"); yield break; }

            if (balance.GetBalance() < sold.buyPrice)
            {
                Logger.Info($"ResolvePurchaseRequest() '{gameObject.name}' — '{buyerId}' cannot afford {sold.seedId} ({balance.GetBalance()} < {sold.buyPrice})");
                Refuse(seed, "cannot be afforded");
                yield break;
            }

            StallId stall = seed.Stall;
            seed.Purchase(buyerId);
            EconomyManager.Instance.RemoveBalance(buyerId, sold.buyPrice);
            if (_pinned.Contains(stall))
            {
                Logger.Info($"ResolvePurchaseRequest() '{gameObject.name}' — sold {stall} '{sold.seedId}' to '{buyerId}'; pinned, restocks now");
                yield break;
            }
            MarkTaken(stall);
            Logger.Info($"ResolvePurchaseRequest() '{gameObject.name}' — sold {stall} '{sold.seedId}' to '{buyerId}'; sold out until the next cycle");
        }

        /// <summary>Puts a seed back into shop state on every client; each stall recalls its own stock.</summary>
        private void Refuse(Seed seed, string why)
        {
            Logger.Warn($"Refuse() '{gameObject.name}' — seed '{seed.name}' {why}; not a sale");
            seed.RestoreToShop();
        }

        private void OnValidate()
        {
            if (_bridge == null) _bridge = GetComponent<NetworkMessenger>();
        }
    }
}
