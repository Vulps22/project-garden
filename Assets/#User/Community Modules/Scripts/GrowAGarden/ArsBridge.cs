using UnityEngine;

namespace Vulps.GrowAGarden
{
    /// <summary>
    /// The only code that names Community Modules' ARS (audio-reactive system). Feeds the Garden screen's sound to the
    /// one ARSController, which publishes the spectrum to shaders (ARS_Texture1) and splits it into four bands: bass,
    /// low-mid, mid, high. Somnium attaches the player's AudioSource some time after load, so ARSVideoPlayer polls for
    /// it and this hands each new one over. Local on every client: each analyses the sound it hears. docs/fun-seeds.md.
    /// </summary>
    public class ArsBridge : MonoBehaviour
    {
        [Tooltip("The ARS bridge on the Garden's video player, which finds the player's AudioSource.")]
        [SerializeField] private ARSVideoPlayer _videoPlayer;

        private static ArsBridge _instance;
        private AudioSource _fed;

        /// <summary>Whether ARS is hearing the screen yet.</summary>
        public static bool IsListening => _instance != null && _instance._fed != null && ARSController.Instance != null;

        /// <summary>One band's raw level (0 bass to 3 high): the band's mean spectrum amplitude, small numbers, not 0..1.</summary>
        public static float Band(int band)
        {
            ARSController ars = ARSController.Instance;
            if (ars == null || band < 0 || band >= ars.Channels.Length) return 0f;
            return ars.Channels[band];
        }

        private void Awake() => _instance = this;

        private void Start()
        {
            if (_videoPlayer == null) _videoPlayer = FindFirstObjectByType<ARSVideoPlayer>();
            if (_videoPlayer == null) { Logger.Warn($"Start() '{gameObject.name}' — no ARSVideoPlayer, so ARS hears nothing"); return; }
            _videoPlayer.OnAudioSourceChanged += Feed;
            ARSController.ForceInit();
        }

        private void OnDestroy()
        {
            if (_videoPlayer != null) _videoPlayer.OnAudioSourceChanged -= Feed;
            if (_instance == this) _instance = null;
        }

        /// <summary>Catches a source found before Start subscribed.</summary>
        private void Update()
        {
            if (_fed == null && _videoPlayer != null && _videoPlayer.AudioSource != null) Feed(_videoPlayer.AudioSource);
        }

        private void Feed(AudioSource source)
        {
            if (source == null || source == _fed) return;
            if (ARSController.Instance == null) ARSController.ForceInit();
            if (ARSController.Instance == null) return;
            ARSController.SetAudioSource(source);
            _fed = source;
            Logger.Info($"Feed() '{gameObject.name}' — ARS now hears '{source.name}'");
        }
    }
}
