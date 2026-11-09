using System;
using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.Settings;

namespace Vulps.GrowAGarden.Tutorial
{
    /// <summary>Recorded tips: each plays once ever, the first time the player meets what it describes. Local only; see docs/tutorial.md.</summary>
    [DisallowMultipleComponent]
    public class TipNarrator : MonoBehaviour
    {
        public static class Tips
        {
            public const string Falling = "Falling";
            public const string Climbing = "Climbing";
            public const string Upgrades = "Upgrades";
            public const string Buffing = "Buffing";
            public const string Teams = "Teams";
        }

        private static readonly Dictionary<string, string> ClipNames = new Dictionary<string, string>
        {
            { Tips.Falling,  "tip_falling"    },
            { Tips.Climbing, "tip_climbing"   },
            { Tips.Upgrades, "tip_upgrades"   },
            { Tips.Buffing,  "tip_buffing"    },
            { Tips.Teams,    "tip_teams"      },
        };

        private const string StorageKey = "tutorial_progress";

        [Tooltip("Where the tips come out. Auto-filled from this GameObject.")]
        [SerializeField] private AudioSource _audioSource;
        [Tooltip("Every tip clip, in any order: matched to its tip by asset name.")]
        [SerializeField] private AudioClip[] _clips;
        [Tooltip("Whether heard tips stay heard between sessions. TRUE ships.")]
        [SerializeField] private bool _rememberProgress = true;

        private static TipNarrator _instance;
        private readonly Dictionary<string, AudioClip> _byId = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> _played = new HashSet<string>();

        [Serializable]
        public class TooltipProgress
        {
            public string[] played = new string[0];
        }

        /// <summary>Plays a tip if it has not been heard, tips are on and the tutorial is not running.</summary>
        public static void Play(string tipId)
        {
            if (_instance == null) _instance = FindFirstObjectByType<TipNarrator>();
            if (_instance != null) _instance.Trigger(tipId);
        }

        private void OnValidate()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            _instance = this;
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            IndexClips();
            Load();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void IndexClips()
        {
            if (_clips == null) return;
            foreach (AudioClip clip in _clips)
            {
                if (clip == null) continue;
                foreach (KeyValuePair<string, string> pair in ClipNames)
                    if (pair.Value == clip.name) _byId[pair.Key] = clip;
            }
            foreach (KeyValuePair<string, string> pair in ClipNames)
                if (!_byId.ContainsKey(pair.Key))
                    Logger.Warn($"IndexClips() '{gameObject.name}' — tip '{pair.Key}' has no clip (expected '{pair.Value}')");
        }

        private void Trigger(string tipId)
        {
            if (string.IsNullOrEmpty(tipId) || _played.Contains(tipId)) return;
            GameSettings settings = GameSettings.GetInstance();
            if (settings != null && settings.DisableVoiceTips) return;
            TutorialManager tutorial = TutorialManager.GetInstance();
            if (tutorial != null && tutorial.IsRunning) { Logger.Log($"Trigger() '{gameObject.name}' — '{tipId}' held back: the tutorial is running"); return; }
            if (!_byId.TryGetValue(tipId, out AudioClip clip) || clip == null || _audioSource == null)
            {
                Logger.Warn($"Trigger() '{gameObject.name}' — no clip or AudioSource for '{tipId}', not marking it heard");
                return;
            }
            if (_audioSource.isPlaying) return;
            _played.Add(tipId);
            Save();
            _audioSource.clip = clip;
            _audioSource.Play();
            Logger.Info($"Trigger() '{gameObject.name}' — tip '{tipId}' playing ({clip.length:F1}s)");
        }

        private void Load()
        {
            if (!_rememberProgress) { Logger.Warn($"Load() '{gameObject.name}' — _rememberProgress is OFF. Test builds only."); return; }
            if (!StorageBridge.Exists(StorageKey, gameObject.name)) return;
            TooltipProgress progress = StorageBridge.Read<TooltipProgress>(StorageKey, gameObject.name);
            if (progress?.played == null) return;
            foreach (string id in progress.played) if (!string.IsNullOrEmpty(id)) _played.Add(id);
        }

        private void Save()
        {
            if (!_rememberProgress) return;
            StorageBridge.Write(StorageKey, new TooltipProgress { played = new List<string>(_played).ToArray() }, gameObject.name);
        }
    }
}
