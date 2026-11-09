using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.Crops;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.WorldTree
{
    /// <summary>
    /// Yggdrasil's sapling (docs/yggdrasil.md): grown, it builds the world tree (<see cref="YggdrasilTree"/>) and shrinks away.
    /// Its life is derived from planted and fall (the wither timestamp) on every client. One in the world at a time.
    /// </summary>
    public class YggdrasilPlant : Plant
    {
        // Every Yggdrasil in the world, on every client.
        private static readonly HashSet<YggdrasilPlant> _all = new HashSet<YggdrasilPlant>();

        /// <summary>One at a time, worldwide: none may be planted while another exists.</summary>
        public override bool MayBePlanted => _all.Count == 0;

        /// <summary>Whether a Yggdrasil exists anywhere in the world, sapling to falling tree.</summary>
        public static bool AnyStanding => _all.Count > 0;

        /// <summary>The Plot it stands behind, once this client knows it.</summary>
        public PlotStateManager Plot => _standingBehind;
        public long PlantedAt => _plantedTimestamp;

        /// <summary>When the sapling is grown and the tree starts being built, or 0 before the state has arrived.</summary>
        public long GrownAt => _plantedTimestamp == 0 ? 0 : _plantedTimestamp + (long)Mathf.Ceil(seedDefinition.growthDuration);
        public long FormedAt => GrownAt == 0 ? 0 : GrownAt + (long)Mathf.Ceil(YggdrasilTree.BuildSeconds);

        /// <summary>When it starts to fall: its time up, or sooner if its Plot is claimed again. 0 while it stands.</summary>
        public long FallAt => _witherTimestamp;

        [Tooltip("Runes spewing up from the sapling like a waterfall in reverse, thicker as it grows; off once it is grown.")]
        [SerializeField] private ParticleSystem _runes;
        [Tooltip("Runes per second when the sapling has only just been planted, and when it is almost grown.")]
        [SerializeField] private Vector2 _runeRate = new Vector2(3f, 70f);
        [Tooltip("How hard the runes rise, as a negative gravity, at the start and near the end of growing.")]
        [SerializeField] private Vector2 _runeLift = new Vector2(0.15f, 0.8f);

        private PlotStateManager _standingBehind;   // kept apart from _plot, which the slot's release clears
        private bool _slotFreed;

        protected override void Awake()
        {
            base.Awake();
            _all.Add(this);
        }

        protected override void OnDestroy()
        {
            _all.Remove(this);
            base.OnDestroy();
        }

        protected override void OnPlotKnown(PlotStateManager plot) => _standingBehind = plot;

        protected override void OnFullyGrown()
        {
            Logger.Info($"OnFullyGrown() '{gameObject.name}' — the sapling is grown; building the world tree");
        }

        /// <summary>The sapling grows, then shrinks to nothing while the tree is built.</summary>
        protected override void ApplyBodyScale()
        {
            if (_bodyToScale == null) return;
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            float building = GrownAt == 0 ? 0f : Mathf.Clamp01((now - GrownAt) / YggdrasilTree.BuildSeconds);
            float scale = GetGrowthCompletion() * (1f - building) * _maxScale;
            _bodyToScale.localScale = Vector3.one * scale;
            if (_bodyToScale != transform) _bodyToScale.gameObject.SetActive(scale > 0.001f);
            ApplyRunes();
        }

        // The rune fountain, from the clock to the millisecond so it swells smoothly; nothing is sent.
        private void ApplyRunes()
        {
            if (_runes == null) return;
            double grown = _plantedTimestamp == 0 ? 0 : (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 - _plantedTimestamp) / Mathf.Max(0.01f, seedDefinition.growthDuration);
            bool flowing = _plantedTimestamp != 0 && grown < 1.0;
            float g = Mathf.Clamp01((float)grown), swell = g * g;
            ParticleSystem.EmissionModule emission = _runes.emission;
            emission.rateOverTime = flowing ? Mathf.Lerp(_runeRate.x, _runeRate.y, swell) : 0f;
            ParticleSystem.MainModule main = _runes.main;
            main.gravityModifier = -Mathf.Lerp(_runeLift.x, _runeLift.y, swell);
            ParticleSystem.ShapeModule shape = _runes.shape;
            shape.radius = Mathf.Lerp(0.1f, 0.45f, g) * _maxScale;
            if (flowing && !_runes.isPlaying) _runes.Play();
        }

        /// <summary>The tree is formed: it takes no slot from here on, and outlives its owner's Plot being cleaned up. Master only.</summary>
        public void FreeSlot()
        {
            if (_slotFreed || !PlayerManager.IsMaster || !HasLocalAuthority) return;
            _slotFreed = true;
            if (_plot != null && _slotIndex >= 0) _plot.Release(_slotIndex);
            _slotIndex = -1;
            broadcastState();
            Logger.Info($"FreeSlot() '{gameObject.name}' — the world tree is formed; its slot is free");
        }

        /// <summary>Starts the fall: the leaves turn, the roots let go and it topples into the void. Master only; the clock does the rest.</summary>
        public void BeginFall(string why)
        {
            if (_witherTimestamp != 0 || !PlayerManager.IsMaster || !HasLocalAuthority) return;
            Logger.Info($"BeginFall() '{gameObject.name}' — {why}");
            BeginWithering();
        }
    }
}
