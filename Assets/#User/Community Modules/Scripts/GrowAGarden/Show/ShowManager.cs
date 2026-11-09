using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vulps.GrowAGarden.Show
{
    /// <summary>
    /// The music the light show dances to, from the screen this client listens to (the nearest; ArsBridge): each ARS band
    /// as a 0..1 level that adapts to the volume, a Kick on each bass hit, the tempo and the beat, set as shader globals
    /// (Show_*). Also the night: how far this client is inside a closed dome, which hushes the day's sounds. Each Garden's
    /// dome is its own ShowStage. Local on every client and sends nothing. See docs/light-show.md.
    /// </summary>
    public class ShowManager : MonoBehaviour
    {
        private static ShowManager _instance;
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

        [Tooltip("How far from the screen the music reaches, in metres. Beyond it the Garden's grooves stay still.")]
        [SerializeField] private float _earshotMetres = 75f;

        [Tooltip("Seconds for a kick's glow to fade, for everything that flashes on the kick.")]
        [SerializeField] private float _kickFade = 0.25f;

        [Tooltip("Below this raw level a band counts as silent, so the adaptive gain does not turn hiss into a show.")]
        [SerializeField] private float _silence = 0.002f;

        [Tooltip("Seconds for the adaptive gain to forget a loud peak.")]
        [SerializeField] private float _peakMemory = 8f;

        [Tooltip("Kicks averaged over for Surge: how much harder the last kick hit than they did.")]
        [SerializeField] private float _surgeMemory = 16f;

        /// <summary>A bass hit.</summary>
        public static event Action Kick;

        public static float BeamSeconds => _instance == null ? 2f : _instance._beamSeconds;
        public static float SpreadSeconds => _instance == null ? 6f : _instance._spreadSeconds;
        public static float ConvergeSeconds => _instance == null ? 3f : _instance._convergeSeconds;

        /// <summary>The beat in beats per second, from the gaps between recent kicks; eases to 0 when the kicks stop.</summary>
        public static float Tempo => _instance == null ? 0f : _instance._tempo;

        /// <summary>Beats counted since load, fractional: steps with the tempo, creeps slowly with no beat. Its fraction is the beat's phase.</summary>
        public static float Beat => _instance == null ? 0f : _instance._beat;

        /// <summary>How far into night this client is: 0 day, 1 inside a closed dome.</summary>
        public static float Night => _instance == null ? 0f : _instance._night;

        /// <summary>1 on a kick, fading to 0 over a quarter second.</summary>
        public static float KickGlow => _instance == null ? 0f : _instance._kickGlow;

        /// <summary>Whether a point is near enough the screen this client listens to (its Garden's, docs/towns.md) to dance to it.</summary>
        public static bool InEarshot(Vector3 point)
        {
            if (_instance == null || !ArsBridge.TryGetScreen(out Vector3 screen)) return false;
            float dx = point.x - screen.x, dz = point.z - screen.z;
            return dx * dx + dz * dz < _instance._earshotMetres * _instance._earshotMetres;
        }

        /// <summary>The last kick's raw bass over that of the kicks before it: 1 as hard as usual, 2 twice as hard. Not adaptive.</summary>
        public static float Surge => _instance == null ? 0f : _instance._surgeNow;

        /// <summary>Seconds the music has been silent (every band below the silence floor); 0 while it plays.</summary>
        public static float QuietFor => _instance == null ? 0f : _instance._quietFor;

        /// <summary>A band's level, 0 bass to 3 high, 0..1 against the recent loudest. 0 when nothing plays.</summary>
        public static float Level(int band) => _instance == null || band < 0 || band > 3 ? 0f : _instance._level[band];

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
            bool wasNight = _instance._night > 0f;
            _instance._night = night;
            if (wasNight != night > 0f) NightChanged?.Invoke(night > 0f);
            for (int i = 0; i < _instance._quietAtNight.Length; i++)
                if (_instance._quietAtNight[i] != null) _instance._quietAtNight[i].volume = _instance._dayVolume[i] * (1f - night);
        }

        /// <summary>Night fell (true) or lifted completely (false) for this client.</summary>
        public static event Action<bool> NightChanged;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            Kick = null;
            NightChanged = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
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
    }
}
