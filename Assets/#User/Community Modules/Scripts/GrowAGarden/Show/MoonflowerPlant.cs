using UnityEngine;
using Vulps.GrowAGarden.Crops;

namespace Vulps.GrowAGarden.Show
{
    /// <summary>
    /// The Moonflower's show. A closed bud while it grows; grown, it blooms and offers itself to the ShowManager. While
    /// the dome is up every grown Moonflower beams: alone, a straight beam up to a star on the dome; two or more, the
    /// star glides to the top of the dome and every beam arcs in to meet it (a convergence). The beams swell on the bass,
    /// carry a pulse up to the star on each bass hit, and writhe with the mids. The holder carries the star. Local on
    /// every client, from the plant's derived growth; nothing is sent.
    /// </summary>
    [RequireComponent(typeof(StandingPlant))]
    public class MoonflowerPlant : MonoBehaviour
    {
        [Tooltip("The petals, each pivoting at the flower's base. Tilted outward to open.")]
        [SerializeField] private Transform[] _petals = new Transform[0];

        [Tooltip("Where the beam leaves the flower.")]
        [SerializeField] private Transform _head;

        [Tooltip("The beam: its mesh is rebuilt along the beam's curve every frame.")]
        [SerializeField] private Transform _beam;

        [Tooltip("The star where the beams meet: a unit sphere. Only the holder's shows.")]
        [SerializeField] private Transform _star;

        [Tooltip("Petal tilt in degrees: closed as a bud, and open.")]
        [SerializeField] private Vector2 _petalTilt = new Vector2(-6f, 62f);

        [Tooltip("Seconds for the grown bud to bloom.")]
        [SerializeField] private float _bloomSeconds = 3f;

        [Tooltip("Beam width in metres, quiet and on a bass hit.")]
        [SerializeField] private Vector2 _beamWidth = new Vector2(0.35f, 0.8f);

        [Tooltip("How far the mids bend the beam sideways at its middle, in metres, at full mids.")]
        [SerializeField] private float _writhe = 1.2f;

        [Tooltip("Star size in metres, quiet and on the highs; each beam past the first adds a third.")]
        [SerializeField] private Vector2 _starSize = new Vector2(2.5f, 4.5f);

        private const int RINGS = 32;
        private const int SIDES = 10;
        private static readonly int INTENSITY = Shader.PropertyToID("_Intensity");
        private static readonly int PULSES = Shader.PropertyToID("_Pulses");

        private StandingPlant _plant;
        private Quaternion[] _petalRest;
        private Vector3[] _petalAxis;
        private Renderer _beamRenderer, _starRenderer;
        private MaterialPropertyBlock _block;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private bool _seenGrowing;
        private bool _registered;
        private float _grownAt = -1f;
        private float _beamFrom = -1f;   // when the beam started climbing; < 0 not beaming
        private Vector4 _pulses = new Vector4(99f, 99f, 99f, 99f);
        private int _nextPulse;
        private float _phase;

        public long PlantedAt => _plant == null ? 0 : _plant.PlantedAt;

        /// <summary>Where the beam starts, in world space.</summary>
        public Vector3 HeadPosition => _head != null ? _head.position : transform.position;

        private void Awake()
        {
            _plant = GetComponent<StandingPlant>();
            _block = new MaterialPropertyBlock();
            _phase = Random.value * 10f;
            _petalRest = new Quaternion[_petals.Length];
            _petalAxis = new Vector3[_petals.Length];
            for (int i = 0; i < _petals.Length; i++)
            {
                if (_petals[i] == null) continue;
                _petalRest[i] = _petals[i].localRotation;
                // Each petal tilts about the line across its base, so its tip leans out from the centre.
                Vector3 radial = _petals[i].localPosition;
                radial.y = 0f;
                _petalAxis[i] = Vector3.Cross(Vector3.up, radial.sqrMagnitude > 1e-8f ? radial.normalized : Vector3.forward);
            }
            if (_beam != null) BuildBeamMesh();
            if (_star != null) { _starRenderer = _star.GetComponentInChildren<Renderer>(true); _star.gameObject.SetActive(false); }
            ShowManager.Raising += OnRaising;
            ShowManager.VisibilityChanged += OnVisibilityChanged;
            ShowManager.Kick += OnKick;
        }

