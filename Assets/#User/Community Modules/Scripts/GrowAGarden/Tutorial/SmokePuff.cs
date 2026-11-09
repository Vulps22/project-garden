using UnityEngine;

namespace Vulps.GrowAGarden.Tutorial
{
    /// <summary>The tutorial's one smoke particle system: a burst of white smoke anywhere on the platform, sized to what it covers.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class SmokePuff : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _particles;
        [Tooltip("Particles per cubic metre of what the puff covers, before the limits below.")]
        [SerializeField] private float _density = 6f;
        [SerializeField] private int _minimum = 12;
        [SerializeField] private int _maximum = 120;

        private void OnValidate()
        {
            if (_particles == null) _particles = GetComponent<ParticleSystem>();
        }

        /// <summary>A burst filling these world-space bounds.</summary>
        public void Play(Bounds bounds)
        {
            if (_particles == null) return;
            Vector3 size = Vector3.Max(bounds.size, Vector3.one * 0.3f);
            var shape = _particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            transform.position = bounds.center;
            transform.rotation = Quaternion.identity;
            int count = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * size.z * _density), _minimum, _maximum);
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            emit.startSize = Mathf.Clamp(largest * 0.35f, 0.15f, 2.5f);
            _particles.Emit(emit, count);
        }
    }
}
