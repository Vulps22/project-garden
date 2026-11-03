using SomniumSpace.Bridge.Components;
using UnityEngine;

namespace Vulps.GrowAGarden
{
    /// <summary>
    /// The only code that names Community Modules' ARS (audio-reactive system). Feeds one screen's sound to the one
    /// ARSController, which publishes the spectrum to shaders (ARS_Texture1) and splits it into four bands: bass,
    /// low-mid, mid, high. Every Garden has a screen (docs/towns.md); this client listens to the one nearest its head, so the show dances to the music it hears. Somnium attaches each player's AudioSource some time after load,
    /// so ARSVideoPlayer polls for it and this hands it over. Local on every client; nothing is sent. docs/light-show.md.
    /// </summary>
    public class ArsBridge : MonoBehaviour
    {
        [Tooltip("Seconds between checks for which screen is nearest.")]
        [SerializeField] private float _checkSeconds = 1f;

        private static ArsBridge _instance;
        private ARSVideoPlayer[] _screens = new ARSVideoPlayer[0];
        private ARSVideoPlayer _screen;
        private AudioSource _fed;
        private float _nextCheck;
        private float _playingSince = -1f;   // when the screen listened to was first seen playing with no sound for ARS
        private bool _warnedDeaf;

        /// <summary>Whether ARS is hearing a screen yet.</summary>
        public static bool IsListening => _instance != null && _instance._fed != null && ARSController.Instance != null;

        /// <summary>Where the screen this client listens to stands; false before there is one.</summary>
        public static bool TryGetScreen(out Vector3 position)
        {
            position = Vector3.zero;
            if (_instance == null || _instance._screen == null) return false;
            position = _instance._screen.transform.position;
            return true;
        }

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
            _screens = FindObjectsByType<ARSVideoPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (_screens.Length == 0) { Logger.Warn($"Start() '{gameObject.name}' — no ARSVideoPlayer, so ARS hears nothing"); return; }
            foreach (ARSVideoPlayer screen in _screens) screen.OnAudioSourceChanged += OnSourceChanged;
            ARSController.ForceInit();
        }

        private void OnDestroy()
        {
            foreach (ARSVideoPlayer screen in _screens) if (screen != null) screen.OnAudioSourceChanged -= OnSourceChanged;
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + _checkSeconds;
                ARSVideoPlayer nearest = Nearest();
                if (nearest != _screen)
                {
                    _screen = nearest;
                    _playingSince = -1f;
                    _warnedDeaf = false;
                    Logger.Info($"Update() '{gameObject.name}' — listening to the screen '{(_screen == null ? "none" : _screen.transform.parent != null ? _screen.transform.parent.name + "/" + _screen.name : _screen.name)}'");
                }
            }
            // Also catches a source found before Start subscribed.
            if (_screen != null && _screen.AudioSource != null && _screen.AudioSource != _fed) Feed(_screen.AudioSource);
            WarnIfDeaf();
        }

        // Says once, out loud, when the screen being listened to has been playing for a minute and has still given ARS no
        // sound: every light show then idles, and nothing else in the log says why (2026-11-03: a lost reference left all 14
        // screens deaf). Not before it plays, since a screen nobody has started has nothing to give. If the sound turns
        // up after all, says that it was a false alarm.
        private void WarnIfDeaf()
        {
            if (_screen == null) return;
            if (_fed != null)
            {
                if (_warnedDeaf) Logger.Warn($"WarnIfDeaf() '{gameObject.name}' — false alarm: the screen '{_screen.name}' gave ARS its AudioSource after all, {Time.time - _playingSince:0} s after it started playing");
                _warnedDeaf = false;
                _playingSince = -1f;
                return;
            }
            if (_playingSince < 0f)
            {
                SomniumVideoPlayer player = _screen.GetComponent<SomniumVideoPlayer>();
                if (player != null && player.IsPlaying()) _playingSince = Time.time;
                return;
            }
            if (_warnedDeaf || Time.time - _playingSince < 60f) return;
            _warnedDeaf = true;
            Logger.Warn($"WarnIfDeaf() '{gameObject.name}' — the screen '{_screen.name}' has been playing for 60 s and has given ARS no AudioSource; the show will idle. Is its ARSVideoPlayer._videoPlayer set?");
        }

        private ARSVideoPlayer Nearest()
        {
            Camera head = Camera.main;
            if (head == null) return _screen != null ? _screen : _screens.Length > 0 ? _screens[0] : null;
            Vector3 at = head.transform.position;
            ARSVideoPlayer best = null;
            float bestSqr = float.MaxValue;
            foreach (ARSVideoPlayer screen in _screens)
            {
                if (screen == null) continue;
                float sqr = (screen.transform.position - at).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = screen; }
            }
            return best;
        }

        private void OnSourceChanged(AudioSource source)
        {
            if (_screen != null && source == _screen.AudioSource) Feed(source);
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
