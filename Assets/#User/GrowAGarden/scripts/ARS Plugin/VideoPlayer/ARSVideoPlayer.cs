using SomniumSpace.Bridge.Components;
using System;
using UnityEngine;

namespace GrowAGarden
{
    public class ARSVideoPlayer : MonoBehaviour
    {
        public event Action<AudioSource> OnAudioSourceChanged;

        [HideInInspector] public AudioSource AudioSource => _audioSource;
        [HideInInspector] public AudioLowPassFilter LowPassFilter => _lowPassFilter;
        [HideInInspector] public SomniumVideoPlayer SomniumVideoPlayer => _somniumVideoPlayer;

        [SerializeField] private GameObject _videoPlayer;

        private AudioSource _audioSource; 
        private AudioLowPassFilter _lowPassFilter;
        private SomniumVideoPlayer _somniumVideoPlayer;

        private void Start()
        {
            if (_videoPlayer == null)
                return;

            // We loop in case something go wrong and we lose the audio source
            InvokeRepeating(nameof(GetAudioSource), 1.0f, 1.0f);
        }

        // Find Video Player Audio Source
        private void GetAudioSource()
        {
            if (_audioSource != null)
                return;

            _audioSource = _videoPlayer.GetComponentInChildren<AudioSource>();
            if (_audioSource != null)
            {
                _lowPassFilter = _audioSource.GetComponent<AudioLowPassFilter>();
                if (_lowPassFilter == null)
                {
                    _lowPassFilter = _audioSource.gameObject.AddComponent<AudioLowPassFilter>();
                    _lowPassFilter.cutoffFrequency = 22000.0f;
                    _lowPassFilter.lowpassResonanceQ = 1.0f;
                }
                _somniumVideoPlayer = _videoPlayer.GetComponentInChildren<SomniumVideoPlayer>();

                if(_audioSource != null)
                    OnAudioSourceChanged?.Invoke(_audioSource);
            }
        }
    }
}