        private void OnDestroy()
        {
            ShowManager.Raising -= OnRaising;
            ShowManager.VisibilityChanged -= OnVisibilityChanged;
            ShowManager.Kick -= OnKick;
            ShowManager.Unregister(this);
            if (_mesh != null) Destroy(_mesh);
        }

        /// <summary>The dome is raised in view: every grown Moonflower's beam climbs together.</summary>
        private void OnRaising(MoonflowerPlant plant)
        {
            if (!_registered) return;
            _beamFrom = Time.time;
            ShowManager.ReportClimb(this);
        }

        /// <summary>Already up (a late joiner): the beams are simply there. Gone: no beams.</summary>
        private void OnVisibilityChanged(bool visible)
        {
            _beamFrom = visible && _registered ? Time.time - ShowManager.BeamSeconds : -1f;
        }

        private void OnKick()
        {
            _pulses[_nextPulse] = 0f;
            _nextPulse = (_nextPulse + 1) % 4;
        }

        private void Update()
        {
            if (_plant.PlantedAt == 0) return;
            float growth = _plant.GetGrowthCompletion();

            if (growth < 1f) { _seenGrowing = true; SetPetals(0f); return; }
            if (_grownAt < 0f) _grownAt = _seenGrowing ? Time.time : Time.time - _bloomSeconds;
            SetPetals(Mathf.SmoothStep(0f, 1f, (Time.time - _grownAt) / _bloomSeconds));

            if (!_registered)
            {
                _registered = true;
                ShowManager.Register(this, _seenGrowing);
            }

            // Joining a dome that is already up: climb if this client watched it grow, else simply be there.
            if (!ShowManager.IsVisible) _beamFrom = -1f;
            else if (_beamFrom < 0f)
            {
                _beamFrom = _seenGrowing ? Time.time : Time.time - ShowManager.BeamSeconds;
                if (_seenGrowing) ShowManager.ReportClimb(this);
            }

            for (int k = 0; k < 4; k++) _pulses[k] += Time.deltaTime;
            ShowBeam();
            ShowStar();
        }

        private void SetPetals(float open)
        {
            float tilt = Mathf.Lerp(_petalTilt.x, _petalTilt.y, open);
            for (int i = 0; i < _petals.Length; i++)
                if (_petals[i] != null) _petals[i].localRotation = Quaternion.AngleAxis(tilt, _petalAxis[i]) * _petalRest[i];
        }

        private void ShowBeam()
        {
            if (_beam == null) return;
            bool beaming = _beamFrom >= 0f;
            if (_beam.gameObject.activeSelf != beaming) _beam.gameObject.SetActive(beaming);
            if (!beaming) return;

            float climb = Mathf.Clamp01((Time.time - _beamFrom) / Mathf.Max(0.01f, ShowManager.BeamSeconds));
            float bass = ShowManager.Level(0);
            float mid = Mathf.Max(ShowManager.Level(1), ShowManager.Level(2));
            float width = Mathf.Lerp(_beamWidth.x, _beamWidth.y, bass);

            Vector3 p0 = HeadPosition, p3 = ShowManager.Star;
            // Straight: the controls on the line. Converged: rise out of the flower, then curve in to the star.
            Vector3 s1 = Vector3.Lerp(p0, p3, 1f / 3f), s2 = Vector3.Lerp(p0, p3, 2f / 3f);
            Vector3 a1 = p0 + Vector3.up * (p3.y - p0.y) * 0.75f;
            Vector3 a2 = new Vector3(Mathf.Lerp(p3.x, p0.x, 0.55f), p3.y, Mathf.Lerp(p3.z, p0.z, 0.55f));
            float c = Mathf.SmoothStep(0f, 1f, ShowManager.Converged);
            Vector3 p1 = Vector3.Lerp(s1, a1, c), p2 = Vector3.Lerp(s2, a2, c);

            _beam.SetPositionAndRotation(p0, Quaternion.identity);
            for (int r = 0; r <= RINGS; r++)
            {
                float t = climb * r / RINGS;
                Vector3 at = Bezier(p0, p1, p2, p3, t);
                Vector3 tangent = (Bezier(p0, p1, p2, p3, Mathf.Min(1f, t + 0.01f)) - Bezier(p0, p1, p2, p3, Mathf.Max(0f, t - 0.01f))).normalized;
                if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.up;
                Vector3 side = Vector3.Cross(tangent, Mathf.Abs(tangent.y) > 0.95f ? Vector3.forward : Vector3.up).normalized;
                Vector3 across = Vector3.Cross(side, tangent);
                // The mids: a wave rolling up the beam, pinned at both ends.
                float wave = Mathf.Sin(t * Mathf.PI * 3f - Time.time * 4f + _phase) * Mathf.Sin(Mathf.PI * t / Mathf.Max(0.01f, climb));
                at += (side * Mathf.Cos(_phase) + across * Mathf.Sin(_phase)) * wave * _writhe * mid;
                Vector3 local = _beam.InverseTransformPoint(at);
                for (int s = 0; s < SIDES; s++)
                {
                    float angle = s * Mathf.PI * 2f / SIDES;
                    Vector3 normal = side * Mathf.Cos(angle) + across * Mathf.Sin(angle);
                    _vertices[r * (SIDES + 1) + s] = local + _beam.InverseTransformVector(normal * width * 0.5f);
                    _normals[r * (SIDES + 1) + s] = _beam.InverseTransformDirection(normal);
                }
                _vertices[r * (SIDES + 1) + SIDES] = _vertices[r * (SIDES + 1)];
                _normals[r * (SIDES + 1) + SIDES] = _normals[r * (SIDES + 1)];
            }
            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.RecalculateBounds();

            _beamRenderer.GetPropertyBlock(_block);
            _block.SetFloat(INTENSITY, 1.2f + 2.5f * bass);
            _block.SetVector(PULSES, _pulses);
            _beamRenderer.SetPropertyBlock(_block);
        }

