using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
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
        private AudioSource _audioSource;
        private Dictionary<string, AudioClip> _audioClips = new Dictionary<string, AudioClip>();
        private HashSet<string> _played = new HashSet<string>();

        public static TutorialManager GetInstance()
        {
            if (_instance == null)
                _instance = FindObjectOfType<TutorialManager>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
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

        public void TriggerTooltip(string tooltipId)
        {
            if (_played.Contains(tooltipId))
                return;

            if (_audioClips.TryGetValue(tooltipId, out AudioClip clip) && clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip);

            _played.Add(tooltipId);
        }

        public void SetAudio(string tooltipId, AudioClip clip)
        {
            _audioClips[tooltipId] = clip;
        }
    }
}
