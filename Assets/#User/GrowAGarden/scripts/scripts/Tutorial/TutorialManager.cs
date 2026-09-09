using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
    [Serializable]
    public class TutorialProgress
    {
        public bool tutorialDisabled;
        public int currentStep;
        public int[] stepsPlayed;
        public string[] tipsPlayed;
    }

    public class TutorialManager : MonoBehaviour
    {
        public static class Tutorials
        {
            public static class Steps
            {
                public const int Welcome = 0;
                public const int Hut = 1;
                public const int Planting = 2;
                public const int Selling = 3;
                public const int SellingBasics = 4;
                public const int ExplorationIntro = 5;
                public const int FlyingOopsMoment = 6;
                public const int FlightControls = 7;
                public const int TroughShotTechnique = 8;
                public const int FlyingWrapUp = 9;
            }

            public static class Tips
            {
                public const string Falling = "falling";
                public const string Climbing = "climbing";
                public const string Upgrades = "upgrades";
                public const string Buffing = "buffing";
                public const string Teams = "teams";
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
        [SerializeField] private Dictionary<string, AudioClip> _tips = new Dictionary<string, AudioClip>();

        private TutorialProgress _progress;
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
                    stepsPlayed = Array.Empty<int>(),
                    tipsPlayed = Array.Empty<string>()
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

        public void TriggerStep(int step)
        {
            if (_progress.tutorialDisabled || step < _progress.currentStep || step >= _tutorials.Length)
                return;

            if (HasStepPlayed(step))
                return;

            PlayTutorialAudio(step);
            MarkStepPlayed(step);
            _progress.currentStep = step + 1;
            SaveProgress();
            OnStepTriggered?.Invoke(step);
        }

        public void TriggerTip(string tipKey)
        {
            if (_progress.tutorialDisabled)
                return;

            if (HasTipPlayed(tipKey))
                return;

            if (!_tips.TryGetValue(tipKey, out AudioClip clip))
                return;

            PlayAudio(clip);
            MarkTipPlayed(tipKey);
            SaveProgress();
            OnTipTriggered?.Invoke(tipKey);
        }

        private bool HasStepPlayed(int step)
        {
            return System.Array.Exists(_progress.stepsPlayed, element => element == step);
        }

        private bool HasTipPlayed(string tipKey)
        {
            return System.Array.Exists(_progress.tipsPlayed, element => element == tipKey);
        }

        private void MarkStepPlayed(int step)
        {
            System.Array.Resize(ref _progress.stepsPlayed, _progress.stepsPlayed.Length + 1);
            _progress.stepsPlayed[_progress.stepsPlayed.Length - 1] = step;
        }

        private void MarkTipPlayed(string tipKey)
        {
            System.Array.Resize(ref _progress.tipsPlayed, _progress.tipsPlayed.Length + 1);
            _progress.tipsPlayed[_progress.tipsPlayed.Length - 1] = tipKey;
        }

        private void PlayTutorialAudio(int step)
        {
            if (step >= _tutorials.Length || _tutorials[step] == null)
                return;

            PlayAudio(_tutorials[step]);
        }

        private void PlayAudio(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }

        public void SetTutorialAudio(int step, AudioClip clip)
        {
            if (step < 0 || step >= _tutorials.Length)
                return;

            _tutorials[step] = clip;
        }

        public void SetTipAudio(string tipKey, AudioClip clip)
        {
            if (clip != null)
                _tips[tipKey] = clip;
        }

        public void ResetProgress()
        {
            _progress = new TutorialProgress
            {
                tutorialDisabled = false,
                currentStep = 0,
                stepsPlayed = Array.Empty<int>(),
                tipsPlayed = Array.Empty<string>()
            };
            SaveProgress();
        }
    }
}