        private void ShowStar()
        {
            if (_star == null) return;
            bool lit = ShowManager.Holder == this && _beamFrom >= 0f && Time.time - _beamFrom >= ShowManager.BeamSeconds;
            if (_star.gameObject.activeSelf != lit) _star.gameObject.SetActive(lit);
            if (!lit) return;

            float size = Mathf.Lerp(_starSize.x, _starSize.y, ShowManager.Level(3)) * (1f + (ShowManager.BeamCount - 1) / 3f);
            _star.SetPositionAndRotation(ShowManager.Star, Quaternion.identity);
            Vector3 parent = _star.parent == null ? Vector3.one : _star.parent.lossyScale;
            _star.localScale = new Vector3(size / Mathf.Max(1e-4f, parent.x), size / Mathf.Max(1e-4f, parent.y), size / Mathf.Max(1e-4f, parent.z));
            if (_starRenderer == null) return;
            _starRenderer.GetPropertyBlock(_block);
            _block.SetFloat(INTENSITY, 2f + 4f * ShowManager.Level(3));
            _starRenderer.SetPropertyBlock(_block);
        }

        /// <summary>The beam's own tube, uv.y along it (0 at the flower), rebuilt in place each frame.</summary>
        private void BuildBeamMesh()
        {
            MeshFilter filter = _beam.GetComponentInChildren<MeshFilter>(true);
            _beamRenderer = _beam.GetComponentInChildren<Renderer>(true);
            _mesh = new Mesh { name = "Beam (curved)" };
            _mesh.MarkDynamic();
            int count = (RINGS + 1) * (SIDES + 1);
            _vertices = new Vector3[count];
            _normals = new Vector3[count];
            var uv = new Vector2[count];
            var triangles = new int[RINGS * SIDES * 6];
            for (int r = 0; r <= RINGS; r++)
                for (int s = 0; s <= SIDES; s++) uv[r * (SIDES + 1) + s] = new Vector2(s / (float)SIDES, r / (float)RINGS);
            int i = 0;
            for (int r = 0; r < RINGS; r++)
                for (int s = 0; s < SIDES; s++)
                {
                    int a = r * (SIDES + 1) + s, b = a + SIDES + 1;
                    triangles[i++] = a; triangles[i++] = b; triangles[i++] = a + 1;
                    triangles[i++] = b; triangles[i++] = b + 1; triangles[i++] = a + 1;
                }
            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.uv = uv;
            _mesh.triangles = triangles;
            if (filter != null) filter.sharedMesh = _mesh;
            _beam.localScale = Vector3.one;
            _beam.gameObject.SetActive(false);
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }
    }
}
