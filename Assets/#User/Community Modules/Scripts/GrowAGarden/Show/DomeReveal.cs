using UnityEngine;

namespace Vulps.GrowAGarden.Show
{
    /// <summary>
    /// The black sphere around the Garden, on the GardenIsland prefab. Hidden until a Moonflower holds it; then, once
    /// the beam has reached the dome, night spreads from the beam's tip over the whole sphere, and the light show plays
    /// on it (the ShowDome shader, which reads the ARS spectrum itself; this feeds it the band levels and bass kicks).
    /// With no holder it draws back into the tip. While this client's head is inside it, the sun fades to moonlight (no
    /// daylight shadows under a black sky), the ambient to a moonlit night that pulses with the bass, and the nature
    /// ambience hushes (through the ShowManager); all of it comes back on leaving or when it goes. Local on every client; nothing is sent, and players outside keep their
    /// daylight. docs/moonflower.md.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class DomeReveal : MonoBehaviour
    {
        /// <summary>The sphere's radius in metres.</summary>
        public float Radius => _radius;

        [Tooltip("The sphere's radius in metres; the mesh is a unit sphere, scaled to this.")]
        [SerializeField] private float _radius = 45f;

        [Tooltip("Seconds for night to draw back into the tip when no Moonflower holds the dome.")]
        [SerializeField] private float _retreatSeconds = 3f;

        [Tooltip("Seconds for the light to fade to night on entering the dome, and back on leaving.")]
        [SerializeField] private float _nightSeconds = 1.5f;

        [Tooltip("Ambient light at night, inside the dome: a moonlit night, light enough to garden by.")]
        [SerializeField] private Color _nightAmbient = new Color(0.2f, 0.22f, 0.32f);

        [Tooltip("The sun at night becomes the moon: this share of its day intensity, in this colour, with no shadows.")]
        [SerializeField] private float _moonlight = 0.25f;

        [SerializeField] private Color _moonColour = new Color(0.6f, 0.7f, 1f);

        [Tooltip("How much the bass lifts the night's ambient, in a colour that turns with the beat: the Garden breathing with the music.")]
        [SerializeField] private float _ambientPulse = 0.3f;

        private static readonly int CENTRE = Shader.PropertyToID("_Centre");
        private static readonly int APEX = Shader.PropertyToID("_Apex");
        private static readonly int REVEAL = Shader.PropertyToID("_Reveal");
        private static readonly int BASS = Shader.PropertyToID("_Bass");
        private static readonly int MID = Shader.PropertyToID("_Mid");
        private static readonly int HIGH = Shader.PropertyToID("_High");
        private static readonly int RINGS = Shader.PropertyToID("_Rings");

        private Renderer _renderer;
        private ShowStage _stage;
        private MaterialPropertyBlock _block;
        private float _shown;          // 0 hidden, 1 the whole sphere
        private float _spreadFrom = -1f;
        private Vector3 _apex;
        private Vector4 _rings = new Vector4(99f, 99f, 99f, 99f);
        private int _nextRing;

        private float _night;          // 0 day, 1 night inside the dome
        private bool _dimmed;
        private Light _sun;
        private float _sunIntensity;
        private Color _sunColour;
        private LightShadows _sunShadows;
        private Color _sky, _equator, _ground;

        private float Full => _radius * 2.05f;   // far enough from the apex to cover the whole sphere

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _block = new MaterialPropertyBlock();
            transform.localScale = Vector3.one * _radius / Mathf.Max(1e-4f, transform.parent == null ? 1f : transform.parent.lossyScale.x);
            _renderer.enabled = false;
            _stage = GetComponent<ShowStage>();
            _stage.Raising += OnRaising;
            _stage.VisibilityChanged += OnVisibilityChanged;
            ShowManager.Kick += OnKick;
        }

        private void OnDestroy()
        {
            if (_stage != null) { _stage.Raising -= OnRaising; _stage.VisibilityChanged -= OnVisibilityChanged; }
            ShowManager.Kick -= OnKick;
            SetNight(0f);
        }

        private void OnRaising(MoonflowerPlant plant)
        {
            _shown = 0f;
            _spreadFrom = Time.time + ShowManager.BeamSeconds;
        }

