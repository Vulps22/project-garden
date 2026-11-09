using System;
using UnityEngine;

namespace Vulps.GrowAGarden.Settings
{
    /// <summary>
    /// The player's own preferences: what their wrists call level, and how much the narrator talks.
    ///
    /// Entirely per-client and never replicated — nobody else is affected by, or can see, any of
    /// it. That is what makes this the cheapest thing in the project to add to: no RPC, no master
    /// decision, no wire id.
    ///
    /// Persisted through StorageBridge, which is the point rather than a nicety: a
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

        /// <summary>Silences the recorded tips (TipNarrator).</summary>
        public bool DisableVoiceTips { get; private set; }

        /// <summary>Whether eating or smoking weed may get this player stoned. On unless they turn it off (World Settings).</summary>
        public bool AllowStonedEffects { get; private set; } = true;

        /// <summary>Whether the Notifier shows toasts to this player. On unless they turn it off (World Settings).</summary>
        public bool ShowNotifications { get; private set; } = true;

        /// <summary>How far this player sees Gardens and islands, in metres (World Settings). See docs/detail.md.</summary>
        public float DrawDistance { get; private set; } = DEFAULT_DRAW_DISTANCE;

        public const float DEFAULT_DRAW_DISTANCE = 1200f;
        public const float MIN_DRAW_DISTANCE = 300f;
        public const float MAX_DRAW_DISTANCE = 2000f;

        private float _saveAt = float.PositiveInfinity;   // a slider drag saves once it stops, not on every step

        /// <summary>Developer only: Somnium's own fly mode back on, over ours. Ignored for anyone but the developer.</summary>
        public bool DevNativeFlight { get; private set; }

        /// <summary>Whether others may feed this player. The feature does not exist yet; World Settings shows it disabled.</summary>
        public bool AllowForceFeeding { get; private set; }

        [Serializable]
        private class Stored
        {
            public float trim;
            public bool disableVoiceTips;
            public bool noStonedEffects;    // inverted so a save from before it existed reads as allowed
            public bool allowForceFeeding;
            public bool devNativeFlight;
            public bool noNotifications;    // inverted so a save from before it existed reads as on
            public float drawDistance;      // 0 in a save from before it existed: the default
        }

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

        public void SetDisableVoiceTips(bool disable)
        {
            DisableVoiceTips = disable;
            Logger.Info($"SetDisableVoiceTips() '{gameObject.name}' — {(disable ? "tips silenced" : "tips on")}");
            Commit();
        }

        public void SetAllowStonedEffects(bool allow)
        {
            AllowStonedEffects = allow;
            Logger.Info($"SetAllowStonedEffects() '{gameObject.name}' — {(allow ? "stoned effects allowed" : "stoned effects off")}");
            Commit();
        }

        public void SetShowNotifications(bool show)
        {
            ShowNotifications = show;
            Logger.Info($"SetShowNotifications() '{gameObject.name}' — notifications {(show ? "on" : "off")}");
            Commit();
        }

        /// <summary>Takes effect at once; saved a second after the last change, so dragging the slider writes once.</summary>
        public void SetDrawDistance(float metres)
        {
            DrawDistance = Mathf.Clamp(Mathf.Round(metres / 100f) * 100f, MIN_DRAW_DISTANCE, MAX_DRAW_DISTANCE);
            _saveAt = Time.unscaledTime + 1f;
            Changed?.Invoke();
        }

        private void Update()
        {
            if (Time.unscaledTime < _saveAt) return;
            _saveAt = float.PositiveInfinity;
            Logger.Info($"Update() '{gameObject.name}' — draw distance now {DrawDistance:F0} m");
            Save();
        }

        public void SetAllowForceFeeding(bool allow)
        {
            AllowForceFeeding = allow;
            Logger.Info($"SetAllowForceFeeding() '{gameObject.name}' — {(allow ? "allowed" : "off")}");
            Commit();
        }

        public void SetDevNativeFlight(bool on)
        {
            DevNativeFlight = on;
            Logger.Info($"SetDevNativeFlight() '{gameObject.name}' — Somnium's fly mode {(on ? "allowed" : "off")}");
            Commit();
        }

        private void Commit()
        {
            Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// StorageBridge swallows and latches its own failures, so nothing here can throw out of
        /// Awake and take the settings object down with it — and with it the trim, silently,
        /// leaving the player fighting their own wrists with no clue why.
        /// </summary>
        private void Load()
        {
            // Logged before the early return, not after. The first version said nothing at all
            // when there was no stored file, so "settings never loaded" and "settings loaded and
            // there was nothing in them" produced identical logs — which is exactly the ambiguity
            // that made a dead panel impossible to diagnose from a log.
            Logger.Info($"Load() '{gameObject.name}' — settings object alive, reading '{StorageKey}'");

            if (!StorageBridge.Exists(StorageKey, gameObject.name))
            {
                Logger.Info($"Load() '{gameObject.name}' — nothing stored yet, using defaults");
                return;
            }

            Stored s = StorageBridge.Read<Stored>(StorageKey, gameObject.name);
            if (s == null) return;

            TrimDegrees = Mathf.Clamp(s.trim, -45f, 45f);
            DisableVoiceTips = s.disableVoiceTips;
            AllowStonedEffects = !s.noStonedEffects;
            AllowForceFeeding = s.allowForceFeeding;
            DevNativeFlight = s.devNativeFlight;
            ShowNotifications = !s.noNotifications;
            DrawDistance = s.drawDistance <= 0f ? DEFAULT_DRAW_DISTANCE : Mathf.Clamp(s.drawDistance, MIN_DRAW_DISTANCE, MAX_DRAW_DISTANCE);

            Logger.Info($"Load() '{gameObject.name}' — trim={TrimDegrees:F1} " +
                        $"disableVoiceTips={DisableVoiceTips} allowStoned={AllowStonedEffects} allowForceFeeding={AllowForceFeeding} notifications={ShowNotifications} drawDistance={DrawDistance:F0}");
        }

        private void Save()
        {
            StorageBridge.Write(StorageKey, new Stored
            {
                trim = TrimDegrees,
                disableVoiceTips = DisableVoiceTips,
                noStonedEffects = !AllowStonedEffects,
                allowForceFeeding = AllowForceFeeding,
                devNativeFlight = DevNativeFlight,
                noNotifications = !ShowNotifications,
                drawDistance = DrawDistance,
            }, gameObject.name);
        }
    }
}
