using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Trip
{
    /// <summary>
    /// Things in the corner of the eye during the intense trip: seeds and crops that appear off to
    /// one side, and dissolve into a swirl of colour when looked at. There is no eye tracking, so
    /// where the head points stands in for where the eyes look. Local only: nobody else sees them.
    /// See docs/weed.md.
    /// </summary>
    public class TripVisions : MonoBehaviour
    {
        [Tooltip("Shapes a vision can take. Paired by index with the materials below; plain arrays, because arrays of custom serializable types arrive empty from the bundle export.")]
        [SerializeField] private Mesh[] _meshes = new Mesh[0];
        [SerializeField] private Material[] _materials = new Material[0];

        [Tooltip("The swirl a vision dissolves into. Cloned per vision; leave it inactive.")]
        [SerializeField] private ParticleSystem _swirl;

        [Tooltip("Trip strength below which nothing appears. The mild high stays clear.")]
        [SerializeField] private float _minLevel = 0.5f;

        [Tooltip("Seconds between visions: the first at the threshold, the second at full strength.")]
        [SerializeField] private Vector2 _interval = new Vector2(9f, 3f);

        [Tooltip("Degrees off where the head points that a vision appears, from and to.")]
        [SerializeField] private Vector2 _angle = new Vector2(40f, 60f);

        [Tooltip("Degrees off where the head points at which a vision is being looked at, and dissolves.")]
        [SerializeField] private float _lookedAtDegrees = 15f;

        [Tooltip("Metres from the head a vision appears, from and to.")]
        [SerializeField] private Vector2 _distance = new Vector2(2.5f, 4f);

        [Tooltip("Size of a vision, in metres across.")]
        [SerializeField] private Vector2 _size = new Vector2(0.25f, 0.6f);

        [Tooltip("Most visions at once.")]
        [SerializeField] private int _maxVisions = 6;

        [Tooltip("Seconds before an unseen vision fades on its own.")]
        [SerializeField] private float _lifetime = 20f;

        private sealed class Vision
        {
            public Transform Body;
            public float Born;
            public float Size;
            public Vector3 Spin;
            public Vector3 Drift;
        }

        private readonly List<Vision> _visions = new List<Vision>();
        private float _nextVision;

        private void Update()
        {
            Transform head = PlayerManager.LocalPlayerHead;
            float level = TripManager.Level;

            if (head != null && level >= _minLevel && Time.time >= _nextVision && _visions.Count < _maxVisions && _meshes.Length > 0)
            {
                float t = Mathf.InverseLerp(_minLevel, 1f, level);
                _nextVision = Time.time + Mathf.Lerp(_interval.x, _interval.y, t) * Random.Range(0.7f, 1.3f);
                Spawn(head);
            }

            for (int i = _visions.Count - 1; i >= 0; i--)
            {
                Vision v = _visions[i];
                if (v.Body == null) { _visions.RemoveAt(i); continue; }

                float age = Time.time - v.Born;
                float grown = Mathf.Clamp01(age / 1.5f);
                float fading = Mathf.Clamp01((_lifetime - age) / 2f);
                v.Body.localScale = Vector3.one * v.Size * grown * Mathf.Min(1f, fading * 2f);
                v.Body.Rotate(v.Spin * Time.deltaTime, Space.Self);
                v.Body.position += v.Drift * Time.deltaTime;

                bool over = level < _minLevel * 0.5f || age >= _lifetime;
                bool seen = head != null && grown >= 1f && Vector3.Angle(head.forward, v.Body.position - head.position) <= _lookedAtDegrees;
                if (!seen && !over) continue;

                if (seen) Dissolve(v);
                Destroy(v.Body.gameObject);
                _visions.RemoveAt(i);
            }
        }

        private void Spawn(Transform head)
        {
            int pick = Random.Range(0, _meshes.Length);
            if (_meshes[pick] == null) return;

            Vector3 flat = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
            float yaw = Random.Range(_angle.x, _angle.y) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * flat.normalized;
            dir = Quaternion.AngleAxis(Random.Range(-15f, 10f), Vector3.Cross(Vector3.up, dir)) * dir;

            var go = new GameObject("GAG_TripVision");
            go.transform.position = head.position + dir * Random.Range(_distance.x, _distance.y);
            go.transform.rotation = Random.rotation;
            go.AddComponent<MeshFilter>().sharedMesh = _meshes[pick];
            go.AddComponent<MeshRenderer>().sharedMaterial = pick < _materials.Length ? _materials[pick] : null;

            float across = Mathf.Max(0.001f, _meshes[pick].bounds.size.magnitude);
            _visions.Add(new Vision
            {
                Body = go.transform,
                Born = Time.time,
                Size = Random.Range(_size.x, _size.y) / across,
                Spin = Random.insideUnitSphere * 40f,
                Drift = Random.insideUnitSphere * 0.08f,
            });
        }

        /// <summary>Looked at: it comes apart into colour where it floated.</summary>
        private void Dissolve(Vision v)
        {
            if (_swirl == null) return;
            ParticleSystem swirl = Instantiate(_swirl, v.Body.position, Quaternion.identity);
            swirl.gameObject.SetActive(true);
            var main = swirl.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            swirl.Play();
        }

        private void OnDisable()
        {
            foreach (Vision v in _visions) if (v.Body != null) Destroy(v.Body.gameObject);
            _visions.Clear();
        }
    }
}
