using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Islands
{
    /// <summary>Places islands from the world seed on an unbounded grid around the local player's sector; every client derives the same layout.</summary>
    public class IslandField : MonoBehaviour
    {
        [Tooltip("The islands the field may place. Order is part of the layout: reordering moves every island.")]
        [SerializeField] private SpawnableIsland[] _islands;

        [Tooltip("Width of one grid cell, in metres. Each cell proposes at most one island.")]
        [SerializeField] private float _cellSize = 150f;

        [Tooltip("Chance that a cell holds an island.")]
        [Range(0f, 1f)]
        [SerializeField] private float _density = 0.9f;

        [Tooltip("Gap kept between an island's proposed centre and its cell's edge, in metres.")]
        [SerializeField] private float _cellMargin = 0f;

        [Tooltip("Islands sit between -this and +this metres of the field's height. Gardens always sit at the field's height.")]
        [SerializeField] private float _heightRange = 25f;

        [Tooltip("No island's footprint reaches inside this radius of any Garden in the scene.")]
        [SerializeField] private float _gardenClearRadius = 110f;

        [Tooltip("The buy point the seed puts on an island's chosen stall sockets.")]
        [SerializeField] private BuyPoint _buyPoint;

        [Tooltip("Cells within this distance of the player are loaded, on top of every island touching the player's sector.")]
        [SerializeField] private float _viewRadius = 1200f;


        private readonly Dictionary<Vector2Int, GameObject> _placed = new Dictionary<Vector2Int, GameObject>();
        private readonly List<Vector2Int> _leaving = new List<Vector2Int>();
        private readonly HashSet<Vector2Int> _wanted = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, Placement> _resolved = new Dictionary<Vector2Int, Placement>();
        private static IslandField _instance;

        /// <summary>Raised after islands are placed or removed: whatever stood on missing ground can look again.</summary>
        public static event System.Action GroundChanged;

        /// <summary>Whether this client has the ground at a world point: its island cell is loaded, or it is on a Garden.</summary>
        public static bool HasGroundAt(Vector3 world)
        {
            foreach (Garden garden in Garden.All)
                if (garden != null && Garden.FlatSqr(garden.transform.position, world) <= GARDEN_GROUND * GARDEN_GROUND) return true;
            return _instance != null && _instance._placed.ContainsKey(_instance.CellOf(world - _instance.transform.position));
        }

        private const float GARDEN_GROUND = 60f;   // a Garden's island reaches about this far from its centre

        /// <summary>Which island a cell holds, where it sits and how much room it takes; null prefab for an empty cell.</summary>
        public readonly struct Placement
        {
            public readonly SpawnableIsland Prefab;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly float Radius;
            public readonly ulong Rank;

            public Placement(SpawnableIsland prefab, Vector3 position, Quaternion rotation, float radius, ulong rank)
            {
                Prefab = prefab;
                Position = position;
                Rotation = rotation;
                Radius = radius;
                Rank = rank;
            }

            public Placement MovedTo(Vector3 position) => new Placement(Prefab, position, Rotation, Radius, Rank);
        }

        // Refreshed only when something it depends on changes: the player's cell or sector, the seed, the draw distance.
        private void OnEnable()
        {
            _instance = this;
            WorldSeed.SeedKnown += OnSeedKnown;
            SectorManager.LocalSectorChanged += OnLocalSectorChanged;
            Settings.GameSettings.Changed += OnSettingsChanged;
            PlayerTracker.Watch(p => CellOf(p - transform.position), OnPlayerMoved);
        }

        private void OnDisable()
        {
            WorldSeed.SeedKnown -= OnSeedKnown;
            SectorManager.LocalSectorChanged -= OnLocalSectorChanged;
            Settings.GameSettings.Changed -= OnSettingsChanged;
            PlayerTracker.Unwatch(OnPlayerMoved);
            Clear();
            if (_instance == this) _instance = null;
        }

        private void OnSeedKnown(long seed)
        {
            Clear();
            RefreshAroundPlayer();
        }

        private void OnPlayerMoved(Vector3 at) => RefreshAroundPlayer();
        private void OnLocalSectorChanged() => RefreshAroundPlayer();

        private float _refreshedFor = -1f;

        private void OnSettingsChanged()
        {
            if (Mathf.Approximately(ViewRadius, _refreshedFor)) return;
            RefreshAroundPlayer();
        }

        private void RefreshAroundPlayer()
        {
            if (!WorldSeed.HasSeed || !PlayerTracker.HasPosition) return;
            if (_islands == null || _islands.Length == 0) return;
            _refreshedFor = ViewRadius;
            Refresh(PlayerTracker.Position);
        }

        /// <summary>The player's draw distance from World Settings, or the scene's radius without settings (docs/detail.md).</summary>
        private float ViewRadius
        {
            get
            {
                Settings.GameSettings settings = Settings.GameSettings.GetInstance();
                return settings != null ? settings.DrawDistance : _viewRadius;
            }
        }

        /// <summary>Loads every island touching the local sector, plus every cell within view, and removes the rest.</summary>
        private void Refresh(Vector3 around)
        {
            Vector3 origin = transform.position;
            _resolved.Clear();
            _wanted.Clear();

            float view = ViewRadius;
            int reach = Mathf.CeilToInt(view / _cellSize);
            Vector2Int centre = CellOf(around - origin);
            float viewSqr = view * view;
            for (int dz = -reach; dz <= reach; dz++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                var cell = new Vector2Int(centre.x + dx, centre.y + dz);
                if (FlatDistanceSqr(CellCentre(cell) + origin, around) <= viewSqr) _wanted.Add(cell);
            }
            AddLocalSectorCells(origin);

            int added = 0;
            foreach (Vector2Int cell in _wanted)
            {
                if (_placed.ContainsKey(cell)) continue;
                added++;
                Placement p = Resolve(WorldSeed.Seed, cell);
                if (p.Prefab == null) { _placed[cell] = null; continue; }
                SpawnableIsland island = Instantiate(p.Prefab, p.Position + origin, p.Rotation, transform);
                island.PlacedIn(cell);
                island.OpenStalls(Mix(Hash(WorldSeed.Seed, cell.x, cell.y) ^ 0xBu), _buyPoint);
                _placed[cell] = island.gameObject;
            }

            float leaveSqr = (view + _cellSize) * (view + _cellSize);
            _leaving.Clear();
            foreach (var pair in _placed)
                if (!_wanted.Contains(pair.Key) && FlatDistanceSqr(CellCentre(pair.Key) + origin, around) > leaveSqr) _leaving.Add(pair.Key);

            foreach (Vector2Int cell in _leaving)
            {
                if (_placed[cell] != null) Destroy(_placed[cell]);
                _placed.Remove(cell);
            }
            if (added > 0 || _leaving.Count > 0) GroundChanged?.Invoke();
        }

        /// <summary>Adds every cell whose island reaches into the local sector, so the whole sector is always loaded.</summary>
        private void AddLocalSectorCells(Vector3 origin)
        {
            if (!SectorManager.HasLocalSector) return;

            Vector3 centre = SectorManager.SectorCentre(SectorManager.LocalSector) - origin;
            float half = SectorManager.SectorSize * 0.5f;
            float pad = _cellSize + 2f * SpawnableIsland.FootprintRadius(IslandCategory.Landmark);
            Vector2Int min = CellOf(centre - new Vector3(half + pad, 0f, half + pad));
            Vector2Int max = CellOf(centre + new Vector3(half + pad, 0f, half + pad));

            for (int z = min.y; z <= max.y; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                var cell = new Vector2Int(x, z);
                if (_wanted.Contains(cell)) continue;
                Placement p = Resolve(WorldSeed.Seed, cell);
                if (p.Prefab == null) continue;
                float dx = Mathf.Max(Mathf.Abs(p.Position.x - centre.x) - half, 0f);
                float dz = Mathf.Max(Mathf.Abs(p.Position.z - centre.z) - half, 0f);
                if (dx * dx + dz * dz < p.Radius * p.Radius) _wanted.Add(cell);
            }
        }

        /// <summary>The island a cell holds for a seed, relative to this field's origin. Pure: same inputs, same answer, on every client.</summary>
        public Placement PlacementFor(long seed, Vector2Int cell)
        {
            _resolved.Clear();
            return Resolve(seed, cell);
        }

        /// <summary>A cell's final island: its proposal, pushed clear of every higher-ranked neighbour, or nothing if it cannot be.</summary>
        private Placement Resolve(long seed, Vector2Int cell)
        {
            if (_resolved.TryGetValue(cell, out Placement known)) return known;

            Placement me = Propose(seed, cell);
            if (me.Prefab == null) return _resolved[cell] = me;

            int reach = Mathf.CeilToInt(2f * SpawnableIsland.FootprintRadius(IslandCategory.Landmark) / _cellSize);
            var above = new List<Placement>();
            for (int dz = -reach; dz <= reach; dz++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                var other = new Vector2Int(cell.x + dx, cell.y + dz);
                Placement proposal = Propose(seed, other);
                if (proposal.Prefab == null || !Outranks(proposal, other, me, cell)) continue;

                Placement settled = Resolve(seed, other);
                if (settled.Prefab != null) above.Add(settled);
            }

            Vector3 position = me.Position;
            foreach (Placement other in above)
            {
                Vector3 apart = Flat(position - other.Position);
                float needed = me.Radius + other.Radius;
                float distance = apart.magnitude;
                if (distance >= needed) continue;

                Vector3 direction = distance > 0.001f ? apart / distance : AwayDirection(me.Rank);
                position += direction * (needed - distance);
            }

            foreach (Placement other in above)
                if (Flat(position - other.Position).magnitude < me.Radius + other.Radius - 0.01f)
                    return _resolved[cell] = default;

            if (TouchesGardenSite(position, me.Radius)) return _resolved[cell] = default;

            return _resolved[cell] = me.MovedTo(position);
        }

        /// <summary>What a cell would hold before any neighbour is considered.</summary>
        private Placement Propose(long seed, Vector2Int cell)
        {
            ulong h = Hash(seed, cell.x, cell.y);
            if (Unit(h, 0) >= _density) return default;

            SpawnableIsland prefab = _islands[(int)(Mix(h ^ 0x1u) % (ulong)_islands.Length)];
            if (prefab == null) return default;

            float half = Mathf.Max(0f, _cellSize * 0.5f - _cellMargin);
            Vector3 centre = CellCentre(cell);
            var position = new Vector3(
                centre.x + Mathf.Lerp(-half, half, Unit(h, 2)),
                Mathf.Lerp(-_heightRange, _heightRange, Unit(h, 3)),
                centre.z + Mathf.Lerp(-half, half, Unit(h, 4)));
            Quaternion rotation = Quaternion.Euler(0f, Unit(h, 5) * 360f, 0f);
            float radius = SpawnableIsland.FootprintRadius(prefab.Category);

            if (TouchesGardenSite(position, radius)) return default;
            return new Placement(prefab, position, rotation, radius, Mix(h ^ 0x7u));
        }

        /// <summary>Whether proposal a stays put when it meets proposal b; ties go to the larger cell coordinate.</summary>
        private static bool Outranks(Placement a, Vector2Int cellA, Placement b, Vector2Int cellB)
        {
            if (a.Rank != b.Rank) return a.Rank > b.Rank;
            if (cellA.x != cellB.x) return cellA.x > cellB.x;
            return cellA.y > cellB.y;
        }

        /// <summary>A flat direction for two islands proposed at exactly the same spot.</summary>
        private static Vector3 AwayDirection(ulong rank)
        {
            float angle = Unit(rank, 9) * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Every island within a radius of a world point, in world space, derived from the seed alone.</summary>
        public void PlacementsNear(Vector3 worldCentre, float radius, List<Placement> results)
        {
            results.Clear();
            if (!WorldSeed.HasSeed || _islands == null || _islands.Length == 0) return;

            Vector3 origin = transform.position;
            Vector2Int centre = CellOf(worldCentre - origin);
            int reach = Mathf.CeilToInt(radius / _cellSize) + 1;
            float radiusSqr = radius * radius;

            for (int dz = -reach; dz <= reach; dz++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                Placement p = Resolve(WorldSeed.Seed, new Vector2Int(centre.x + dx, centre.y + dz));
                if (p.Prefab == null) continue;
                Placement world = p.MovedTo(p.Position + origin);
                if (FlatDistanceSqr(world.Position, worldCentre) <= radiusSqr) results.Add(world);
            }
        }

        // Gardens are placed in the scene (docs/towns.md), so islands clear around the real ones, nowhere else.
        private bool TouchesGardenSite(Vector3 local, float radius)
        {
            Vector3 world = local + transform.position;
            float reach = _gardenClearRadius + radius;
            foreach (Garden garden in Garden.All)
                if (garden != null && Garden.FlatSqr(garden.transform.position, world) < reach * reach) return true;
            return false;
        }

        private Vector2Int CellOf(Vector3 local) =>
            new Vector2Int(Mathf.FloorToInt(local.x / _cellSize), Mathf.FloorToInt(local.z / _cellSize));

        private Vector3 CellCentre(Vector2Int cell) =>
            new Vector3((cell.x + 0.5f) * _cellSize, 0f, (cell.y + 0.5f) * _cellSize);

        private static float FlatDistanceSqr(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return x * x + z * z;
        }

        /// <summary>Mixes the seed and a cell into one 64-bit value.</summary>
        private static ulong Hash(long seed, int x, int z)
        {
            unchecked
            {
                ulong h = Mix((ulong)seed);
                h = Mix(h ^ ((ulong)(uint)x * 0x9E3779B97F4A7C15UL));
                h = Mix(h ^ ((ulong)(uint)z * 0xC2B2AE3D27D4EB4FUL));
                return h;
            }
        }

        /// <summary>A value in [0, 1) drawn from a hash and a stream index.</summary>
        internal static float Unit(ulong h, uint stream) =>
            (Mix(h ^ (stream * 0xD6E8FEB86659FD93UL)) >> 40) / 16777216f;

        /// <summary>SplitMix64's finaliser.</summary>
        internal static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        private void Clear()
        {
            foreach (GameObject island in _placed.Values)
                if (island != null) Destroy(island);
            _placed.Clear();
            _resolved.Clear();
            GroundChanged?.Invoke();
        }

        private void OnValidate()
        {
            if (_cellSize < 1f) _cellSize = 1f;
            if (_cellMargin < 0f) _cellMargin = 0f;
            if (_viewRadius < _cellSize) _viewRadius = _cellSize;
            if (_heightRange < 0f) _heightRange = 0f;
        }
    }
}
