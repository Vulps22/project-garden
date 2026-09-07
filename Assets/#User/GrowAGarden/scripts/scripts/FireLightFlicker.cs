using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Makes a light flicker like an open flame — two octaves of Perlin noise (a fast crackle
    /// riding a slower gust), driving intensity, a touch of range, and an ember/flame colour
    /// shift. Purely decorative: every client runs its own flicker independently, so there is
    /// nothing here worth networking.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FireLightFlicker : MonoBehaviour
    {
        [SerializeField] private Light _light;
        [SerializeField] private float _flickerSpeed = 9f;
        [SerializeField] private float _gustSpeed = 1.3f;
        [SerializeField] private float _minIntensityMultiplier = 0.6f;
        [SerializeField] private float _maxIntensityMultiplier = 1.25f;
        [SerializeField] private float _rangeJitter = 0.08f;
        [SerializeField] private bool _colorShift = true;
        [SerializeField] private Color _emberColor = new Color(0.85f, 0.35f, 0.08f);
        [SerializeField] private Color _flameColor = new Color(1f, 0.78f, 0.4f);

        private float _baseIntensity;
        private float _baseRange;
        private float _seed;

        private void OnValidate()
        {
            if (_light == null) _light = GetComponent<Light>();
        }

        private void Awake()
        {
            if (_light == null) _light = GetComponent<Light>();
            _baseIntensity = _light.intensity;
            _baseRange = _light.range;
            _seed = Random.Range(0f, 1000f);
        }

        private void Update()
        {
            float fast = Mathf.PerlinNoise(_seed, Time.time * _flickerSpeed);
            float slow = Mathf.PerlinNoise(_seed + 100f, Time.time * _gustSpeed);
            float noise = fast * 0.65f + slow * 0.35f;

            float multiplier = Mathf.Lerp(_minIntensityMultiplier, _maxIntensityMultiplier, noise);
            _light.intensity = _baseIntensity * multiplier;
            _light.range = _baseRange * (1f + (noise - 0.5f) * _rangeJitter);

            if (_colorShift) _light.color = Color.Lerp(_emberColor, _flameColor, noise);
        }
    }
}
