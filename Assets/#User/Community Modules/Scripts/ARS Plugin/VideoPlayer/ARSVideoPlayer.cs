using SomniumSpace.Bridge.Components;
using System;
using UnityEngine;

namespace Vulps.GrowAGarden
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

        private SomniumVideoPlayer _events;   // the player whose playback events say when its AudioSource can be there

        // Looked for when the player says it is ready or playing, not on a timer (docs/principles.md → Games are event driven).
        private void Start()
        {
            if (_videoPlayer == null)
                return;

            _events = _videoPlayer.GetComponentInChildren<SomniumVideoPlayer>();
            if (_events != null) _events.OnEventReceived.AddListener(OnVideoEvent);
            GetAudioSource();
        }

        private void OnDestroy()
        {
            if (_events != null) _events.OnEventReceived.RemoveListener(OnVideoEvent);
        }

        private void OnVideoEvent(SomniumVideoEvent e)
        {
            switch (e)
            {
                case SomniumVideoEvent.Initialized:
                case SomniumVideoEvent.ReadyToPlay:
                case SomniumVideoEvent.FirstFrameReady:
                case SomniumVideoEvent.Started:
                    GetAudioSource();
                    break;
                case SomniumVideoEvent.Closing:
                case SomniumVideoEvent.Error:
                    if (_audioSource == null) GetAudioSource();   // a lost source is looked for again on the next ready
                    break;
            }
        }

        // Find Video Player Audio Source
        private void GetAudioSource()
        {
            if (_audioSource != null || _videoPlayer == null)
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
