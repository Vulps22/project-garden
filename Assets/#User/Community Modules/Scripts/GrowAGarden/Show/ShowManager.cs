using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vulps.GrowAGarden.Show
{
    /// <summary>
    /// The Garden's light show. Which grown Moonflower holds the dome (the oldest, as the screen goes to the oldest
    /// popcorn), where the dome is, where the star is, and the music: each ARS band as a 0..1 level that adapts to the
    /// volume, and a Kick on each bass hit. With two or more Moonflowers beaming, the star glides to the top of the dome
    /// and every beam arcs in to meet it (a convergence, Vulps's idea). Local on every client and sends nothing: plants grow on the same clock everywhere, and each
    /// client hears the same screen. See docs/fun-seeds.md.
    /// </summary>
    public class ShowManager : MonoBehaviour
    {
        private static ShowManager _instance;
        private readonly List<MoonflowerPlant> _grown = new List<MoonflowerPlant>();
        private MoonflowerPlant _current;
        private bool _hadCurrent;
        private Vector3 _domeCentre;
        private float _domeRadius;
        private bool _hasDome;

        private Vector3 _star;
        private Vector3 _starVelocity;
        private bool _starPlaced;
        private float _converged;

        private readonly float[] _level = new float[4];
        private readonly float[] _peak = new float[4];
        private float _bassAverage;
        private float _rawBass;
        private float _kickAverage;     // raw bass at recent kicks, for Surge
        private float _surgeNow;
        private float _quietFor;
        private float _lastKick;
        private readonly List<float> _kicks = new List<float>();
        private readonly List<float> _gaps = new List<float>();
        private float _tempo;
        private float[] _dayVolume;
        private float _beat;           // beats counted, fractional
        private float _kickGlow;       // 1 on a kick, fading
        private float _night;

        private static readonly int LEVELS = Shader.PropertyToID("Show_Levels");
        private static readonly int BEAT = Shader.PropertyToID("Show_Beat");
        private static readonly int KICK = Shader.PropertyToID("Show_Kick");
        private static readonly int NIGHT = Shader.PropertyToID("Show_Night");

        [Tooltip("Seconds for the beam to climb from the flower to the dome.")]
        [SerializeField] private float _beamSeconds = 2f;

        [Tooltip("Seconds for night to spread from the beam's tip over the whole dome.")]
        [SerializeField] private float _spreadSeconds = 6f;

        [Tooltip("Seconds for the star to glide to the top of the dome when a second Moonflower joins, and back.")]
        [SerializeField] private float _convergeSeconds = 3f;

        [Tooltip("Sounds of the day that fall silent while this client is inside the dome: the nature ambience.")]
        [SerializeField] private AudioSource[] _quietAtNight = new AudioSource[0];

        [Tooltip("How far from the dome's centre the screen can be heard, as a share of the dome's radius. Beyond it the Garden's grooves stay still.")]
        [SerializeField] private float _earshot = 1.6f;

        [Tooltip("Seconds for a kick's glow to fade, for everything that flashes on the kick.")]
        [SerializeField] private float _kickFade = 0.25f;

        [Tooltip("Below this raw level a band counts as silent, so the adaptive gain does not turn hiss into a show.")]
        [SerializeField] private float _silence = 0.002f;

        [Tooltip("Seconds for the adaptive gain to forget a loud peak.")]
        [SerializeField] private float _peakMemory = 8f;

        [Tooltip("Kicks averaged over for Surge: how much harder the last kick hit than they did.")]
        [SerializeField] private float _surgeMemory = 16f;

        /// <summary>The dome is up already (true: a late joiner's first sight) or gone (false). Not raised when it is raised in view.</summary>
        public static event Action<bool> VisibilityChanged;

        /// <summary>A Moonflower took the dome in view: its beam climbs, then night spreads from the beam's tip.</summary>
        public static event Action<MoonflowerPlant> Raising;

        /// <summary>A bass hit.</summary>
        public static event Action Kick;

        /// <summary>A Moonflower's beam has started climbing from this point (not raised for beams a late joiner finds already up).</summary>
        public static event Action<Vector3> BeamClimbing;

        /// <summary>A second Moonflower has joined: the star begins to glide to the top and the beams to arc.</summary>
        public static event Action Converging;

        /// <summary>A Moonflower's beam is climbing in view; the sound of it is the ShowSounds' business.</summary>
        public static void ReportClimb(MoonflowerPlant plant)
        {
            if (_instance != null && plant != null) BeamClimbing?.Invoke(plant.HeadPosition);
        }

        public static bool IsVisible => _instance != null && _instance._current != null;
        public static MoonflowerPlant Holder => _instance == null ? null : _instance._current;
        public static float BeamSeconds => _instance == null ? 2f : _instance._beamSeconds;
        public static float SpreadSeconds => _instance == null ? 6f : _instance._spreadSeconds;
        public static bool HasDome => _instance != null && _instance._hasDome;
        public static Vector3 DomeCentre => _instance == null ? Vector3.zero : _instance._domeCentre;
        public static float DomeRadius => _instance == null ? 0f : _instance._domeRadius;

        /// <summary>Moonflowers beaming: every grown one while the dome is up.</summary>
        public static int BeamCount => IsVisible ? _instance._grown.Count : 0;

        /// <summary>Where the beams meet: above the holder alone, at the top of the dome when two or more converge. Glides between.</summary>
        public static Vector3 Star => _instance == null ? Vector3.zero : _instance._star;

        /// <summary>0 one straight beam, 1 every beam arcing in to the star at the top. Eases between.</summary>
        public static float Converged => _instance == null ? 0f : _instance._converged;

        /// <summary>The beat in beats per second, from the gaps between recent kicks; eases to 0 when the kicks stop.</summary>
        public static float Tempo => _instance == null ? 0f : _instance._tempo;

        /// <summary>Beats counted since load, fractional: steps with the tempo, creeps slowly with no beat. Its fraction is the beat's phase.</summary>
        public static float Beat => _instance == null ? 0f : _instance._beat;

        /// <summary>How far into night this client is: 0 day, 1 inside a closed dome.</summary>
        public static float Night => _instance == null ? 0f : _instance._night;

        /// <summary>1 on a kick, fading to 0 over a quarter second.</summary>
        public static float KickGlow => _instance == null ? 0f : _instance._kickGlow;

        /// <summary>Whether a point is near enough the Garden's screen to dance to it.</summary>
        public static bool InEarshot(Vector3 point) =>
            HasDome && (point - _instance._domeCentre).sqrMagnitude < Mathf.Pow(_instance._domeRadius * _instance._earshot, 2f);

        /// <summary>The last kick's raw bass over that of the kicks before it: 1 as hard as usual, 2 twice as hard. Not adaptive.</summary>
        public static float Surge => _instance == null ? 0f : _instance._surgeNow;

        /// <summary>Seconds the music has been silent (every band below the silence floor); 0 while it plays.</summary>
        public static float QuietFor => _instance == null ? 0f : _instance._quietFor;

        /// <summary>A band's level, 0 bass to 3 high, 0..1 against the recent loudest. 0 when nothing plays.</summary>
        public static float Level(int band) => _instance == null || band < 0 || band > 3 ? 0f : _instance._level[band];

        /// <summary>Where the dome sits; the dome reports itself.</summary>
        public static void RegisterDome(Vector3 centre, float radius)
        {
            if (_instance == null) return;
            _instance._domeCentre = centre;
            _instance._domeRadius = radius;
            _instance._hasDome = true;
        }

        /// <summary>The point on the dome straight above a position: where a beam from it meets the dome.</summary>
        public static Vector3 ApexAbove(Vector3 from)
        {
            if (!HasDome) return from + Vector3.up * 30f;
            Vector3 c = _instance._domeCentre;
            float r = _instance._domeRadius;
            float dx = from.x - c.x, dz = from.z - c.z;
            float y = Mathf.Sqrt(Mathf.Max(0f, r * r - dx * dx - dz * dz));
            return new Vector3(from.x, c.y + y, from.z);
        }

        /// <summary>A Moonflower has finished growing. seenGrowing: this client watched it grow, rather than joining after.</summary>
        public static void Register(MoonflowerPlant plant, bool seenGrowing)
        {
            if (_instance == null || plant == null || _instance._grown.Contains(plant)) return;
            _instance._grown.Add(plant);
            _instance.Choose(seenGrowing);
        }

        /// <summary>A Moonflower is gone. If it held the dome, the next oldest takes over.</summary>
        public static void Unregister(MoonflowerPlant plant)
        {
            if (_instance == null || !_instance._grown.Remove(plant)) return;
            if (plant == _instance._current) _instance.Choose(false);
        }

        private void Awake()
        {
            _instance = this;
            _dayVolume = new float[_quietAtNight.Length];
            for (int i = 0; i < _quietAtNight.Length; i++) _dayVolume[i] = _quietAtNight[i] == null ? 0f : _quietAtNight[i].volume;
        }

        /// <summary>How far into night this client is (0 day, 1 inside a closed dome); the dome reports it. Hushes the day's sounds.</summary>
        public static void ReportNight(float night)
        {
            if (_instance == null) return;
            _instance._night = night;
            for (int i = 0; i < _instance._quietAtNight.Length; i++)
                if (_instance._quietAtNight[i] != null) _instance._quietAtNight[i].volume = _instance._dayVolume[i] * (1f - night);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            VisibilityChanged = null;
            Raising = null;
            Kick = null;
            BeamClimbing = null;
            Converging = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateStar(dt);
            for (int b = 0; b < 4; b++)
            {
                float raw = ArsBridge.Band(b);
                _peak[b] = Mathf.Max(raw, _peak[b] * Mathf.Exp(-dt / _peakMemory), _silence);
                float target = raw < _silence ? 0f : Mathf.Clamp01(raw / _peak[b]);
                // Quick to rise, slower to fall, so hits land and the light does not flicker.
                float rate = target > _level[b] ? 30f : 6f;
                _level[b] = Mathf.Lerp(_level[b], target, 1f - Mathf.Exp(-dt * rate));
            }

            _bassAverage = Mathf.Lerp(_bassAverage, _level[0], 1f - Mathf.Exp(-dt * 2f));
            _rawBass = ArsBridge.Band(0);
            bool silent = true;
            for (int b = 0; b < 3; b++) if (ArsBridge.Band(b) >= _silence) silent = false;
            _quietFor = silent ? _quietFor + dt : 0f;
            if (_level[0] > 0.5f && _level[0] > _bassAverage + 0.2f && Time.time - _lastKick > 0.22f)
            {
                _lastKick = Time.time;
                // Kick against kick: the gaps between them would make every kick look loud.
                _surgeNow = _kickAverage > 0f ? _rawBass / Mathf.Max(_kickAverage, _silence) : 1f;
                _kickAverage = _kickAverage > 0f ? Mathf.Lerp(_kickAverage, _rawBass, 1f / _surgeMemory) : _rawBass;
                _kicks.Add(Time.time);
                _kickGlow = 1f;
                if (_kicks.Count > 9) _kicks.RemoveAt(0);
                Kick?.Invoke();
            }
            UpdateTempo(dt);

            // Every shader can dance: the levels, the beat, the kick and the night, as globals (Show_*).
            _beat += (_tempo > 0f ? _tempo : 0.25f) * dt;
            _kickGlow = Mathf.MoveTowards(_kickGlow, 0f, dt / Mathf.Max(0.01f, _kickFade));
            Shader.SetGlobalVector(LEVELS, new Vector4(_level[0], _level[1], _level[2], _level[3]));
            Shader.SetGlobalFloat(BEAT, _beat);
            Shader.SetGlobalFloat(KICK, _kickGlow);
            Shader.SetGlobalFloat(NIGHT, _night);
        }

        /// <summary>
        /// The median gap between the last few kicks, folded into 0.3 to 0.8 s (75 to 200 bpm) so a missed or doubled
        /// kick reads as the same tempo. No kick for 3 s: the tempo eases to 0.
        /// </summary>
        private void UpdateTempo(float dt)
        {
            float target = 0f;
            if (_kicks.Count >= 4 && Time.time - _lastKick < 3f)
            {
                _gaps.Clear();
                for (int i = 1; i < _kicks.Count; i++)
                {
                    float gap = _kicks[i] - _kicks[i - 1];
                    if (gap > 2f) continue;
                    while (gap > 0.8f) gap *= 0.5f;
                    while (gap < 0.3f) gap *= 2f;
                    _gaps.Add(gap);
                }
                if (_gaps.Count >= 3)
                {
                    _gaps.Sort();
                    target = 1f / _gaps[_gaps.Count / 2];
                }
            }
            _tempo = target <= 0f
                ? Mathf.MoveTowards(_tempo, 0f, dt * 0.5f)
                : _tempo <= 0f ? target : Mathf.Lerp(_tempo, target, 1f - Mathf.Exp(-dt * 1.5f));
        }

        private void UpdateStar(float dt)
        {
            _grown.RemoveAll(p => p == null);
            if (_current == null) { _starPlaced = false; _converged = 0f; return; }
            bool converging = _grown.Count >= 2;
            Vector3 target = converging && _hasDome ? ApexAbove(_domeCentre) : ApexAbove(_current.HeadPosition);
            if (!_starPlaced)
            {
                // First sight: no glide from wherever it last was.
                _star = target;
                _starVelocity = Vector3.zero;
                _converged = converging ? 1f : 0f;
                _starPlaced = true;
                return;
            }
            if (converging && _converged <= 0f) Converging?.Invoke();
            _star = Vector3.SmoothDamp(_star, target, ref _starVelocity, _convergeSeconds * 0.35f);
            _converged = Mathf.MoveTowards(_converged, converging ? 1f : 0f, dt / Mathf.Max(0.01f, _convergeSeconds));
        }

        private void Choose(bool seenGrowing)
        {
            _grown.RemoveAll(p => p == null);
            MoonflowerPlant next = null;
            foreach (MoonflowerPlant p in _grown)
                if (next == null || p.PlantedAt < next.PlantedAt) next = p;
            if (next == _current) return;

            bool wasVisible = _current != null;
            // A late joiner's first dome is already up: shown, not raised. A takeover, or one grown in view, raises it.
            bool raise = next != null && (_hadCurrent || seenGrowing);
            _current = next;
            if (next != null) _hadCurrent = true;

            Logger.Info($"Choose() '{gameObject.name}' — dome {(next == null ? "down" : $"held by '{next.name}'")}{(raise ? ", raising it" : "")}");
            if (raise) Raising?.Invoke(next);
            else if (wasVisible != (next != null)) VisibilityChanged?.Invoke(next != null);
        }
    }
}
