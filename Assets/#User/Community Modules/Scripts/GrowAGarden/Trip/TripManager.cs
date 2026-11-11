using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vulps.GrowAGarden.Climbing;
using Vulps.GrowAGarden.Flight;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Trip
{
    /// <summary>
    /// The local player's high. One trip with a strength: eating a bloom is the intense one, each
    /// puff of a splif a mild one that lasts a little longer. Entirely local: nothing is sent, and
    /// only this player's view changes. Slow and smooth by design, never flashing. See docs/weed.md.
    /// </summary>
    public class TripManager : MonoBehaviour
    {
        /// <summary>Renderers were swapped to their melting twins or back: anything that sets materials itself can redo them.</summary>
        public static event System.Action MaterialsSwapped;

        [Tooltip("A global Volume, weight 0 at rest, whose profile is the full-strength trip.")]
        [SerializeField] private Volume _volume;

        [Tooltip("Seconds to come up to full strength.")]
        [SerializeField] private float _easeInSeconds = 12f;

        [Tooltip("Seconds to come down once the trip runs out.")]
        [SerializeField] private float _easeOutSeconds = 20f;

        [Tooltip("Seconds to come down when ended early.")]
        [SerializeField] private float _endEarlySeconds = 4f;

        [Tooltip("Seconds for one full cycle of the colours.")]
        [SerializeField] private float _hueCycleSeconds = 40f;

        [Tooltip("Furthest the hue swings each way, in degrees, at full strength.")]
        [SerializeField] private float _maxHueShift = 180f;

        [Header("Melting")]

        [Tooltip("Materials the world is drawn with, each swapped for the twin at the same index while tripping. Two plain arrays, not one of pairs: arrays of custom serializable types arrive empty from the bundle export.")]
        [SerializeField] private Material[] _materials = new Material[0];

        [Tooltip("The Vulps/TripLit twin of each material above, which sags and wobbles with the trip.")]
        [SerializeField] private Material[] _tripMaterials = new Material[0];

        [Tooltip("Seconds between looks for newly spawned things to melt while tripping.")]
        [SerializeField] private float _swapInterval = 2f;

        [Header("Flipped controls")]

        [Tooltip("Strength at or above which an eaten trip flips the controls now and then. Smoking never does.")]
        [SerializeField] private float _flipLevel = 0.8f;

        [Tooltip("Seconds the controls stay flipped.")]
        [SerializeField] private float _flipSeconds = 0.5f;

        [Tooltip("Seconds between flips, from and to.")]
        [SerializeField] private Vector2 _flipGap = new Vector2(6f, 15f);

        [Header("Ending early")]

        [Tooltip("Covering both eyes with both hands for this long ends the trip early.")]
        [SerializeField] private float _coverSeconds = 2f;

        [Tooltip("How close each hand must be to the head to count as covering the eyes, in metres.")]
        [SerializeField] private float _coverDistance = 0.18f;

        private static TripManager _instance;
        private ColorAdjustments _colour;
        private float _strength;
        private float _until;
        private float _level;
        private float _comeDown;
        private float _coveredFor;
        private bool _active;
        private float _nextSwap;
        private readonly Dictionary<Material, Material> _twins = new Dictionary<Material, Material>();
        private readonly Dictionary<Renderer, Material[]> _swapped = new Dictionary<Renderer, Material[]>();
        private static readonly int TripId = Shader.PropertyToID("_GAG_Trip");

        /// <summary>How strongly the local player is feeling it right now, 0 to 1.</summary>
        public static float Level => _instance != null ? _instance._level : 0f;

        /// <summary>True for the moment an eaten trip has flipped the controls. Flight reads this; walking is flipped here.</summary>
        public static bool ControlsFlipped => _instance != null && _instance._flippedUntil > Time.time;

        private bool _canFlip;
        private float _flippedUntil;
        private float _nextFlip;
        private bool _walkFlipped;
        private bool _airborne;
        private bool _climbing;

        /// <summary>Whether the local player is feeling anything at all.</summary>
        public static bool IsTripping => _instance != null && _instance._level > 0.001f;

        /// <summary>Starts a trip, or deepens and lengthens the one already running.</summary>
        public static void Begin(float strength, float seconds, string why, bool flipsControls = false)
        {
            if (_instance == null) return;
            if (!_instance._allowed) { Logger.Info($"Begin() 'TripManager' — {why}, but this player has stoned effects off"); return; }
            if (flipsControls) _instance._canFlip = true;
            _instance._strength = Mathf.Max(_instance.Running ? _instance._strength : 0f, Mathf.Clamp01(strength));
            _instance._until = Mathf.Max(_instance._until, Time.time + seconds);
            _instance._comeDown = _instance._easeOutSeconds;
            Logger.Info($"Begin() 'TripManager' — {why}: strength {_instance._strength:F2} for {_instance._until - Time.time:F0}s");
        }

        /// <summary>Adds time to the trip running, at least at this strength; starts one if none is.</summary>
        public static void Extend(float strength, float seconds, string why)
        {
            if (_instance == null) return;
            if (!_instance._allowed) return;
            if (!_instance.Running) { Begin(strength, seconds, why); return; }
            _instance._strength = Mathf.Max(_instance._strength, Mathf.Clamp01(strength));
            _instance._until += seconds;
            Logger.Info($"Extend() 'TripManager' — {why}: strength {_instance._strength:F2}, {_instance._until - Time.time:F0}s left");
        }

        private bool Running => Time.time < _until;

        private void Awake()
        {
            _instance = this;
            if (_volume == null)
            {
                Logger.Error($"Awake() '{gameObject.name}' — no trip Volume; trips will be felt by nobody");
                return;
            }
            _volume.weight = 0f;
            for (int i = 0; i < Mathf.Min(_materials.Length, _tripMaterials.Length); i++)
                if (_materials[i] != null && _tripMaterials[i] != null) _twins[_materials[i]] = _tripMaterials[i];
            Shader.SetGlobalFloat(TripId, 0f);
            if (!_volume.profile.TryGet(out _colour))
                Logger.Warn($"Awake() '{gameObject.name}' — trip profile has no ColorAdjustments; the colours will not cycle");
        }

        /// <summary>The player's World Settings choice; a trip is never started, and one running ends, while it is off.</summary>
        private bool _allowed = true;

        private void Start()
        {
            Settings.GameSettings.Changed += OnSettingsChanged;
            OnSettingsChanged();
        }

        private void OnSettingsChanged()
        {
            Settings.GameSettings settings = Settings.GameSettings.GetInstance();
            _allowed = settings == null || settings.AllowStonedEffects;
            if (_allowed || !Running) return;
            _until = Time.time;
            _comeDown = _endEarlySeconds;
            Logger.Info($"OnSettingsChanged() '{gameObject.name}' — stoned effects turned off; ending the trip");
        }

        private void OnEnable()
        {
            FlightController.Launched += OnLaunched;
            FlightController.Landed += OnLanded;
            ClimbController.ClimbStarted += OnClimbStarted;
            ClimbController.ClimbEnded += OnClimbEnded;
        }

        private void OnDisable()
        {
            FlightController.Launched -= OnLaunched;
            FlightController.Landed -= OnLanded;
            ClimbController.ClimbStarted -= OnClimbStarted;
            ClimbController.ClimbEnded -= OnClimbEnded;
            UnflipWalking();
        }

        private void OnLaunched() => _airborne = true;
        private void OnLanded(float seconds) => _airborne = false;
        private void OnClimbStarted() => _climbing = true;
        private void OnClimbEnded() => _climbing = false;

        private void OnDestroy()
        {
            Settings.GameSettings.Changed -= OnSettingsChanged;
            Unmelt();
            MaterialsSwapped = null;
            Shader.SetGlobalFloat(TripId, 0f);
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            float target = Running ? _strength : 0f;
            float seconds = target > _level ? _easeInSeconds : _comeDown;
            _level = Mathf.MoveTowards(_level, target, Time.deltaTime / Mathf.Max(0.1f, seconds));

            if (_volume != null)
            {
                _volume.weight = _level;
                if (_colour != null) _colour.hueShift.value = Mathf.Sin(Time.time * 2f * Mathf.PI / Mathf.Max(1f, _hueCycleSeconds)) * _maxHueShift;
            }

            // Squared, so the mild high barely stirs the world and the intense one melts it.
            Shader.SetGlobalFloat(TripId, _level * _level);

            bool active = _level > 0.001f;
            if (active != _active)
            {
                _active = active;
                if (!active) { Unmelt(); _canFlip = false; }
                Logger.Info($"Update() '{gameObject.name}' — trip {(active ? "coming up" : "over")}");
            }

            Flip();
            if (!active) return;
            WatchForCoveredEyes();
            if (Time.time >= _nextSwap) Melt();
        }

        /// <summary>Both hands over the eyes for a moment ends it: the obvious way out, at either strength.</summary>
        private void WatchForCoveredEyes()
        {
            PlayerRig rig = PlayerManager.LocalPlayerRig;
            Transform head = PlayerManager.LocalPlayerHead;
            if (!rig.HasHands || head == null) { _coveredFor = 0f; return; }

            bool covered = Vector3.Distance(rig.LeftHand.position, head.position) <= _coverDistance
                        && Vector3.Distance(rig.RightHand.position, head.position) <= _coverDistance;
            _coveredFor = covered ? _coveredFor + Time.deltaTime : 0f;
            if (_coveredFor < _coverSeconds || !Running) return;

            _until = Time.time;
            _comeDown = _endEarlySeconds;
            _coveredFor = 0f;
            Logger.Info($"WatchForCoveredEyes() '{gameObject.name}' — eyes covered; ending the trip early");
        }

        /// <summary>
        /// Now and then, deep in an eaten trip, the controls flip for a moment. Flight reads
        /// <see cref="ControlsFlipped"/>; walking is Somnium's, and the only lever on it is its speed,
        /// so walking is flipped by a negative speed. Flight and climbing own the speed while they run,
        /// so walking is only touched on the ground and only handed back if nothing else took it.
        /// </summary>
        private void Flip()
        {
            if (_walkFlipped && Time.time >= _flippedUntil) UnflipWalking();
            if (!_canFlip || _level < _flipLevel || Time.time < _nextFlip) return;

            _nextFlip = Time.time + Random.Range(_flipGap.x, _flipGap.y);
            _flippedUntil = Time.time + _flipSeconds;
            if (!_airborne && !_climbing) _walkFlipped = PlayerManager.SetLocalMovementScale(-1f);
            Logger.Info($"Flip() '{gameObject.name}' — controls flipped for {_flipSeconds}s ({(_airborne ? "flying" : _climbing ? "climbing" : _walkFlipped ? "walking at speed -1" : "walking: no motion to set")})");
        }

        private void UnflipWalking()
        {
            if (!_walkFlipped) return;
            _walkFlipped = false;
            if (!_airborne && !_climbing) PlayerManager.SetLocalMovementScale(1f);
        }

        /// <summary>Draws everything made of a known material with its melting twin. Only here, only while tripping.</summary>
        private void Melt()
        {
            _nextSwap = Time.time + Mathf.Max(0.5f, _swapInterval);
            if (_twins.Count == 0) return;

            int added = 0;
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || _swapped.ContainsKey(r)) continue;
                Material[] original = r.sharedMaterials;
                Material[] melted = null;
                for (int i = 0; i < original.Length; i++)
                {
                    if (original[i] == null || !_twins.TryGetValue(original[i], out Material twin)) continue;
                    melted ??= (Material[])original.Clone();
                    melted[i] = twin;
                }
                if (melted == null) continue;
                _swapped[r] = original;
                r.sharedMaterials = melted;
                added++;
            }
            if (added == 0) return;
            Logger.Info($"Melt() '{gameObject.name}' — {added} more renderer(s) melting, {_swapped.Count} in all");
            MaterialsSwapped?.Invoke();
        }

        /// <summary>Puts every melted renderer back as it was.</summary>
        private void Unmelt()
        {
            foreach (KeyValuePair<Renderer, Material[]> entry in _swapped)
                if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
            if (_swapped.Count == 0) return;
            Logger.Info($"Unmelt() '{gameObject.name}' — {_swapped.Count} renderer(s) back to normal");
            _swapped.Clear();
            MaterialsSwapped?.Invoke();
        }

        private void OnValidate()
        {
            if (_volume == null) _volume = GetComponentInChildren<Volume>();
        }
    }
}
