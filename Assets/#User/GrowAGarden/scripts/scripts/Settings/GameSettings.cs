using System;
using UnityEngine;
using SomniumSpace.Bridge;

namespace GrowAGarden
{
    /// <summary>
    /// The player's own preferences: what their wrists call level, and how much the narrator talks.
    ///
    /// Entirely per-client and never replicated — nobody else is affected by, or can see, any of
    /// it. That is what makes this the cheapest thing in the project to add to: no RPC, no master
    /// decision, no wire id.
    ///
    /// Persisted through SomniumBridge.LocalStorage, which is the point rather than a nicety: a
    /// trim you have to re-dial on every join is worse than no trim at all, because you would never
    /// bother, and a comfort setting that forgets you is actively hostile.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameSettings : MonoBehaviour
    {
        private const string StorageKey = "player_settings";

        /// <summary>Raised after any setting changes, so displays and consumers can re-read.</summary>
        public static event Action Changed;

        private static GameSettings _instance;

        public static GameSettings GetInstance()
        {
            if (_instance == null) _instance = FindFirstObjectByType<GameSettings>();
            return _instance;
        }

        /// <summary>
        /// Degrees subtracted from the measured wrist angle before the flight model sees it.
        ///
        /// People hold their wrists differently, and the cost of being wrong is invisible: a player
        /// whose neutral is 12 degrees high commands a gentle climb every frame they believe they
        /// are flying level, so their speed bleeds away and they never work out why. One session's
        /// telemetry had a median wrist pitch of +11.9 with 91% of samples below best-glide speed.
        /// </summary>
        public float TrimDegrees { get; private set; }

        /// <summary>Silences the linear tutorial — the numbered script steps.</summary>
        public bool SkipTutorial { get; private set; }

        /// <summary>Silences the context tips, which are not part of the linear script.</summary>
        public bool DisableVoiceTips { get; private set; }

        [Serializable]
        private class Stored
        {
            public float trim;
            public bool skipTutorial;
            public bool disableVoiceTips;
        }

        /// <summary>Set once LocalStorage has thrown, so it is not asked again this session.</summary>
        private bool _storageFailed;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            Load();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            Changed = null;
        }

        public void SetTrim(float degrees)
        {
            // A trim near the stall angle would mean level hands command a dive steeper than the
            // model can recover from — the 55-degree scene value did exactly that for two evenings.
            TrimDegrees = Mathf.Clamp(degrees, -45f, 45f);
            Logger.Info($"SetTrim() '{gameObject.name}' — trim now {TrimDegrees:F1} degrees");
            Commit();
        }

        public void SetSkipTutorial(bool skip)
        {
            SkipTutorial = skip;
            Logger.Info($"SetSkipTutorial() '{gameObject.name}' — {(skip ? "tutorial silenced" : "tutorial on")}");
            Commit();
        }

        public void SetDisableVoiceTips(bool disable)
        {
            DisableVoiceTips = disable;
            Logger.Info($"SetDisableVoiceTips() '{gameObject.name}' — {(disable ? "tips silenced" : "tips on")}");
            Commit();
        }

        private void Commit()
        {
            Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// Wrapped for the same reason TutorialManager's are: the read and write halves of
        /// LocalStorage are not confirmed in-world, and an unguarded throw in Awake would take the
        /// settings object down and with it the trim, silently, leaving the player fighting their
        /// own wrists with no clue why.
        /// </summary>
        private void Load()
        {
            // Logged before the early return, not after. The first version said nothing at all
            // when there was no stored file, so "settings never loaded" and "settings loaded and
            // there was nothing in them" produced identical logs — which is exactly the ambiguity
            // that made a dead panel impossible to diagnose from a log.
            Logger.Info($"Load() '{gameObject.name}' — settings object alive, reading '{StorageKey}'");

            try
            {
                if (!SomniumBridge.LocalStorage.JsonExists(StorageKey))
                {
                    Logger.Info($"Load() '{gameObject.name}' — nothing stored yet, using defaults");
                    return;
                }

                Stored s = SomniumBridge.LocalStorage.ReadJson<Stored>(StorageKey);
                if (s == null) return;

                TrimDegrees = Mathf.Clamp(s.trim, -45f, 45f);
                SkipTutorial = s.skipTutorial;
                DisableVoiceTips = s.disableVoiceTips;

                Logger.Info($"Load() '{gameObject.name}' — trim={TrimDegrees:F1} skipTutorial={SkipTutorial} " +
                            $"disableVoiceTips={DisableVoiceTips}");
            }
            catch (Exception e)
            {
                _storageFailed = true;
                Logger.Error($"Load() '{gameObject.name}' — settings read failed, using defaults and not " +
                             $"saving: {e.GetType().Name}: {e.Message}");
            }
        }

        private void Save()
        {
            if (_storageFailed) return;

            try
            {
                SomniumBridge.LocalStorage.WriteJson(StorageKey, new Stored
                {
                    trim = TrimDegrees,
                    skipTutorial = SkipTutorial,
                    disableVoiceTips = DisableVoiceTips,
                });
            }
            catch (Exception e)
            {
                // Latched: whatever makes the write throw will throw again, and one line per button
                // press is how a client log reaches hundreds of megabytes.
                _storageFailed = true;
                Logger.Error($"Save() '{gameObject.name}' — settings write failed, they will not persist " +
                             $"this session: {e.GetType().Name}: {e.Message}");
            }
        }
    }
}
