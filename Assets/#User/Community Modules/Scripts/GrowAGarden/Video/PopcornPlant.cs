using UnityEngine;
using Vulps.GrowAGarden.Crops;

namespace Vulps.GrowAGarden.Video
{
    /// <summary>
    /// The popcorn plant's show. While it grows, popcorn sprays out of its top, livelier the bigger it gets; grown, it
    /// offers itself to the ScreenManager. When it takes the screen, the spray stops and a stream of popcorn flies to the
    /// screen's building edge, each piece vanishing as it joins: the popcorn builds the screen. A plant waiting its turn
    /// keeps spraying. Local on every client, from the plant's derived growth; nothing is sent.
    /// </summary>
    [RequireComponent(typeof(StandingPlant))]
    public class PopcornPlant : MonoBehaviour
    {
        [Tooltip("Sprays popcorn while growing or waiting. Kept at the top of the popcorn as it grows.")]
        [SerializeField] private ParticleSystem _spray;

        [Tooltip("The stream that builds the screen: no gravity, steered piece by piece to the screen's building edge.")]
        [SerializeField] private ParticleSystem _stream;

        [Tooltip("Height of the top of the body's mesh above its pivot, in the body's own units, so the spray sits on it.")]
        [SerializeField] private float _topOfBody = 0.5f;

        [Tooltip("Popcorn per second while sprouting, and once full grown.")]
        [SerializeField] private Vector2 _sprayRate = new Vector2(2f, 10f);

        [Tooltip("Popcorn per second once grown and waiting for the screen.")]
        [SerializeField] private float _grownSprayRate = 4f;

        [Tooltip("Popcorn per second while building the screen.")]
        [SerializeField] private float _streamRate = 50f;

        [Tooltip("How fast the stream's popcorn flies, in metres per second.")]
        [SerializeField] private float _streamSpeed = 7f;

        private StandingPlant _plant;
        private Transform _body;
        private bool _seenGrowing;
        private bool _registered;
        private float _streamUntil;
        private ScreenManager _screen;
        private ParticleSystem.Particle[] _particles;

        public long PlantedAt => _plant == null ? 0 : _plant.PlantedAt;

        private void Awake()
        {
            _plant = GetComponent<StandingPlant>();
            _body = _plant.Body != null ? _plant.Body : _plant.transform;
        }

        private void OnDestroy()
        {
            if (_screen == null) return;
            _screen.Assembling -= OnAssembling;
            _screen.Unregister(this);
        }

        // The screen of the town this plant stands in, found once it stands somewhere.
        private ScreenManager Screen()
        {
            if (_screen != null || _plant.PlantedAt == 0) return _screen;
            _screen = ScreenManager.For(transform.position);
            if (_screen != null) _screen.Assembling += OnAssembling;
            return _screen;
        }

        private void OnAssembling(PopcornPlant from)
        {
            if (from != this) return;
            _streamUntil = Time.time + Mathf.Max(0.5f, _screen.BuildSeconds - 1.5f);   // the last pieces still have to fly
        }

        private void Update()
        {
            if (_plant.PlantedAt == 0) return;
            float growth = _plant.GetGrowthCompletion();
            Vector3 top = _body.position + _body.up * (_topOfBody * _body.lossyScale.y);
            ScreenManager screen = Screen();
            bool holding = screen != null && screen.Holder == this;

            if (_spray != null)
            {
                _spray.transform.position = top;
                ParticleSystem.EmissionModule spray = _spray.emission;
                spray.rateOverTime = holding ? 0f : growth >= 1f ? _grownSprayRate : Mathf.Lerp(_sprayRate.x, _sprayRate.y, growth);
                if (!_spray.isPlaying) _spray.Play();
            }

            if (_stream != null)
            {
                _stream.transform.position = top;
                ParticleSystem.EmissionModule stream = _stream.emission;
                stream.rateOverTime = Time.time < _streamUntil ? _streamRate : 0f;
                if (!_stream.isPlaying) _stream.Play();
            }

            if (growth < 1f) { _seenGrowing = true; return; }
            if (_registered || screen == null) return;
            _registered = true;
            screen.Register(this, _seenGrowing);
        }

        /// <summary>Steers each piece of the stream to its own place along the screen's building edge; it vanishes on arrival.</summary>
        private void LateUpdate()
        {
            if (_stream == null || _stream.particleCount == 0) return;
            if (_particles == null || _particles.Length < _stream.main.maxParticles) _particles = new ParticleSystem.Particle[_stream.main.maxParticles];
            int count = _stream.GetParticles(_particles);

            Vector3 left = Vector3.zero, right = Vector3.zero;
            bool building = _screen != null && _screen.TryGetBuildEdge(out left, out right);
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle p = _particles[i];
                if (!building) { p.remainingLifetime = Mathf.Min(p.remainingLifetime, 0.3f); _particles[i] = p; continue; }

                // Each piece keeps its own spot along the edge, with a little wander so the stream reads as a cloud.
                float along = (p.randomSeed % 1000) / 999f;
                Vector3 target = Vector3.Lerp(left, right, along);
                Vector3 to = target - p.position;
                float distance = to.magnitude;
                if (distance < 0.15f) { p.remainingLifetime = 0f; _particles[i] = p; continue; }

                float wander = Mathf.Sin(Time.time * 3f + p.randomSeed) * 0.6f;
                Vector3 side = Vector3.Cross(to / distance, Vector3.up) * wander;
                p.velocity = Vector3.Lerp(p.velocity, (to / distance) * _streamSpeed + side, Time.deltaTime * 4f);
                _particles[i] = p;
            }
            _stream.SetParticles(_particles, count);
        }
    }
}
