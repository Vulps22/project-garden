using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.Islands;
using Vulps.GrowAGarden.Ownership;
using Vulps.GrowAGarden.Show;

namespace Vulps.GrowAGarden.WorldTree
{
    /// <summary>
    /// The world tree's show and its master's decisions (docs/yggdrasil.md): built, standing as a buff, rusting, toppling.
    /// Every client derives the stage from the plant's timestamps; the master only frees the slot and starts the fall.
    /// </summary>
    [RequireComponent(typeof(YggdrasilPlant))]
    public class YggdrasilTree : MonoBehaviour
    {
        /// <summary>How long the stream takes to build the tree.</summary>
        public const float BuildSeconds = 12f;
        /// <summary>From the fall beginning to the tree far below the floor, when the master despawns it.</summary>
        public const float DyingSeconds = 34f;

        // The dying, in seconds after the fall begins.
        private const float RustEnd = 16f, ShedStart = 8f, ShedEnd = 20f, LetGo = 21f, Topple = 24f;
        private const float ToppleRate = 25f, MaxLean = 75f, Push = 1.5f, Gravity = 9.81f;

        [Tooltip("Placed at its Plot's transform: every variant is modelled in its Plot's local space. Carries the Climbable marker.")]
        [SerializeField] private Transform _tree;
        [Tooltip("One per Plot of the Garden, in the Garden's Plot order: each a pivot at its trunk's base.")]
        [SerializeField] private Transform[] _variants = new Transform[4];
        [Tooltip("Rises from the sapling and builds the tree: no gravity, steered piece by piece onto it.")]
        [SerializeField] private ParticleSystem _stream;
        [Tooltip("Rusty leaves falling from the canopy as it dies.")]
        [SerializeField] private ParticleSystem _leaves;
        [Tooltip("The earthbending rumble while it is built. Its mixer group is copied from the Garden's own, so it follows the player's volume.")]
        [SerializeField] private AudioSource _rumble;
        [Tooltip("The leaves' colour when they have rusted.")]
        [SerializeField] private Color _rust = new Color(0.55f, 0.25f, 0.08f);
        [Tooltip("How fast the stream flies, in metres per second.")]
        [SerializeField] private float _streamSpeed = 30f;
        [Tooltip("Pieces per second while the tree is being built.")]
        [SerializeField] private float _streamRate = 90f;

        private YggdrasilPlant _plant;
        private PlotStateManager _plot;
        private PlotLeaseManager _lease;
        private Transform _variant;
        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private readonly List<(Transform shed, Vector3 scale)> _crowns = new List<(Transform, Vector3)>();
        private readonly List<(Material material, Color colour)> _leafMaterials = new List<(Material, Color)>();
        private readonly List<Material> _instances = new List<Material>();
        private Collider[] _colliders = new Collider[0];
        private RootDangle _dangle;
        private GameObject _farCanopy;
        private bool _rumbled, _released, _materialsTaken;
        private long _buffEnd = -1;
        private ParticleSystem.Particle[] _particles;

        private void Awake()
        {
            _plant = GetComponent<YggdrasilPlant>();
            foreach (Transform v in _variants) if (v != null) v.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_lease != null) _lease.OwnerChanged -= OnOwnerChanged;
            foreach (Material material in _instances) Destroy(material);
        }

