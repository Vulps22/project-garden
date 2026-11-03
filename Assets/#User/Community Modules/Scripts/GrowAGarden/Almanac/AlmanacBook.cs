using System;
using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.Crops;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Almanac
{
    /// <summary>
    /// The Almanac's contents: a numbered entry for every crop (the stall pool first, then the ones listed as coming soon), each crop's rarity in the pool, and the local player's own record of each, kept on this device.
    /// Everything is local: the crops are the scene's definitions, the stall count is the stock table every client keeps,
    /// and the record is this player's. Nothing is sent. See docs/almanac.md.
    /// </summary>
    public class AlmanacBook : MonoBehaviour
    {
        private const string STORAGE_KEY = "almanac";

        /// <summary>One crop's page.</summary>
        public sealed class Entry
        {
            public int Number;
            public SeedDefinition Crop;
            public bool InPool;
            public float Share;    // of all stall rolls, 0..1; 0 outside the pool
            public string Kind;
        }

        [Serializable]
        private class Row
        {
            public string id;
            public bool collected;
            public int planted;
            public int harvested;
            public int sold;
            public int best;
        }

        [Serializable]
        private class Stored
        {
            public List<Row> rows = new List<Row>();
        }

        [Tooltip("Crops not in the stalls yet, shown as coming soon: their definitions, wherever they live. A crop added to " +
                 "the stall pool (WorldManager) moves to its place there by itself.")]
        [SerializeField] private SeedDefinition[] _comingSoon = new SeedDefinition[0];

        private static AlmanacBook _instance;
        private readonly List<Entry> _entries = new List<Entry>();
        private Stored _record = new Stored();
        private readonly HashSet<int> _harvestedSeen = new HashSet<int>();
        private bool _loaded;
        private bool _dirty;
        private float _saveAt;

        /// <summary>The record or the entries changed: an open page redraws.</summary>
        public static event Action Changed;

        public static IReadOnlyList<Entry> Entries => _instance == null ? (IReadOnlyList<Entry>)Array.Empty<Entry>() : _instance.Build();

        /// <summary>A named tier for a share of the stall rolls.</summary>
        public static string Tier(float share) =>
            share >= 0.15f ? "Common" : share >= 0.075f ? "Uncommon" : share >= 0.03f ? "Rare" : "Very Rare";

        /// <summary>Whether this player has bought the crop's seed from a stall (the Developer Market does not count).</summary>
        public static bool Collected(string seedId) => _instance != null && _instance.RowOf(seedId, false)?.collected == true;

        /// <summary>This player's planted, harvested and sold counts for a crop, and their best single sale.</summary>
        public static (int planted, int harvested, int sold, int best) Record(string seedId)
        {
            Row row = _instance?.RowOf(seedId, false);
            return row == null ? (0, 0, 0, 0) : (row.planted, row.harvested, row.sold, row.best);
        }

        private void Awake() => _instance = this;

        private void OnEnable()
        {
            Seed.AnyPurchased += OnPurchased;
            Seed.AnyPlanted += OnPlanted;
            Produce.AnyLifecycleChanged += OnProduceChanged;
            SellPoint.SaleOfCrop += OnSold;
        }

        private void OnDisable()
        {
            Seed.AnyPurchased -= OnPurchased;
            Seed.AnyPlanted -= OnPlanted;
            Produce.AnyLifecycleChanged -= OnProduceChanged;
            SellPoint.SaleOfCrop -= OnSold;
            Save();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            Changed = null;
        }

        private void Update()
        {
            if (_dirty && Time.time >= _saveAt) Save();
        }

        // ── The pages ─────────────────────────────────────────────────────────────

        private IReadOnlyList<Entry> Build()
        {
            if (_entries.Count > 0) return _entries;
            var pool = new List<SeedDefinition>();
            if (WorldManager.Instance != null) foreach (SeedDefinition d in WorldManager.Instance.Buyables) if (d != null) pool.Add(d);
            var rest = new List<SeedDefinition>();
            foreach (SeedDefinition d in _comingSoon)
                if (d != null && !pool.Exists(p => p.seedId == d.seedId) && !rest.Exists(r => r.seedId == d.seedId)) rest.Add(d);

            float total = 0f;
            foreach (SeedDefinition d in pool) total += Mathf.Max(0f, d.spawnWeight);
            foreach (SeedDefinition d in pool) Add(d, true, total > 0f ? Mathf.Max(0f, d.spawnWeight) / total : 0f);
            foreach (SeedDefinition d in rest) Add(d, false, 0f);
            return _entries;
        }

        private void Add(SeedDefinition crop, bool inPool, float share) =>
            _entries.Add(new Entry { Number = _entries.Count + 1, Crop = crop, InPool = inPool, Share = share, Kind = KindOf(crop) });

        /// <summary>What the crop's plant is: from the plant its seed grows.</summary>
        private static string KindOf(SeedDefinition crop)
        {
            Seed seed = crop.seedPrefab == null ? null : crop.seedPrefab.GetComponent<Seed>();
            GameObject plant = seed == null || seed.PlantPrefab == null ? null : seed.PlantPrefab.gameObject;
            if (plant == null) return "Crop";
            if (plant.GetComponent<StandingPlant>() != null) return "Upgrade";
            if (plant.GetComponent<RootedPlant>() != null) return "Root crop";
            if (plant.GetComponent<MultiHarvestPlant>() != null) return $"Regrowing crop · fruits {crop.harvestCount} times";
            return "Crop";
        }

        // ── The record ────────────────────────────────────────────────────────────

        private Row RowOf(string seedId, bool create)
        {
            if (string.IsNullOrEmpty(seedId)) return null;
            Load();
            Row row = _record.rows.Find(r => r.id == seedId);
            if (row == null && create) { row = new Row { id = seedId }; _record.rows.Add(row); }
            return row;
        }

        private static bool Mine(string playerId) => !string.IsNullOrEmpty(playerId) && playerId == PlayerManager.LocalPlayerId;

        private void OnPurchased(Seed seed)
        {
            if (!Mine(seed.OwnerId) || !seed.HasStall || seed.seedDefinition == null) return;
            Row row = RowOf(seed.seedDefinition.seedId, true);
            if (row.collected) return;
            row.collected = true;
            Logger.Info($"OnPurchased() '{gameObject.name}' — '{seed.seedDefinition.seedId}' collected in the Almanac");
            Touch();
        }

        private void OnPlanted(Seed seed)
        {
            if (!Mine(seed.OwnerId) || seed.seedDefinition == null) return;
            RowOf(seed.seedDefinition.seedId, true).planted++;
            Touch();
        }

        // The harvest and the RPC naming the holder arrive in either order: counted once, when both have.
        private void OnProduceChanged(Produce produce)
        {
            if (produce == null || !produce.IsHarvested || !Mine(produce.HolderId) || produce.seedDefinition == null) return;
            if (!_harvestedSeen.Add(produce.GetInstanceID())) return;
            RowOf(produce.seedDefinition.seedId, true).harvested++;
            Touch();
        }

        private void OnSold(string sellerId, int value, string seedId)
        {
            if (!Mine(sellerId) || string.IsNullOrEmpty(seedId)) return;
            Row row = RowOf(seedId, true);
            row.sold++;
            row.best = Mathf.Max(row.best, value);
            Touch();
        }

        private void Touch()
        {
            if (!_dirty) _saveAt = Time.time + 2f;
            _dirty = true;
            Changed?.Invoke();
        }

        private void Load()
        {
            if (_loaded) return;
            _loaded = true;
            if (!StorageBridge.Exists(STORAGE_KEY, gameObject.name)) return;
            Stored stored = StorageBridge.Read<Stored>(STORAGE_KEY, gameObject.name);
            if (stored?.rows != null) _record = stored;
        }

        private void Save()
        {
            if (!_dirty) return;
            _dirty = false;
            StorageBridge.Write(STORAGE_KEY, _record, gameObject.name);
        }
    }
}
