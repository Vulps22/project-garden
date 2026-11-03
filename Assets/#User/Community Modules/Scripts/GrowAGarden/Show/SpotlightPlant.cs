using UnityEngine;
using Vulps.GrowAGarden.Crops;

namespace Vulps.GrowAGarden.Show
{
    /// <summary>
    /// The Spotlight Tulip's show. Grown, its cup opens and a stage light rises out of it to half the height of the
    /// Moonflower's beam, then points down at the Garden and pans around it, its brightness on one ARS band. On a beat it
    /// pans with the tempo (one turn every 8 or 16 beats) and flashes on each kick; with no beat it drifts slowly. Each
    /// tulip picks its colour, band, beats per turn and direction from its planting time, so every client agrees on them. Every grown tulip is a
    /// light; there is no holder. Local on every client, from the plant's derived growth; nothing is sent.
    /// </summary>
    [RequireComponent(typeof(StandingPlant))]
    public class SpotlightPlant : MonoBehaviour
    {
        [Tooltip("The cup's petals, each pivoting at the cup's base. Tilted outward to open.")]
        [SerializeField] private Transform[] _petals = new Transform[0];

        [Tooltip("Where the light rises from: the cup.")]
        [SerializeField] private Transform _cup;

        [Tooltip("The light, aimed along its +Z: the lamp model, the Spot Light and the cone are under it.")]
        [SerializeField] private Transform _lamp;

        [Tooltip("The real light on the lamp.")]
        [SerializeField] private Light _light;

        [Tooltip("The visible cone: a unit cone along +Z, apex at its pivot, base radius 1 at z 1.")]
        [SerializeField] private Transform _cone;

        [Tooltip("The lamp's lens, which glows with the light.")]
        [SerializeField] private Renderer _lens;

        [Tooltip("Petal tilt in degrees: closed, and open once the light has gone.")]
        [SerializeField] private Vector2 _petalTilt = new Vector2(0f, 38f);

        [Tooltip("Seconds for the light to rise to its height.")]
        [SerializeField] private float _riseSeconds = 4f;

        [Tooltip("Lamp size while in the cup and once risen.")]
        [SerializeField] private Vector2 _lampScale = new Vector2(0.15f, 1f);

        [Tooltip("The pan's radius about the dome's centre, as a share of the dome's radius.")]
        [SerializeField] private float _panReach = 0.4f;

        [Tooltip("Seconds per turn around the Garden with no beat: the slowest and the fastest a tulip may pick.")]
        [SerializeField] private Vector2 _panSeconds = new Vector2(16f, 30f);

        [Tooltip("Beats per turn around the Garden on a beat; each tulip picks one.")]
        [SerializeField] private int[] _beatsPerTurn = { 8, 16 };

        [Tooltip("Extra brightness on a kick, as a share of the top intensity, fading over the decay.")]
        [SerializeField] private float _kickFlash = 0.8f;

        [Tooltip("Seconds for a kick's flash to fade.")]
        [SerializeField] private float _flashDecay = 0.18f;

        [Tooltip("Light intensity quiet, and at the top of its band.")]
        [SerializeField] private Vector2 _intensity = new Vector2(6f, 30f);

        [Tooltip("Half the cone's angle in degrees; the Spot Light's angle is twice this.")]
        [SerializeField] private float _halfAngle = 9f;

        private static readonly Color[] PALETTE =
        {
            new Color(1f, 0.2f, 0.7f), new Color(0.2f, 0.85f, 1f), new Color(1f, 0.7f, 0.15f),
            new Color(0.5f, 1f, 0.25f), new Color(0.6f, 0.3f, 1f), new Color(1f, 1f, 1f),
        };

        private StandingPlant _plant;
        private Quaternion[] _petalRest;
        private Vector3[] _petalAxis;
        private Renderer _coneRenderer;
        private MaterialPropertyBlock _block;
        private bool _seenGrowing;
        private float _grownAt = -1f;
        private ShowStage _stage;   // the dome of this plant's Garden
        private bool _picked;
        private Color _colour;
        private int _band;
        private float _panAngle, _idleSpeed, _direction;
        private int _beats;
        private float _flash;

        private void Awake()
        {
            _plant = GetComponent<StandingPlant>();
            _block = new MaterialPropertyBlock();
            _petalRest = new Quaternion[_petals.Length];
            _petalAxis = new Vector3[_petals.Length];
            for (int i = 0; i < _petals.Length; i++)
            {
                if (_petals[i] == null) continue;
                _petalRest[i] = _petals[i].localRotation;
                Vector3 radial = _petals[i].localPosition;
                radial.y = 0f;
                _petalAxis[i] = Vector3.Cross(Vector3.up, radial.sqrMagnitude > 1e-8f ? radial.normalized : Vector3.forward);
            }
            if (_cone != null) _coneRenderer = _cone.GetComponentInChildren<Renderer>(true);
            if (_lamp != null) _lamp.gameObject.SetActive(false);
            ShowManager.Kick += OnKick;
        }

        private void OnDestroy() => ShowManager.Kick -= OnKick;

        private void OnKick() => _flash = 1f;