        // Once this client knows the Plot: stand the right variant behind it.
        private bool Placed()
        {
            if (_variant != null) return true;
            if (_plant.Plot == null) return false;
            _plot = _plant.Plot;
            _lease = _plot.GetComponent<PlotLeaseManager>();
            if (_lease == null) { Logger.Error($"Placed() '{gameObject.name}' — Plot '{_plot.name}' has no lease; the tree cannot stand"); enabled = false; return false; }

            int index = System.Array.IndexOf(Garden.PlotsFor(_lease), _lease);
            if (index < 0 || index >= _variants.Length || _variants[index] == null)
            {
                Logger.Error($"Placed() '{gameObject.name}' — no variant for Plot index {index}; the tree cannot stand");
                enabled = false;
                return false;
            }
            _tree.SetPositionAndRotation(_lease.transform.position, _lease.transform.rotation);
            _variant = _variants[index];
            _restPosition = _variant.localPosition;
            _restRotation = _variant.localRotation;
            foreach (Transform t in _variant.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Shed_")) _crowns.Add((t, t.localScale));
                if (t.name.StartsWith("Canopy_LOD1")) _farCanopy = t.gameObject;
            }
            _colliders = _variant.GetComponentsInChildren<Collider>(true);
            _dangle = _variant.GetComponentInChildren<RootDangle>(true);
            if (_dangle != null) _dangle.enabled = false;

            ShowStage stage = ShowStage.For(_lease.transform.position);
            AudioSource loadTime = stage == null ? null : stage.GetComponentInChildren<AudioSource>(true);
            if (_rumble != null && loadTime != null) _rumble.outputAudioMixerGroup = loadTime.outputAudioMixerGroup;

            _lease.OwnerChanged += OnOwnerChanged;
            Logger.Info($"Placed() '{gameObject.name}' — standing behind Plot {index} of its Garden at {_tree.position}");
            return true;
        }

        /// <summary>Its Plot freed and then claimed again starts the fall. Decided on the master.</summary>
        private void OnOwnerChanged(PlotLeaseManager lease)
        {
            if (!PlayerManager.IsMaster || _plant.FormedAt == 0 || Now() < _plant.FormedAt) return;
            if (!lease.IsClaimed) { _released = true; return; }
            if (_released) _plant.BeginFall($"its Plot was claimed again by '{lease.OwnerId}'");
        }

        private static double Now() => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        private void Update()
        {
            if (_plant.GrownAt == 0 || !Placed()) return;
            double now = Now();
            double built = (now - _plant.GrownAt) / BuildSeconds;
            bool formed = built >= 1.0;
            double dying = _plant.FallAt == 0 ? -1 : now - _plant.FallAt;

            _variant.gameObject.SetActive(built > 0.0);
            if (built <= 0.0) return;

            Decide(now, formed);
            Buff();
            Build((float)built);
            foreach (Collider c in _colliders) c.enabled = formed && dying < LetGo;
            if (dying >= 0) Die((float)dying);
        }

        // Master: free the slot once formed, and start the fall when its time is up.
        private void Decide(double now, bool formed)
        {
            if (!PlayerManager.IsMaster || !_plant.HasLocalAuthority || !formed) return;
            _plant.FreeSlot();
            if (_lease != null && !_lease.IsClaimed) _released = true;   // its owner left while it was built
            if (_plant.FallAt == 0 && now >= _plant.FormedAt + Seeds.YggdrasilSeed.LifeSeconds) _plant.BeginFall("its thirty minutes are up");
        }

        // The buff stands from the tree being formed to its fall, recorded on the Plot on every client.
        private void Buff()
        {
            long end = _plant.FallAt == 0 ? 0 : _plant.FallAt;
            if (end == _buffEnd) return;
            _buffEnd = end;
            _plot.SetBuff(_plant, _plant.seedDefinition.buffMultiplier, _plant.FormedAt, end);
        }

        private void Build(float built)
        {
            float t = Mathf.Clamp01(built);
            float scale = t * t * (3f - 2f * t);
            if (_plant.FallAt == 0 || Now() < _plant.FallAt + Topple) _variant.localScale = Vector3.one * Mathf.Max(0.001f, scale);

            if (_stream != null)
            {
                _stream.transform.position = _plant.transform.position + Vector3.up * 1.2f;
                ParticleSystem.EmissionModule emission = _stream.emission;
                emission.rateOverTime = built < 1f - 1.5f / BuildSeconds ? _streamRate : 0f;   // the last pieces still have to fly
                if (!_stream.isPlaying) _stream.Play();
            }

            // The rumble, once, from where the build has got to: a late joiner mid-build hears the rest of it.
            if (!_rumbled && _rumble != null && _rumble.clip != null)
            {
                _rumbled = true;
                float into = built * BuildSeconds;
                if (into < _rumble.clip.length - 0.5f)
                {
                    _rumble.transform.position = _variant.position + Vector3.up * 20f;
                    _rumble.time = Mathf.Max(0f, into);
                    _rumble.Play();
                }
            }
        }

