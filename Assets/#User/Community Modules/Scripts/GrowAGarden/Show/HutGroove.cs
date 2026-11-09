using UnityEngine;

namespace Vulps.GrowAGarden.Show
{
    /// <summary>
    /// The hut dancing to the Garden's music, under the dome only. Each kick sends a pulse through it (ShowLit's pieces):
    /// whole ahead of the front and behind it, broken into glowing cubes where it passes. Pulses run up, then down, in
    /// turn; a kick much louder than usual sweeps sideways; the first kick after a silence bursts out from the middle.
    /// Its windows glow, the fire flares on the bass, the chimney puffs a coloured ring on each kick. Local; nothing is sent.
    /// </summary>
    public class HutGroove : MonoBehaviour
    {
        [Tooltip("The windows, which glow.")]
        [SerializeField] private Renderer _windows;

        [Tooltip("Every part the pulses break (ShowLit, Pieces), the windows among them.")]
        [SerializeField] private Renderer[] _pieces = new Renderer[0];

        [Tooltip("The hut's shape, for where a pulse starts and ends: the log house's collider.")]
        [SerializeField] private Collider _shape;

        [Tooltip("Seconds a pulse takes to cross the hut.")]
        [SerializeField] private float _pulseSeconds = 0.4f;

        [Tooltip("How much of the hut the broken band spans, as a share of the crossing.")]
        [SerializeField] private float _pulseWidth = 0.18f;

        [Tooltip("A kick this many times harder than recent kicks sweeps sideways.")]
        [SerializeField] private float _surge = 1.5f;

        [Tooltip("Seconds of silence after which the next kick bursts out from the middle.")]
        [SerializeField] private float _pause = 1f;

        [Tooltip("The fireplace's light, flared on the bass.")]
        [SerializeField] private FireLightFlicker _fire;

        [Tooltip("The fire's particles, which burn faster on the bass.")]
        [SerializeField] private ParticleSystem[] _flames = new ParticleSystem[0];

        [Tooltip("The chimney's rings: an empty particle system this emits into, one ring a kick.")]
        [SerializeField] private ParticleSystem _rings;

        [Tooltip("The windows' glow when the music is quiet: firelight.")]
        [ColorUsage(false, true)]
        [SerializeField] private Color _warm = new Color(1f, 0.55f, 0.2f);

        [Tooltip("Puffs in a ring.")]
        [SerializeField] private int _ringPuffs = 28;

        [Tooltip("How fast a ring spreads, and rises, in metres per second.")]
        [SerializeField] private Vector2 _ringSpeed = new Vector2(1.4f, 1.3f);

        private static readonly int EMISSION = Shader.PropertyToID("_EmissionColor");
        private static readonly int WAVE = Shader.PropertyToID("_Wave");
        private static readonly int WAVE_FROM = Shader.PropertyToID("_WaveFrom");
        private static readonly int WAVE_TO = Shader.PropertyToID("_WaveTo");

        private enum Pulse { Up, Down, Sideways, Burst }

        private MaterialPropertyBlock _block;
        private float[] _flameRate;
        private bool _moving;
        private float _pulseStart = -10f;
        private bool _ring;
        private float _strength;
        private bool _goUp = true;
        private bool _goLeft;
        private bool _afterPause;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _flameRate = new float[_flames.Length];
            for (int i = 0; i < _flames.Length; i++) if (_flames[i] != null) _flameRate[i] = _flames[i].emission.rateOverTimeMultiplier;
            ShowManager.Kick += OnKick;
            ShowManager.NightChanged += OnNightChanged;
            _garden = GetComponentInParent<Detail.GardenDetail>();
            if (_garden != null) _garden.BubbleChanged += OnBubbleChanged;
            Wake();
        }

        private void OnDestroy()
        {
            ShowManager.Kick -= OnKick;
            ShowManager.NightChanged -= OnNightChanged;
            if (_garden != null) _garden.BubbleChanged -= OnBubbleChanged;
        }

        private Detail.GardenDetail _garden;

        // Night is global, but only the hut of the Garden the player stands in dances: the others rest and do not update.
        private bool Dancing => ShowManager.Night > 0f && (_garden == null || _garden.InBubble);
        private void OnNightChanged(bool night) => Wake();
        private void OnBubbleChanged(bool inside) => Wake();

        private void Wake()
        {
            bool dancing = Dancing;
            if (!dancing && _moving) Rest();
            if (!dancing) _moving = false;
            enabled = dancing;
        }

