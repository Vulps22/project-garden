using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The panel on the hut wall. Four buttons and three labels, wired to GameSettings.
    ///
    /// Trim is captured, not dialled. The number a player needs is one they cannot feel — one
    /// session's telemetry showed a median wrist pitch of +11.9 degrees from someone who believed
    /// they were flying level — so asking them to guess it on a slider would never converge. Hold
    /// your arms out the way you fly and press Set: whatever your wrists are doing right then
    /// becomes level.
    ///
    /// That is not the auto-calibration docs/flight.md rules out. What that did wrong was infer the
    /// moment — it sampled the launch pose, which turned out to be a player holding their hands up
    /// trying to park, and redefined zero from it without anyone agreeing. Here the player chooses
    /// the moment and can see the result.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private SettingsButton _setTrimButton;
        [SerializeField] private SettingsButton _resetTrimButton;
        [SerializeField] private SettingsButton _skipTutorialButton;
        [SerializeField] private SettingsButton _voiceTipsButton;

        [SerializeField] private TMP_Text _trimLabel;
        [SerializeField] private TMP_Text _skipTutorialLabel;
        [SerializeField] private TMP_Text _voiceTipsLabel;

        [SerializeField] private FlightController _flight;

        [Tooltip("Seconds between pressing SET and the panel starting to listen — time to get your " +
                 "arms out of the way of the button and into a comfortable flying position.")]
        [SerializeField] private int _countdownSeconds = 5;

        [Tooltip("Seconds spent measuring. The median across the whole window becomes the trim, so " +
                 "a wobble or a moment's drift does not decide it.")]
        [SerializeField] private int _measureSeconds = 5;

        private Coroutine _capture;

        private void OnValidate()
        {
            if (_flight == null) _flight = FindFirstObjectByType<FlightController>();
        }

        private void Awake()
        {
            if (_flight == null) _flight = FindFirstObjectByType<FlightController>();
        }

        private void OnEnable()
        {
            if (_setTrimButton != null)      _setTrimButton.Pressed += OnSetTrim;
            if (_resetTrimButton != null)    _resetTrimButton.Pressed += OnResetTrim;
            if (_skipTutorialButton != null) _skipTutorialButton.Pressed += OnToggleSkipTutorial;
            if (_voiceTipsButton != null)    _voiceTipsButton.Pressed += OnToggleVoiceTips;

            GameSettings.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_setTrimButton != null)      _setTrimButton.Pressed -= OnSetTrim;
            if (_resetTrimButton != null)    _resetTrimButton.Pressed -= OnResetTrim;
            if (_skipTutorialButton != null) _skipTutorialButton.Pressed -= OnToggleSkipTutorial;
            if (_voiceTipsButton != null)    _voiceTipsButton.Pressed -= OnToggleVoiceTips;

            GameSettings.Changed -= Refresh;
        }

        private void OnSetTrim()
        {
            // A second press during a capture cancels it. The player has walked away, or realised
            // they were not ready, and the alternative is standing there waiting for a number they
            // already know is wrong.
            if (_capture != null)
            {
                StopCoroutine(_capture);
                _capture = null;
                Logger.Info($"OnSetTrim() '{gameObject.name}' — capture cancelled");
                Refresh();
                return;
            }

            if (_flight == null)
            {
                Logger.Warn($"OnSetTrim() '{gameObject.name}' — no FlightController, cannot read the wrists");
                return;
            }

            _capture = StartCoroutine(CaptureTrim());
        }

        /// <summary>
        /// Count the player into position, then measure for a few seconds and take the median.
        ///
        /// The countdown is the whole point of the design and not politeness: the hands that press
        /// this button are the hands being measured, so there has to be a gap between the press and
        /// the reading or the panel only ever learns what "reaching for a button" looks like.
        ///
        /// Measured over a window rather than sampled once because a held pose drifts and shakes;
        /// the median of the window is steady where a single frame is whatever the tracking said at
        /// that instant.
        ///
        /// This is a *deliberate* capture, which is what separates it from the auto-calibration
        /// docs/flight.md rules out. That one inferred the moment — it sampled whatever pose the
        /// player happened to launch in, and silently redefined level from it. Here the player asks
        /// for it, is told when it is listening, and sees the number it arrived at.
        /// </summary>
        private IEnumerator CaptureTrim()
        {
            for (int i = _countdownSeconds; i > 0; i--)
            {
                SetTrimLabel($"HOLD POSITION\n{i}");
                yield return new WaitForSeconds(1f);
            }

            var samples = new List<float>();
            float elapsed = 0f;
            int shown = 0;

            while (elapsed < _measureSeconds)
            {
                if (_flight.TryGetWristPitch(out float pitch)) samples.Add(pitch);

                elapsed += Time.deltaTime;
                int second = Mathf.Min(_measureSeconds, Mathf.FloorToInt(elapsed) + 1);
                if (second != shown)
                {
                    shown = second;
                    SetTrimLabel($"MEASURING\n{second}");
                }
                yield return null;
            }

            _capture = null;

            if (samples.Count < 10)
            {
                // Refusing beats storing a zero: zero is a real, valid trim meaning "my wrists are
                // level", so it must never double as "I could not read them".
                Logger.Warn($"OnSetTrim() '{gameObject.name}' — only {samples.Count} samples, trim unchanged");
                SetTrimLabel("NO READING");
                yield return new WaitForSeconds(2f);
                Refresh();
                yield break;
            }

            samples.Sort();
            float median = samples[samples.Count / 2];

            Logger.Info($"CaptureTrim() '{gameObject.name}' — {samples.Count} samples over {_measureSeconds}s, " +
                        $"median {median:F1} degrees (min {samples[0]:F1}, max {samples[samples.Count - 1]:F1})");

            GameSettings.GetInstance()?.SetTrim(median);
            Refresh();
        }

        private void SetTrimLabel(string text)
        {
            if (_trimLabel != null) _trimLabel.text = text;
        }

        private void OnResetTrim()
        {
            GameSettings.GetInstance()?.SetTrim(0f);
        }

        private void OnToggleSkipTutorial()
        {
            GameSettings s = GameSettings.GetInstance();
            if (s == null) return;
            s.SetSkipTutorial(!s.SkipTutorial);
        }

        private void OnToggleVoiceTips()
        {
            GameSettings s = GameSettings.GetInstance();
            if (s == null) return;
            s.SetDisableVoiceTips(!s.DisableVoiceTips);
        }

        private void Refresh()
        {
            GameSettings s = GameSettings.GetInstance();
            if (s == null) return;

            // Signed, because which way it leans is the useful part: a player comparing "+12" with
            // how their arms feel learns something a bare "12" does not tell them.
            if (_trimLabel != null)
                _trimLabel.text = $"Trim  {s.TrimDegrees:+0.0;-0.0;0.0}°";

            if (_skipTutorialLabel != null)
                _skipTutorialLabel.text = s.SkipTutorial ? "Tutorial  OFF" : "Tutorial  ON";

            if (_voiceTipsLabel != null)
                _voiceTipsLabel.text = s.DisableVoiceTips ? "Voice Tips  OFF" : "Voice Tips  ON";
        }
    }
}