        private void OnVisibilityChanged(bool visible)
        {
            _spreadFrom = -1f;
            if (visible) _shown = 1f;
        }

        private void OnKick()
        {
            _rings[_nextRing] = 0f;
            _nextRing = (_nextRing + 1) % 4;
        }

        private void Update()
        {
            MoonflowerPlant holder = _stage.Holder;
            if (holder != null) _apex = _stage.Star;

            if (holder == null) _shown = Mathf.MoveTowards(_shown, 0f, Time.deltaTime / Mathf.Max(0.01f, _retreatSeconds));
            else if (_spreadFrom >= 0f) _shown = Mathf.Clamp01((Time.time - _spreadFrom) / Mathf.Max(0.01f, ShowManager.SpreadSeconds));

            UpdateNight();
            bool visible = _shown > 0f;
            if (_renderer.enabled != visible) _renderer.enabled = visible;
            if (!visible) return;

            for (int k = 0; k < 4; k++) _rings[k] += Time.deltaTime;

            // Eased so the night starts as a pool at the tip and rolls down the sides.
            float reveal = _shown >= 1f && holder != null ? 1000f : Full * Mathf.SmoothStep(0f, 1f, _shown);
            _renderer.GetPropertyBlock(_block);
            _block.SetVector(CENTRE, transform.position);
            _block.SetVector(APEX, _apex);
            _block.SetFloat(REVEAL, reveal);
            _block.SetFloat(BASS, ShowManager.Level(0));
            _block.SetFloat(MID, Mathf.Max(ShowManager.Level(1), ShowManager.Level(2)));
            _block.SetFloat(HIGH, ShowManager.Level(3));
            _block.SetVector(RINGS, _rings);
            _renderer.SetPropertyBlock(_block);
        }
    
        /// <summary>Night follows the dome's spread, but only while this client's head is inside the sphere.</summary>
        private void UpdateNight()
        {
            Camera head = Camera.main;
            bool inside = head != null && (head.transform.position - transform.position).sqrMagnitude < _radius * _radius;
            float target = inside ? Mathf.SmoothStep(0f, 1f, _shown) : 0f;
            SetNight(Mathf.MoveTowards(_night, target, Time.deltaTime / Mathf.Max(0.01f, _nightSeconds)));
        }

        /// <summary>The day's sun and ambient are saved on the way into night and put back exactly on the way out.</summary>
        private void SetNight(float night)
        {
            if (night != _night || night > 0f) ShowManager.ReportNight(night);
            _night = night;
            if (night <= 0f)
            {
                if (!_dimmed) return;
                if (_sun != null) { _sun.intensity = _sunIntensity; _sun.color = _sunColour; _sun.shadows = _sunShadows; }
                RenderSettings.ambientSkyColor = _sky;
                RenderSettings.ambientEquatorColor = _equator;
                RenderSettings.ambientGroundColor = _ground;
                _dimmed = false;
                return;
            }
            if (!_dimmed)
            {
                _sun = RenderSettings.sun;
                if (_sun != null) { _sunIntensity = _sun.intensity; _sunColour = _sun.color; _sunShadows = _sun.shadows; }
                _sky = RenderSettings.ambientSkyColor;
                _equator = RenderSettings.ambientEquatorColor;
                _ground = RenderSettings.ambientGroundColor;
                _dimmed = true;
            }
            if (_sun != null)
            {
                _sun.intensity = _sunIntensity * Mathf.Lerp(1f, _moonlight, night);
                _sun.color = Color.Lerp(_sunColour, _moonColour, night);
                _sun.shadows = night > 0.5f ? LightShadows.None : _sunShadows;
            }
            // The bass lifts the night, in a colour that turns every eight beats.
            Color pulse = Color.HSVToRGB(Mathf.Repeat(ShowManager.Beat / 8f, 1f), 0.6f, 1f) * (ShowManager.Level(0) * _ambientPulse);
            Color night_ = _nightAmbient + pulse;
            RenderSettings.ambientSkyColor = Color.Lerp(_sky, night_, night);
            RenderSettings.ambientEquatorColor = Color.Lerp(_equator, night_, night);
            RenderSettings.ambientGroundColor = Color.Lerp(_ground, night_ * 0.6f, night);
        }
    }
}