        private void Update()
        {
            // Only under the dome: night is this client's head inside a closed one.
            float night = ShowManager.Night;
            if (night <= 0f) { if (_moving) Rest(); _moving = false; return; }
            _moving = true;

            float bass = ShowManager.Level(0), mid = Mathf.Max(ShowManager.Level(1), ShowManager.Level(2));
            float kick = ShowManager.KickGlow;
            float music = Mathf.Clamp01(bass + mid);

            // Firelight by day, party colours as the music fills; brighter at night, when windows count.
            Color party = Color.HSVToRGB(Mathf.Repeat(ShowManager.Beat * 0.125f, 1f), 0.8f, 1f);
            Color glow = Color.Lerp(_warm * 0.8f, party * (0.6f + 1.8f * mid), music) * (1f + kick * 0.6f) * night;
            if (_windows != null)
            {
                _windows.GetPropertyBlock(_block);
                _block.SetColor(EMISSION, glow);
                _windows.SetPropertyBlock(_block);
            }
            if (ShowManager.QuietFor > _pause) _afterPause = true;
            SetFront((Time.time - _pulseStart) / Mathf.Max(0.05f, _pulseSeconds));
            if (_fire != null) _fire.Boost = 1f + (bass * 1.2f + kick * 0.8f) * night;
            for (int i = 0; i < _flames.Length; i++)
            {
                if (_flames[i] == null) continue;
                ParticleSystem.EmissionModule emission = _flames[i].emission;
                emission.rateOverTimeMultiplier = _flameRate[i] * (1f + bass * 2f * night);
            }
        }

        // Picks a kick's pulse and where it runs, and starts it.
        private void StartPulse()
        {
            if (_shape == null) return;
            Pulse kind;
            if (_afterPause) kind = Pulse.Burst;
            else if (ShowManager.Surge > _surge) kind = Pulse.Sideways;
            else kind = _goUp ? Pulse.Up : Pulse.Down;
            _afterPause = false;
            if (kind == Pulse.Up || kind == Pulse.Down) _goUp = !_goUp;

            Bounds b = _shape.bounds;
            Vector3 from, to;
            switch (kind)
            {
                case Pulse.Burst:
                    // From the middle of the floor, so it ripples out and up like a dome.
                    from = new Vector3(b.center.x, b.min.y, b.center.z); to = b.max; break;
                case Pulse.Sideways:
                    Vector3 along = b.extents.x > b.extents.z ? new Vector3(b.extents.x, 0f, 0f) : new Vector3(0f, 0f, b.extents.z);
                    _goLeft = !_goLeft;
                    from = _goLeft ? b.center - along : b.center + along; to = _goLeft ? b.center + along : b.center - along; break;
                case Pulse.Up:
                    from = new Vector3(b.center.x, b.min.y, b.center.z); to = new Vector3(b.center.x, b.max.y, b.center.z); break;
                default:
                    from = new Vector3(b.center.x, b.max.y, b.center.z); to = new Vector3(b.center.x, b.min.y, b.center.z); break;
            }
            _ring = kind == Pulse.Burst;
            _strength = kind == Pulse.Up || kind == Pulse.Down ? 1f : 1.4f;
            _pulseStart = Time.time;
            foreach (Renderer part in _pieces)
            {
                if (part == null) continue;
                part.GetPropertyBlock(_block);
                _block.SetVector(WAVE_FROM, from);
                _block.SetVector(WAVE_TO, to);
                part.SetPropertyBlock(_block);
            }
        }

        // Moves the pulse's front: from just before the start (-width) to just past the end, then nothing.
        private void SetFront(float progress)
        {
            float width = _ring ? _pulseWidth * 1.6f : _pulseWidth;
            bool running = progress <= 1f;
            var wave = new Vector4(Mathf.Lerp(-width, 1f + width, progress), width, _ring ? 1f : 0f, running ? _strength : 0f);
            foreach (Renderer part in _pieces)
            {
                if (part == null) continue;
                part.GetPropertyBlock(_block);
                _block.SetVector(WAVE, wave);
                part.SetPropertyBlock(_block);
            }
        }

        private void OnKick()
        {
            if (!enabled || ShowManager.Night < 0.5f) return;
            StartPulse();
            if (_rings == null) return;
            Color colour = Color.HSVToRGB(Random.value, 0.7f, 1f);
            var puff = new ParticleSystem.EmitParams { startColor = colour, applyShapeToPosition = false };
            Vector3 origin = _rings.transform.position;
            for (int k = 0; k < _ringPuffs; k++)
            {
                float a = k * Mathf.PI * 2f / _ringPuffs;
                Vector3 out_ = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                puff.position = origin + out_ * 0.15f;
                puff.velocity = out_ * _ringSpeed.x + Vector3.up * _ringSpeed.y;
                _rings.Emit(puff, 1);
            }
        }

        private void Rest()
        {
            if (_windows != null) { _windows.GetPropertyBlock(_block); _block.SetColor(EMISSION, Color.black); _windows.SetPropertyBlock(_block); }
            _pulseStart = -10f;
            SetFront(2f);
            if (_fire != null) _fire.Boost = 1f;
            for (int i = 0; i < _flames.Length; i++)
            {
                if (_flames[i] == null) continue;
                ParticleSystem.EmissionModule emission = _flames[i].emission;
                emission.rateOverTimeMultiplier = _flameRate[i];
            }
        }
    }
}
