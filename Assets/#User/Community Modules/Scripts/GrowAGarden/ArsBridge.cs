using System;
using System.Collections;
using SomniumSpace.Bridge.Components;
using UnityEngine;

namespace Vulps.GrowAGarden
{
    /// <summary>
    /// The only code that names Community Modules' ARS (audio-reactive system). Feeds one screen's sound to the one
    /// ARSController, which publishes the spectrum to shaders (ARS_Texture1) and splits it into four bands: bass,
    /// low-mid, mid, high. Every Garden has a screen (docs/towns.md); this client listens to the one nearest its head,
    /// picked again whenever the player moves a few metres. Somnium attaches each player's AudioSource some time after
    /// load; ARSVideoPlayer raises it and this hands it over. Local on every client; nothing is sent. docs/light-show.md.
    /// </summary>
    public class ArsBridge : MonoBehaviour
    {
        [Tooltip("How far the player moves, in metres, before the nearest screen is picked again.")]
        [SerializeField] private float _step = 10f;

        private const float DEAF_SECONDS = 60f;

        private static ArsBridge _instance;
        private ARSVideoPlayer[] _screens = new ARSVideoPlayer[0];
        private ARSVideoPlayer _screen;
        private AudioSource _fed;
        private SomniumVideoPlayer _player;   // the listened screen's player, for its playback events
        private Coroutine _deafCheck;
        private float _playingSince = -1f;
        private bool _warnedDeaf;

        /// <summary>This client now listens to another screen (or none).</summary>
        public static event Action ScreenChanged;

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
            PlayerTracker.Watch(PlayerTracker.Grid(_step), OnPlayerMoved);
        }

        private void OnDestroy()
        {
            foreach (ARSVideoPlayer screen in _screens) if (screen != null) screen.OnAudioSourceChanged -= OnSourceChanged;
            PlayerTracker.Unwatch(OnPlayerMoved);
            Listen(null);
            if (_instance == this) _instance = null;
            ScreenChanged = null;
        }

        private void OnPlayerMoved(Vector3 at)
        {
            ARSVideoPlayer nearest = Nearest(at);
            if (nearest != _screen) Listen(nearest);
        }

        /// <summary>Switches to a screen: its sound if it has some already, and its playback events for the deaf warning.</summary>
        private void Listen(ARSVideoPlayer screen)
        {
            if (_player != null) _player.OnEventReceived.RemoveListener(OnVideoEvent);
            StopDeafCheck();
            _screen = screen;
            _player = screen == null ? null : screen.GetComponentInChildren<SomniumVideoPlayer>();
            if (_player != null) _player.OnEventReceived.AddListener(OnVideoEvent);
            if (screen == null) return;
            Logger.Info($"Listen() '{gameObject.name}' — listening to the screen '{(screen.transform.parent != null ? screen.transform.parent.name + "/" + screen.name : screen.name)}'");
            if (screen.AudioSource != null) Feed(screen.AudioSource);
            if (_player != null && _player.IsPlaying()) StartDeafCheck();
            ScreenChanged?.Invoke();
        }

        // Says once, out loud, when the screen being listened to has been playing for a minute and has still given ARS no
        // sound: every light show then idles, and nothing else in the log says why. Timed from the player's own Started
        // event; if the sound turns up after all, says that it was a false alarm.
        private void OnVideoEvent(SomniumVideoEvent e)
        {
            switch (e)
            {
                case SomniumVideoEvent.Started:
                case SomniumVideoEvent.Unpaused:
                    StartDeafCheck();
                    break;
                case SomniumVideoEvent.Paused:
                case SomniumVideoEvent.FinishedPlaying:
                case SomniumVideoEvent.Closing:
                    StopDeafCheck();
                    break;
            }
        }

        private void StartDeafCheck()
        {
            if (_deafCheck != null || IsFedBy(_screen)) return;
            _playingSince = Time.time;
            _deafCheck = StartCoroutine(WarnIfDeaf());
        }

        private void StopDeafCheck()
        {
            if (_deafCheck != null) StopCoroutine(_deafCheck);
            _deafCheck = null;
        }

        private IEnumerator WarnIfDeaf()
        {
            yield return new WaitForSeconds(DEAF_SECONDS);
            _deafCheck = null;
            if (IsFedBy(_screen)) yield break;
            _warnedDeaf = true;
            Logger.Warn($"WarnIfDeaf() '{gameObject.name}' — the screen '{_screen.name}' has been playing for {DEAF_SECONDS:0} s and has given ARS no AudioSource; the show will idle. Is its ARSVideoPlayer._videoPlayer set?");
        }

        private bool IsFedBy(ARSVideoPlayer screen) => screen != null && _fed != null && screen.AudioSource == _fed;

        private ARSVideoPlayer Nearest(Vector3 at)
        {
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
            StopDeafCheck();
            if (_warnedDeaf) Logger.Warn($"Feed() '{gameObject.name}' — false alarm: the screen gave ARS its AudioSource after all, {Time.time - _playingSince:0} s after it started playing");
            _warnedDeaf = false;
        }
    }
}
