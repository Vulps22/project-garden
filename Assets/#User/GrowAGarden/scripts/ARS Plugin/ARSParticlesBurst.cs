using UnityEngine;

namespace GrowAGarden
{
    public class ARSParticlesBurst : ARSBehaviour
    {
        [SerializeField] ParticleSystem _particleSystem;
        [Tooltip("Amount of particles to emit at every burst")]
        [SerializeField] int _emitCount = 30;
        [Tooltip("Amplitude at which the burst happen")]
        [SerializeField] float _treshold = 0.25f;
        [Tooltip("To avoid emitting to many particle, delay after every burst")]
        [SerializeField] float _delayBeforeRepete = 0f;

        private float _lastEmissionTime = 0f;

        public override void OnARSUpdate(float value01)
        {
            if (_particleSystem != null)
            {
                if (value01 > _treshold && Time.time - _lastEmissionTime > _delayBeforeRepete)
                {

                    _particleSystem.Emit(_emitCount);
                    _lastEmissionTime = Time.time;
                }
            }
        }

        private void OnValidate()
        {
            if (_particleSystem == null)
            {
                _particleSystem = GetComponent<ParticleSystem>();
            }
        }
    }
}
