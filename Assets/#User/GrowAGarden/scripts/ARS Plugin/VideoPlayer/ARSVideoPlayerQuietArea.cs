using SomniumSpace.Bridge.Components;
using UnityEngine;
using UnityEngine.Video;

namespace GrowAGarden
{
    // ARSVideoPlayerQuietArea is very basic; in the future a more advanced version could be created
    // Do not overlap multiple quiet areas � it will not work as expected
    public class ARSVideoPlayerQuietArea : MonoBehaviour
    {
        [SerializeField] private ARSVideoPlayer _videoPlayer;

        [SerializeField]
        [Range(0f, 1.0f)]
        private float _quietVolume = 0.1f;

        [SerializeField]
        [Range(0.05f, 5.0f)]
        private float _fadeDelay = 0.5f;

        [Header("LowPass Settings")]
        [SerializeField]
        [Range(0f, 20000.0f)]
        private float _lowPassCutoffFrequency = 22000.0f;

        [SerializeField]
        [Range(0f, 10.0f)]
        private float _lowPassResonanceQ = 1.0f;

        private bool _canSaveNormalValues = true;

        private float _normalVolume = 1.0f;
        private float _normalLowPassFrequency = 22000.0f;
        private float _normalLowPassResonanceQ = 1.0f;

        private Vector2 _lerpVolume;
        private Vector2 _lerpLowPassCutoffFrequency;
        private Vector2 _lerpLowPassResonanceQ;

        private float _t0;

        private void OnTriggerEnter(Collider other)
        {
            if (_videoPlayer == null) return;

            SomniumTriggerActionArgs args = new(other.gameObject);
            if (!args.IsPlayer) return;

            if (args.IsLocal)
            {
                if (_canSaveNormalValues) // Protect against quick enter/exit spam
                {
                    _normalVolume = _videoPlayer.AudioSource.volume;
                    _normalLowPassFrequency = _videoPlayer.LowPassFilter.cutoffFrequency;
                    _normalLowPassResonanceQ = _videoPlayer.LowPassFilter.lowpassResonanceQ;
                }

                _lerpVolume = new Vector2(_normalVolume, _quietVolume);
                _lerpLowPassCutoffFrequency = new Vector2(_normalLowPassFrequency, _lowPassCutoffFrequency);
                _lerpLowPassResonanceQ = new Vector2(_normalLowPassResonanceQ, _lowPassResonanceQ);

                _t0 = Time.time;
                _canSaveNormalValues = false;

                CancelInvoke(nameof(LoopLerp));
                InvokeRepeating(nameof(LoopLerp), 0f, Time.fixedDeltaTime);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (_videoPlayer == null) return;

            SomniumTriggerActionArgs args = new(other.gameObject);
            if (!args.IsPlayer) return;

            if (args.IsLocal)
            {
                _lerpVolume = new Vector2(_videoPlayer.AudioSource.volume, _normalVolume);
                _lerpLowPassCutoffFrequency = new Vector2(_videoPlayer.LowPassFilter.cutoffFrequency, _normalLowPassFrequency);
                _lerpLowPassResonanceQ = new Vector2(_videoPlayer.LowPassFilter.lowpassResonanceQ, _normalLowPassResonanceQ);

                _t0 = Time.time;
                _canSaveNormalValues = false;

                CancelInvoke(nameof(LoopLerp));
                InvokeRepeating(nameof(LoopLerp), 0f, Time.fixedDeltaTime);
            }
        }

        private void LoopLerp()
        {
            float timePassed = Time.time - _t0;
            float lerp = Mathf.Clamp01(timePassed / _fadeDelay);

            float currentVolume = Mathf.Lerp(_lerpVolume.x, _lerpVolume.y, lerp);
            _videoPlayer.SomniumVideoPlayer.SetVolume(currentVolume);

            _videoPlayer.LowPassFilter.cutoffFrequency =
                Mathf.Lerp(_lerpLowPassCutoffFrequency.x, _lerpLowPassCutoffFrequency.y, lerp);

            _videoPlayer.LowPassFilter.lowpassResonanceQ =
                Mathf.Lerp(_lerpLowPassResonanceQ.x, _lerpLowPassResonanceQ.y, lerp);

            if (timePassed >= _fadeDelay)
            {
                _canSaveNormalValues = true;
                CancelInvoke(nameof(LoopLerp));
            }
        }
    }
}
