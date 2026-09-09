using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
    [Serializable]
    public class TutorialAudioEntry
    {
        public string key;
        public AudioClip clip;
    }

    [Serializable]
    public class TutorialProgress
    {
        public bool tutorialDisabled;
        public int currentStep;
        public string[] tooltipsPlayed;
    }

    public class TutorialManager : MonoBehaviour
    {
        public static class Tooltips
        {
            public static class Tutorial
            {
                public const string Welcome = "Welcome";
                public const string Hut = "Hut";
                public const string Planting = "Planting";
                public const string Selling = "Selling";
                public const string SellingBasics = "SellingBasics";
                public const string ExplorationIntro = "ExplorationIntro";
                public const string FlyingOopsMoment = "FlyingOopsMoment";
                public const string FlightControls = "FlightControls";
                public const string TroughShotTechnique = "TroughShotTechnique";
                public const string FlyingWrapUp = "FlyingWrapUp";
            }

            public static class Tips
            {
                public const string Falling = "Falling";
                public const string Climbing = "Climbing";
                public const string Upgrades = "Upgrades";
                public const string Buffing = "Buffing";
                public const string Teams = "Teams";
            }
        }
        private static TutorialManager _instance;

        public static TutorialManager GetInstance()
        {
            if (_instance == null)
                _instance = FindObjectOfType<TutorialManager>();
            return _instance;
        }

        public static event Action<int> OnStepTriggered;
        public static event Action<string> OnTipTriggered;

        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip[] _tutorials = new AudioClip[10];
        [SerializeField] private List<TutorialAudioEntry> _tips = new List<TutorialAudioEntry>();

        private TutorialProgress _progress;
        private Dictionary<string, AudioClip> _tipLookup = new Dictionary<string, AudioClip>();
        private const string STORAGE_KEY = "gag_tutorial_progress";

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            LoadProgress();
        }

        private void Start()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();

            _tipLookup.Clear();
            foreach (var entry in _tips)
                _tipLookup[entry.key] = entry.clip;
        }

        private void OnEnable()
        {
            PlayerManager.LocalPlayerJoined += OnLocalPlayerJoined;
        }

        private void OnDisable()
        {
            PlayerManager.LocalPlayerJoined -= OnLocalPlayerJoined;
        }

        private void OnLocalPlayerJoined(string playerId, string playerName)
        {
            TriggerTooltip(Tooltips.Tutorial.Welcome);
        }

        private void LoadProgress()
        {
            string json = PlayerPrefs.GetString(STORAGE_KEY, "");
            if (string.IsNullOrEmpty(json))
            {
                _progress = new TutorialProgress
                {
                    tutorialDisabled = false,
                    currentStep = 0,
                    tooltipsPlayed = Array.Empty<string>()
                };
            }
            else
            {
                _progress = JsonUtility.FromJson<TutorialProgress>(json);
            }
        }

        private void SaveProgress()
        {
            string json = JsonUtility.ToJson(_progress);
            PlayerPrefs.SetString(STORAGE_KEY, json);
            PlayerPrefs.Save();
        }

        public bool IsTutorialDisabled() => _progress.tutorialDisabled;
        public void SetTutorialDisabled(bool disabled)
        {
            _progress.tutorialDisabled = disabled;
            SaveProgress();
        }

        public int GetCurrentStep() => _progress.currentStep;

        public void TriggerTooltip(string tooltipId)
        {
            if (_progress.tutorialDisabled)
                return;

            if (HasTooltipPlayed(tooltipId))
                return;

            if (!TryGetAudioClip(tooltipId, out AudioClip clip))
                return;

            PlayAudio(clip);
            MarkTooltipPlayed(tooltipId);
            SaveProgress();
            OnStepTriggered?.Invoke(GetStepIndex(tooltipId));
        }

        private bool HasTooltipPlayed(string tooltipId)
        {
            return System.Array.Exists(_progress.tooltipsPlayed, element => element == tooltipId);
        }

        private void MarkTooltipPlayed(string tooltipId)
        {
            System.Array.Resize(ref _progress.tooltipsPlayed, _progress.tooltipsPlayed.Length + 1);
            _progress.tooltipsPlayed[_progress.tooltipsPlayed.Length - 1] = tooltipId;
        }

        private bool TryGetAudioClip(string tooltipId, out AudioClip clip)
        {
            return _tipLookup.TryGetValue(tooltipId, out clip);
        }

        private void PlayAudio(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }

        private int GetStepIndex(string tooltipId)
        {
            return tooltipId switch
            {
                "Welcome" => 0,
                "Hut" => 1,
                "Planting" => 2,
                "Selling" => 3,
                "SellingBasics" => 4,
                "ExplorationIntro" => 5,
                "FlyingOopsMoment" => 6,
                "FlightControls" => 7,
                "TroughShotTechnique" => 8,
                "FlyingWrapUp" => 9,
                _ => -1
            };
        }

        public void SetTooltipAudio(string tooltipId, AudioClip clip)
        {
            if (clip != null)
                _tips[tooltipId] = clip;
        }

        public void ResetProgress()
        {
            _progress = new TutorialProgress
            {
                tutorialDisabled = false,
                currentStep = 0,
                tooltipsPlayed = Array.Empty<string>()
            };
            SaveProgress();
        }
    }
}