        /// <summary>Colour, band and pan from the planting time, the same on every client.</summary>
        private void Pick()
        {
            var random = new System.Random((int)(_plant.PlantedAt ^ (_plant.PlantedAt >> 32)) ^ transform.position.GetHashCode());
            _colour = PALETTE[random.Next(PALETTE.Length)];
            _band = random.Next(4);
            _panAngle = (float)random.NextDouble() * Mathf.PI * 2f;
            float seconds = Mathf.Lerp(_panSeconds.x, _panSeconds.y, (float)random.NextDouble());
            _idleSpeed = Mathf.PI * 2f / seconds;
            _direction = random.Next(2) == 0 ? 1f : -1f;
            _beats = _beatsPerTurn.Length == 0 ? 8 : _beatsPerTurn[random.Next(_beatsPerTurn.Length)];
            if (_light != null) { _light.color = _colour; _light.spotAngle = _halfAngle * 2f; }
            _picked = true;
        }

        private void Update()
        {
            if (_plant.PlantedAt == 0) return;
            float growth = _plant.GetGrowthCompletion();
            if (growth < 1f) { _seenGrowing = true; SetPetals(0f); return; }

            if (!_picked) Pick();
            if (_grownAt < 0f) _grownAt = _seenGrowing ? Time.time : Time.time - _riseSeconds;
            float rise = Mathf.SmoothStep(0f, 1f, (Time.time - _grownAt) / _riseSeconds);
            SetPetals(Mathf.Clamp01(rise * 3f));
            if (_lamp == null) return;
            if (!_lamp.gameObject.activeSelf) _lamp.gameObject.SetActive(true);

            // Half the height of a Moonflower's beam from here: the dome's height above this plant, halved.
            Vector3 from = _cup != null ? _cup.position : transform.position;
            ShowStage stage = _stage != null ? _stage : _stage = ShowStage.For(transform.position);
            float height = ((stage != null ? stage.ApexAbove(transform.position).y : transform.position.y + 30f) - transform.position.y) * 0.5f;
            Vector3 top = transform.position + Vector3.up * height;
            Vector3 at = Vector3.Lerp(from, top, rise);

            // Panning: a circle about the dome's centre, at this plant's ground height.
            Vector3 centre = stage != null ? stage.Centre : transform.position;
            float reach = (stage != null ? stage.Radius : 20f) * _panReach;
            // With a beat, a turn every _beats beats, so a faster song pans faster; with none, the slow drift.
            float tempo = ShowManager.Tempo;
            float speed = tempo > 0f ? Mathf.Max(_idleSpeed, Mathf.PI * 2f * tempo / Mathf.Max(1, _beats)) : _idleSpeed;
            _panAngle += speed * _direction * Time.deltaTime;
            float angle = _panAngle;
            Vector3 target = new Vector3(centre.x + Mathf.Cos(angle) * reach, transform.position.y, centre.z + Mathf.Sin(angle) * reach);
            // Rising, it looks up; risen, it turns down onto the Garden.
            Vector3 aim = Vector3.Slerp(Vector3.up, (target - at).normalized, Mathf.Clamp01(rise * 1.5f - 0.5f));
            _lamp.SetPositionAndRotation(at, Quaternion.LookRotation(aim, Vector3.forward));
            float scale = Mathf.Lerp(_lampScale.x, _lampScale.y, rise);
            Vector3 parentScale = _lamp.parent == null ? Vector3.one : _lamp.parent.lossyScale;
            _lamp.localScale = new Vector3(scale / Mathf.Max(1e-4f, parentScale.x), scale / Mathf.Max(1e-4f, parentScale.y), scale / Mathf.Max(1e-4f, parentScale.z));

            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime / Mathf.Max(0.01f, _flashDecay));
            float level = Mathf.Clamp01(ShowManager.Level(_band) + _flash * _kickFlash);
            float lit = rise >= 1f ? 1f : rise * 0.3f;
            if (_light != null)
            {
                _light.intensity = Mathf.Lerp(_intensity.x, _intensity.y, level) * lit;
                _light.range = (target - at).magnitude * 1.6f + 5f;
            }

            if (_cone != null)
            {
                float length = (target - at).magnitude;
                float radius = Mathf.Tan(_halfAngle * Mathf.Deg2Rad) * length;
                // The cone is under the lamp, so undo the lamp's scale.
                _cone.localScale = new Vector3(radius, radius, length) / Mathf.Max(1e-4f, scale);
                SetGlow(_coneRenderer, "_Color", _colour, (0.25f + 0.9f * level) * lit);
            }
            SetGlow(_lens, "_EmissionColor", _colour, (1f + 4f * level) * lit);
        }

        private void SetPetals(float open)
        {
            float tilt = Mathf.Lerp(_petalTilt.x, _petalTilt.y, open);
            for (int i = 0; i < _petals.Length; i++)
                if (_petals[i] != null) _petals[i].localRotation = Quaternion.AngleAxis(tilt, _petalAxis[i]) * _petalRest[i];
        }

        private void SetGlow(Renderer r, string property, Color colour, float strength)
        {
            if (r == null) return;
            r.GetPropertyBlock(_block);
            _block.SetColor(property, colour * strength);
            r.SetPropertyBlock(_block);
        }
    }
}
