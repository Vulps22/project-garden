using System;
using UnityEngine;

namespace GrowAGarden
{
    public abstract class ARSBehaviour : MonoBehaviour
    {
        [Header("ARS Channel Settings")]
        [Tooltip("Chose the ARS channel, you can configure them in the ARSController")]
        [Range(1, 4)][SerializeField] private int _channel = 1; // We start channel at 1 not 0
        [Tooltip("High smoothness will give you a less reactive smoother output")]
        [Range(0.0f, 0.99f)][SerializeField] private float _smoothness = 0.1f;
        [Tooltip("Multiplication of the channel intensity")]
        [Range(0.0f, 100.0f)][SerializeField] private float _gain = 10.0f;
        [Tooltip("Apply pow function to the intensity, will attenuate or increase low amplitude values")]
        [Range(0.125f, 8.0f)][SerializeField] private float _power = 1.0f;

        private float _channelLastValue = 0.0f;

        private ARSController _ars;

        private void Start()
        {
            _ars = ARSController.Instance;
            if (_ars == null)
            {
                ARSController.ForceInit();
            }
            _ars.OnUpdate += OnUpdate;
        }

        private void OnUpdate()
        {
            _channelLastValue = (_ars.Channels[_channel-1] * (1.0f - _smoothness)) + (_channelLastValue * _smoothness);
            float value = Mathf.Pow(_channelLastValue * _gain, _power);
            OnARSUpdate(Mathf.Clamp01(value));
        }

        // Meant to be overridden by derived classes
        public virtual void OnARSUpdate(float value01)
        { }

        // Cleanup events
        private void OnDestroy()
        {
            if (_ars != null)
            {
                _ars.OnUpdate -= OnUpdate;
            }
        }
    }
}