        /// <summary>Steers each piece of the stream onto the tree as far as it has been built; it vanishes on arrival.</summary>
        private void LateUpdate()
        {
            if (_stream == null || _variant == null || _stream.particleCount == 0) return;
            if (_particles == null || _particles.Length < _stream.main.maxParticles) _particles = new ParticleSystem.Particle[_stream.main.maxParticles];
            int count = _stream.GetParticles(_particles);
            float grown = _variant.localScale.x;
            Vector3 foot = _variant.position, up = _variant.up;
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle p = _particles[i];
                // Each piece keeps its own spot on the tree: higher ones spread wider, as the crown does.
                float h = (p.randomSeed % 997) / 996f;
                float a = (p.randomSeed % 359) * Mathf.Deg2Rad;
                float reach = Mathf.Lerp(5f, 50f, h * h * h) * grown;
                Vector3 target = foot + up * (h * 85f * grown) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * reach;
                Vector3 to = target - p.position;
                float distance = to.magnitude;
                if (distance < 2f) { p.remainingLifetime = 0f; _particles[i] = p; continue; }
                float swirl = Mathf.Sin(Time.time * 2f + p.randomSeed) * 4f;
                Vector3 side = Vector3.Cross(to / distance, Vector3.up) * swirl;
                p.velocity = Vector3.Lerp(p.velocity, (to / distance) * _streamSpeed + side, Time.deltaTime * 3f);
                _particles[i] = p;
            }
            _stream.SetParticles(_particles, count);
        }

        private void Die(float dying)
        {
            Rust(Mathf.Clamp01(dying / RustEnd));

            if (_leaves != null)
            {
                _leaves.transform.SetPositionAndRotation(_variant.position + Vector3.up * 80f, Quaternion.identity);
                ParticleSystem.EmissionModule emission = _leaves.emission;
                emission.enabled = dying > 2f && dying < ShedEnd;
                if (!_leaves.isPlaying) _leaves.Play();
            }

            float shed = Mathf.Clamp01((dying - ShedStart) / (ShedEnd - ShedStart));
            float keep = 1f - shed * shed * (3f - 2f * shed);
            foreach ((Transform crown, Vector3 scale) in _crowns) crown.localScale = scale * Mathf.Max(0.001f, keep);
            if (_farCanopy != null) _farCanopy.SetActive(keep > 0.01f);

            if (_dangle != null && dying >= LetGo && !_dangle.enabled) _dangle.enabled = true;

            if (dying < Topple) return;
            // Tipped outward over the rim, away from the Plot, and dropped: the same pose on every client from the clock.
            float t = dying - Topple;
            float lean = Mathf.Min(MaxLean, 0.5f * ToppleRate * t * t);
            float drop = 0.5f * Gravity * Mathf.Max(0f, t - 0.6f) * Mathf.Max(0f, t - 0.6f);
            _variant.localRotation = Quaternion.AngleAxis(-lean, Vector3.right) * _restRotation;
            _variant.localPosition = _restPosition + Vector3.back * (Push * t) + Vector3.down * drop;
        }

        // The leaves turn to rust: their own copies of the leaf materials, made the first time the tree dies.
        private void Rust(float rust)
        {
            if (!_materialsTaken)
            {
                _materialsTaken = true;
                foreach (Renderer r in _variant.GetComponentsInChildren<Renderer>(true))
                {
                    bool leafy = false;
                    foreach (Material m in r.sharedMaterials) leafy |= m != null && m.name.Contains("Leaves");
                    if (!leafy) continue;
                    Material[] materials = r.materials;
                    _instances.AddRange(materials);
                    foreach (Material m in materials)
                        if (m.name.Contains("Leaves")) _leafMaterials.Add((m, m.GetColor("_BaseColor")));
                }
            }
            foreach ((Material material, Color colour) in _leafMaterials) material.SetColor("_BaseColor", Color.Lerp(colour, _rust, rust));
        }
    }
}
