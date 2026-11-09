using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Vulps.GrowAGarden.Economy;
using Vulps.GrowAGarden.Interaction;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden
{
    /// <summary>
    /// The master's half of every stall, keyed by <see cref="StallId"/> so the stall itself never has to be loaded on the
    /// master. A <see cref="BuyPoint"/> on any client asks for stock; the master rolls, spawns, sells and recycles here.
    ///
    /// **The stock table is the master's fact**: which seed (by network id) stocks which stall, sent to every client as it
    /// changes and whole to late joiners and on a change of master. Peers mirror it and never decide anything from what
    /// they happen to have received. A seed and its "stocked" message can arrive in either order; whichever comes second
    /// completes the match. Event driven throughout. See docs/shop.md.
    /// </summary>
    public class ShopKeeper : MonoBehaviour
    {
        private const byte REQUEST_ID = 3;
        private const byte TAKEN_ID = 4;
        private const byte TAKEN_SET_ID = 5;
        private const byte RECYCLED_ID = 6;
        private const byte STOCKED_ID = 16;
        private const byte EMPTIED_ID = 17;
        private const byte TABLE_ID = 18;
        private const byte TABLE_REQUEST_ID = 19;

        /// <summary>How long the master waits for a buyer's grab and authority to arrive before refusing; they race the request.</summary>
        private const float RESOLVE_TIMEOUT_SECONDS = 1.5f;

        [Tooltip("SceneManager's messenger. Message ids 3-6 and 16-19 on it; EconomyManager uses 0, WorldSeed 1, SectorManager 2.")]
        [SerializeField] private NetworkMessenger _bridge;

        private static ShopKeeper _instance;
        private static readonly Dictionary<StallId, uint> _stock = new Dictionary<StallId, uint>();   // the fact: stall → seed's network id
        private static readonly Dictionary<uint, StallId> _stallOf = new Dictionary<uint, StallId>();
        private static readonly Dictionary<StallId, string> _stockItem = new Dictionary<StallId, string>();   // what kind each stall's stock is
        private static readonly HashSet<StallId> _taken = new HashSet<StallId>();
        private static readonly Dictionary<StallId, Action> _watchers = new Dictionary<StallId, Action>();
        private static bool _hasTable;   // this client has the master's table: until then an empty stall may only look stocked-elsewhere

        private readonly HashSet<StallId> _pinned = new HashSet<StallId>();
        private readonly Dictionary<StallId, byte[]> _pendingRequests = new Dictionary<StallId, byte[]>();   // asks waiting to be sent or served
        private readonly Dictionary<Seed, Action> _resolving = new Dictionary<Seed, Action>();

        /// <summary>The cycle turned: every stall may sell again. Every client.</summary>
        public static event Action Recycled;

        /// <summary>Some stall's stock or sold-out mark changed. Every client.</summary>
        public static event Action StockChanged;

        /// <summary>The seed stocking a stall, or null: none, or not spawned on this client yet.</summary>
        public static Seed StockAt(StallId stall)
        {
            if (!_stock.TryGetValue(stall, out uint id)) return null;
            Seed seed = SeedById(id);
            return seed != null && IsExpectedKind(stall, seed) ? seed : null;
        }

        /// <summary>The kind the table says a stall's stock is ("seed.tomato"), or empty if unknown.</summary>
        public static string ItemAt(StallId stall) => _stockItem.TryGetValue(stall, out string item) ? item : string.Empty;

        private static bool IsExpectedKind(StallId stall, Seed seed)
        {
            string expected = ItemAt(stall);
            return expected.Length == 0 || expected == seed.ItemId;
        }

        /// <summary>
        /// Whether the table has moved this seed's stall on to a different seed: it is left over (an old cycle's stock, or a stall
        /// restocked while it was away). An empty stall is not enough, since a new seed can arrive before the table names it.
        /// </summary>
        public static bool IsSuperseded(Seed seed)
        {
            if (seed == null || !seed.HasStall || !HasTable) return false;
            return _stock.TryGetValue(seed.Stall, out uint id) && id != seed.NetworkId;
        }

        /// <summary>Every seed on this client recorded against a stall, other than its current stock, checks whether it is left over.</summary>
        private static void CheckLeftovers(StallId stall)
        {
            foreach (Seed seed in new List<Seed>(_arrived.Values))
                if (seed != null && seed.HasStall && seed.Stall.Equals(stall)) seed.CheckSuperseded();
        }

        private static Seed SeedById(uint id) =>
            _arrived.TryGetValue(id, out Seed seed) && seed != null ? seed : WorldManager.Find<Seed>(id);

        // Stock seeds as they spawn here, by id: the runner may not find a seed from inside its own spawn, which left a stall
        // holding nothing and its sign naming the seed before.
        private static readonly Dictionary<uint, Seed> _arrived = new Dictionary<uint, Seed>();

        // Every client: purchases asked for and not yet bought, so a new master can decide what the old one left open.
        private static readonly HashSet<Seed> _asked = new HashSet<Seed>();

        /// <summary>Whether the master has stocked a stall, whether or not the seed has reached this client yet.</summary>
        public static bool HasStock(StallId stall) => _stock.ContainsKey(stall);

        /// <summary>The stall the table names a seed as stocking, if any.</summary>
        public static bool TryGetStall(uint seedId, out StallId stall) => _stallOf.TryGetValue(seedId, out stall);

        /// <summary>Whether this client knows the master's table yet (the master always does); stalls ask only once it does.</summary>
        public static bool HasTable => _hasTable || PlayerManager.IsMaster;

        /// <summary>Whether a stall has sold this cycle and will not restock until the next. Pinned stalls are never taken.</summary>
        public static bool IsTaken(StallId stall) => _taken.Contains(stall);

        /// <summary>Stalls this client knows of stocking a crop right now, unsold.</summary>
        public static int Stocked(string seedId)
        {
            int count = 0;
            foreach (KeyValuePair<StallId, uint> pair in _stock)
            {
                if (_taken.Contains(pair.Key)) continue;
                Seed seed = WorldManager.Find<Seed>(pair.Value);
                if (seed != null && !seed.IsDiscarded && seed.seedDefinition != null && seed.seedDefinition.seedId == seedId) count++;
            }
            return count;
        }

        /// <summary>A stall hears whenever its stock or sold-out mark changes. One listener per stall.</summary>
        public static void Watch(StallId stall, Action changed) => _watchers[stall] = changed;
        public static void Unwatch(StallId stall, Action changed)
        {
            if (_watchers.TryGetValue(stall, out Action current) && current == changed) _watchers.Remove(stall);
        }

        /// <summary>Asks the master to stock a stall. Any client; the master ignores a stall that is stocked or sold out.</summary>
        public static void RequestStock(StallId stall, Vector3 position, Quaternion rotation, float rarityMultiplier, string overrideSeedId)
        {
            if (_instance == null) return;
            string pinned = overrideSeedId ?? string.Empty;
            int size = StallId.Size + BytesWriter.Vector3Size + BytesWriter.QuaternionSize + BytesWriter.FloatSize
                     + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(pinned);
            var writer = new BytesWriter(size);
            stall.Write(writer);
            writer.AddVector3(position);
            writer.AddQuaternion(rotation);
            writer.AddFloat(rarityMultiplier);
            writer.AddAutoString(pinned);
            _instance.SendRequest(stall, writer.Data);
        }

        /// <summary>A seed spawned on this client: if the table already names it, it takes its stall now.</summary>
        public static void OnSeedSpawned(Seed seed)
        {
            if (seed == null) return;
            _arrived[seed.NetworkId] = seed;   // before the table may name it: ApplyTable's stock is found here too
            if (!_stallOf.TryGetValue(seed.NetworkId, out StallId stall)) return;
            seed.ApplyStall(stall);
            Raise(stall);
        }

        /// <summary>A seed is going. If it was a stall's unsold stock, the master says the stall is empty.</summary>
        public static void OnSeedGone(Seed seed)
        {
            _asked.Remove(seed);
            if (seed != null && _arrived.TryGetValue(seed.NetworkId, out Seed known) && known == seed) _arrived.Remove(seed.NetworkId);
            if (_instance == null || seed == null || !PlayerManager.IsMaster) return;
            if (!_stallOf.TryGetValue(seed.NetworkId, out StallId stall)) return;
            _instance.SendEmptied(stall, seed.NetworkId);
        }

        /// <summary>The holder asked to buy. Master only: it owns the economy.</summary>
        public static void OnPurchaseRequested(Seed seed)
        {
            if (seed == null || seed.IsBought || !seed.InShop) return;
            _asked.Add(seed);
            if (_instance == null || !PlayerManager.IsMaster) return;
            if (seed.HasStall && _stock.TryGetValue(seed.Stall, out uint stocked) && stocked == seed.NetworkId && !IsExpectedKind(seed.Stall, seed))
            {
                // The table's id now names a different kind of seed: discard this one and empty the stall, which asks for its real stock.
                Logger.Warn($"OnPurchaseRequested() 'ShopKeeper' — seed {seed.NetworkId} is '{seed.ItemId}', but {seed.Stall} should hold '{ItemAt(seed.Stall)}'; discarding it and restocking");
                seed.RequestDiscard();
                _instance.SendEmptied(seed.Stall, seed.NetworkId);
                return;
            }
            if (IsSuperseded(seed))
            {
                // Left over from an earlier stock: never sold, discarded; the stall already has its real stock.
                Logger.Warn($"OnPurchaseRequested() 'ShopKeeper' — seed {seed.NetworkId} '{seed.ItemId}' is left over at {seed.Stall}, which holds {_stock[seed.Stall]} '{ItemAt(seed.Stall)}'; discarding it");
                seed.RequestDiscard();
                return;
            }
            if (!seed.HasStall || !_stock.TryGetValue(seed.Stall, out uint id) || id != seed.NetworkId)
            {
                // Said out loud: a dropped request leaves the seed in the buyer's hand, unbought and unplantable.
                _instance.Refuse(seed, $"is not its stall's stock (the stall holds {(seed.HasStall && _stock.TryGetValue(seed.Stall, out uint other) ? other.ToString() : "nothing")})");
                return;
            }
            _instance.BeginResolve(seed);
        }

        private void Awake()
        {
            _instance = this;
            _hasTable = false;
            _stock.Clear();
            _stallOf.Clear();
            _stockItem.Clear();
            _arrived.Clear();
            _taken.Clear();
            _watchers.Clear();
            _asked.Clear();
        }

        private void Start()
        {
            _bridge.MessageToAll += OnMessageToAll;
            _bridge.OnSpawned += FlushRequests;
            _bridge.OnSpawned += AskForTable;
            PlayerManager.OtherPlayerJoined += OnOtherPlayerJoined;
            PlayerManager.BecameWorldMaster += SendTable;
            PlayerManager.BecameWorldMaster += DecideOpenPurchases;
            Seed.AnyPurchased += OnAnyPurchased;
            WorldBridge.NetworkReady += FlushRequests;
            WorldBridge.NetworkReady += AskForTable;
            if (WorldBridge.IsNetworkReady) AskForTable();
            WorldManager.StockRecycled += OnStockRecycled;
        }

        private void OnDestroy()
        {
            if (_bridge != null) { _bridge.MessageToAll -= OnMessageToAll; _bridge.OnSpawned -= FlushRequests; _bridge.OnSpawned -= AskForTable; }
            PlayerManager.OtherPlayerJoined -= OnOtherPlayerJoined;
            PlayerManager.BecameWorldMaster -= SendTable;
            PlayerManager.BecameWorldMaster -= DecideOpenPurchases;
            Seed.AnyPurchased -= OnAnyPurchased;
            WorldBridge.NetworkReady -= FlushRequests;
            WorldBridge.NetworkReady -= AskForTable;
            WorldManager.StockRecycled -= OnStockRecycled;
            if (_instance == this) _instance = null;
            _stock.Clear();
            _stallOf.Clear();
            _stockItem.Clear();
            _arrived.Clear();
            _taken.Clear();
            _watchers.Clear();
            _asked.Clear();
            Recycled = null;
            StockChanged = null;
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch (id)
            {
                case REQUEST_ID: OnStockRequested(data); break;
                case TAKEN_ID:
                    StallId taken = StallId.Read(new BytesReader(data));
                    _taken.Add(taken);
                    Raise(taken);
                    break;
                case TAKEN_SET_ID: ApplyTakenSet(data); break;
                case RECYCLED_ID: ApplyRecycled(); break;
                case STOCKED_ID: ApplyStocked(data); break;
                case EMPTIED_ID: ApplyEmptied(data); break;
                case TABLE_ID: ApplyTable(data); break;
                case TABLE_REQUEST_ID: OnOtherPlayerJoined(); break;
            }
        }

        /// <summary>Tells one stall's listener, and anything showing totals, that the stall changed.</summary>
        private static void Raise(StallId stall)
        {
            if (_watchers.TryGetValue(stall, out Action changed)) changed?.Invoke();
            StockChanged?.Invoke();
        }

        private static void RaiseAll()
        {
            foreach (Action changed in new List<Action>(_watchers.Values)) changed?.Invoke();
            StockChanged?.Invoke();
        }

        // ── Requests ──────────────────────────────────────────────────────────────

        // A stall's ask goes when it can: the messenger may still be attaching on a late joiner, and the master may not be able
        // to spawn yet. Either way it is kept, one per stall, and sent or served on the event that makes it possible.
        private void SendRequest(StallId stall, byte[] data)
        {
            if (!_bridge.CanSend) { _pendingRequests[stall] = data; return; }
            _bridge.SendToAll(REQUEST_ID, data);
        }

        private void FlushRequests()
        {
            if (_pendingRequests.Count == 0) return;
            var pending = new List<KeyValuePair<StallId, byte[]>>(_pendingRequests);
            _pendingRequests.Clear();
            foreach (KeyValuePair<StallId, byte[]> request in pending)
            {
                if (_bridge.CanSend) _bridge.SendToAll(REQUEST_ID, request.Value);
                else _pendingRequests[request.Key] = request.Value;
            }
        }

        // ── Stocking (master) ─────────────────────────────────────────────────────

        private void OnStockRequested(byte[] data)
        {
            var reader = new BytesReader(data);
            StallId stall = StallId.Read(reader);
            Vector3 position = reader.NextVector3();
            Quaternion rotation = reader.NextQuaternion();
            float rarity = reader.NextFloat();
            string pinned = reader.NextAutoString();

            // Every client learns which stalls are pinned (its own stalls ask too), so a new master does not sell one out.
            bool isPinned = pinned.Length > 0;
            if (isPinned) _pinned.Add(stall);
            if (!PlayerManager.IsMaster) return;
            if ((!isPinned && _taken.Contains(stall)) || _stock.ContainsKey(stall)) return;
            if (!WorldManager.CanSpawn)
            {
                // Served once Fusion can spawn (WorldBridge.NetworkReady); the master's own ask loops back here then.
                if (_bridge.CanSend) _pendingRequests[stall] = data;
                return;
            }

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
            seed.PlaceInShop(position, rotation);
            // Into the master's own table at once, so a second ask before the message loops back is ignored.
            string item = seed.ItemId;
            if (item.Length == 0) Logger.Warn($"OnStockRequested() '{gameObject.name}' — '{spawned.name}' has no ItemIdentity; its kind cannot be checked");
            _stock[stall] = spawned.Id.Raw;
            _stallOf[spawned.Id.Raw] = stall;
            _stockItem[stall] = item;
            var writer = new BytesWriter(StallId.Size + BytesWriter.IntSize + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(item));
            stall.Write(writer);
            writer.AddInt((int)spawned.Id.Raw);
            writer.AddAutoString(item);
            _bridge.SendToAll(STOCKED_ID, writer.Data);
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

        /// <summary>Weighted pick from the world's catalogue. Random is fine: the master decides and announces the result.</summary>
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
            if (definition == null || definition.seedPrefab == null || !definition.MayBeStocked) return 0f;
            float weight = definition.spawnWeight;
            if (weight <= 0f) return 0f;
            if (weight >= 1f) return weight;

            // The rarer the crop, the more a rare stall favours it: weight × multiplier^(1 − weight). A flat
            // multiplier gave the barely-rare turnip the same boost as the rarest crop, so above x20 nearly
            // every island stall sold turnips.
            return weight * Mathf.Pow(Mathf.Max(1f, rarityMultiplier), 1f - weight);
        }

        /// <summary>A stall's seed is gone or sold. By seed as well as stall, so a late one can never empty the stall's next stock.</summary>
        private void SendEmptied(StallId stall, uint seedId)
        {
            if (!_bridge.CanSend) return;
            var writer = new BytesWriter(StallId.Size + BytesWriter.IntSize);
            stall.Write(writer);
            writer.AddInt((int)seedId);
            _bridge.SendToAll(EMPTIED_ID, writer.Data);
        }

        // ── The table, on every client ────────────────────────────────────────────

        private static void ApplyStocked(byte[] data)
        {
            var reader = new BytesReader(data);
            StallId stall = StallId.Read(reader);
            uint id = (uint)reader.NextInt();
            string item = reader.NextAutoString();
            if (_stock.TryGetValue(stall, out uint old) && old != id) _stallOf.Remove(old);
            _stock[stall] = id;
            _stallOf[id] = stall;
            _stockItem[stall] = item;
            SeedById(id)?.ApplyStall(stall);   // not spawned here yet: OnSeedSpawned completes it
            Logger.Info($"ApplyStocked() 'ShopKeeper' — {stall} stocked with seed {id} '{item}'");
            CheckLeftovers(stall);
            Raise(stall);
        }

        private static void ApplyEmptied(byte[] data)
        {
            var reader = new BytesReader(data);
            StallId stall = StallId.Read(reader);
            uint seedId = (uint)reader.NextInt();
            if (!_stock.TryGetValue(stall, out uint id) || id != seedId) return;
            _stock.Remove(stall);
            _stallOf.Remove(id);
            _stockItem.Remove(stall);
            Raise(stall);
        }

        /// <summary>The whole table, from the master: to a late joiner, and from a new master so nothing it missed stays wrong.</summary>
        private void SendTable()
        {
            if (!PlayerManager.IsMaster || !_bridge.CanSend) return;
            int size = BytesWriter.IntSize;
            foreach (KeyValuePair<StallId, uint> pair in _stock)
                size += StallId.Size + BytesWriter.IntSize + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(ItemAt(pair.Key));
            var writer = new BytesWriter(size);
            writer.AddInt(_stock.Count);
            foreach (KeyValuePair<StallId, uint> pair in _stock)
            {
                pair.Key.Write(writer);
                writer.AddInt((int)pair.Value);
                writer.AddAutoString(ItemAt(pair.Key));
            }
            _bridge.SendToAll(TABLE_ID, writer.Data);
        }

        private static void ApplyTable(byte[] data)
        {
            var reader = new BytesReader(data);
            int count = reader.NextInt();
            _stock.Clear();
            _stallOf.Clear();
            _stockItem.Clear();
            for (int i = 0; i < count; i++)
            {
                StallId stall = StallId.Read(reader);
                uint id = (uint)reader.NextInt();
                _stock[stall] = id;
                _stallOf[id] = stall;
                _stockItem[stall] = reader.NextAutoString();
                SeedById(id)?.ApplyStall(stall);
            }
            _hasTable = true;
            foreach (Seed seed in new List<Seed>(_arrived.Values)) seed?.CheckSuperseded();
            RaiseAll();   // every stall looks again: an empty one asks
        }

        // ── The cycle ─────────────────────────────────────────────────────────────

        /// <summary>Clears every unsold seed and every sold-out mark; the stalls ask again for themselves.</summary>
        private void OnStockRecycled()
        {
            if (!PlayerManager.IsMaster) return;

            Logger.Info($"OnStockRecycled() '{gameObject.name}' — discarding {_stock.Count} unsold seed(s), reopening {_taken.Count} sold-out stall(s)");
            foreach (uint id in new List<uint>(_stock.Values))
                WorldManager.Find<Seed>(id)?.RequestDiscard();
            if (_bridge.CanSend) _bridge.SendToAll(RECYCLED_ID, new byte[0]);
        }

        private static void ApplyRecycled()
        {
            _stock.Clear();
            _stallOf.Clear();
            _stockItem.Clear();
            _taken.Clear();
            _hasTable = true;
            Recycled?.Invoke();
            RaiseAll();
        }

        private void MarkTaken(StallId stall)
        {
            _taken.Add(stall);
            var writer = new BytesWriter(StallId.Size);
            stall.Write(writer);
            _bridge.SendToAll(TAKEN_ID, writer.Data);
        }

        /// <summary>
        /// A joiner asks for the table once its own messenger can talk: the master's push on the join can arrive before the
        /// joiner's scene objects are attached, and is then lost. Whichever arrives first fills the table.
        /// </summary>
        private void AskForTable()
        {
            if (_hasTable || PlayerManager.IsMaster || !_bridge.CanSend) return;
            _bridge.SendToAll(TABLE_REQUEST_ID, new byte[0]);
        }

        /// <summary>A late joiner needs the sold-out list first, then the table, so an empty sold-out stall does not ask.</summary>
        private void OnOtherPlayerJoined()
        {
            if (!PlayerManager.IsMaster || !_bridge.CanSend) return;

            var writer = new BytesWriter(BytesWriter.IntSize + _taken.Count * StallId.Size);
            writer.AddInt(_taken.Count);
            foreach (StallId stall in _taken) stall.Write(writer);
            _bridge.SendToAll(TAKEN_SET_ID, writer.Data);
            SendTable();
        }

        private static void ApplyTakenSet(byte[] data)
        {
            var reader = new BytesReader(data);
            int count = reader.NextInt();
            for (int i = 0; i < count; i++) _taken.Add(StallId.Read(reader));
        }

        // ── Deciding a sale (master) ──────────────────────────────────────────────

        private static void OnAnyPurchased(Seed seed) => _asked.Remove(seed);

        /// <summary>A new master decides the purchases the old one left open: asked for, still stock, still in a hand.</summary>
        private void DecideOpenPurchases()
        {
            var open = new List<Seed>(_asked);
            _asked.Clear();
            foreach (Seed seed in open)
            {
                if (seed == null || seed.IsBought || !seed.InShop || !seed.GetGrabber().Exists) continue;
                Logger.Info($"DecideOpenPurchases() '{gameObject.name}' — deciding '{seed.name}', left open by the last master");
                OnPurchaseRequested(seed);
            }
        }

        /// <summary>
        /// Decides now if the buyer's grab and authority are already here, else when the seed's lifecycle next changes
        /// (its grabber message arriving), or refuses at the timeout. They race the request, in no fixed order.
        /// </summary>
        private void BeginResolve(Seed seed)
        {
            if (_resolving.ContainsKey(seed)) return;
            if (TryResolve(seed, false)) return;
            Action retry = () => { if (TryResolve(seed, false)) EndResolve(seed); };
            _resolving[seed] = retry;
            seed.LifecycleChanged += retry;
            StartCoroutine(ResolveTimeout(seed));
        }

        private IEnumerator ResolveTimeout(Seed seed)
        {
            yield return new WaitForSeconds(RESOLVE_TIMEOUT_SECONDS);
            if (!_resolving.ContainsKey(seed)) yield break;
            EndResolve(seed);
            TryResolve(seed, true);
        }

        private void EndResolve(Seed seed)
        {
            if (seed != null && _resolving.TryGetValue(seed, out Action retry)) seed.LifecycleChanged -= retry;
            _resolving.Remove(seed);
        }

        /// <summary>
        /// Charges or refuses. Not ready yet (no grab or authority known) returns false, unless this is the last word.
        /// Any refusal puts the seed back without charging: an uncharged sale cannot be undone, whereas a player can pick
        /// a seed up again.
        /// </summary>
        private bool TryResolve(Seed seed, bool final)
        {
            if (seed == null || seed.IsBought || !seed.InShop) return true;

            PlayerIdentity buyer = seed.GetGrabber();
            if (!buyer.Exists || !seed.HasKnownAuthority)
            {
                if (!final) return false;
                Refuse(seed, !buyer.Exists ? "was requested by a buyer this client does not know yet" : "has no state authority");
                return true;
            }

            // Priced from the seed itself, so a cycle mid-sale never charges for a different crop.
            SeedDefinition sold = seed.seedDefinition;
            if (sold == null) { Refuse(seed, "has no definition, so it has no price"); return true; }

            string buyerId = buyer.Id;
            PlayerBalance balance = EconomyManager.Instance == null ? null : EconomyManager.Instance.GetPlayer(buyerId);
            if (balance == null) { Refuse(seed, $"names a buyer with no balance on this client ('{buyerId}')"); return true; }

            if (balance.GetBalance() < sold.buyPrice)
            {
                Logger.Info($"TryResolve() '{gameObject.name}' — '{buyerId}' cannot afford {sold.seedId} ({balance.GetBalance()} < {sold.buyPrice})");
                Refuse(seed, "cannot be afforded");
                return true;
            }

            StallId stall = seed.Stall;
            seed.Purchase(buyerId);
            EconomyManager.Instance.RemoveBalance(buyerId, sold.buyPrice);
            if (_pinned.Contains(stall))
                Logger.Info($"TryResolve() '{gameObject.name}' — sold {stall} '{sold.seedId}' to '{buyerId}'; pinned, restocks now");
            else
            {
                MarkTaken(stall);
                Logger.Info($"TryResolve() '{gameObject.name}' — sold {stall} '{sold.seedId}' to '{buyerId}'; sold out until the next cycle");
            }
            SendEmptied(stall, seed.NetworkId);   // after the sold-out mark, so the stall hears it is sold out before it hears it is empty
            return true;
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
