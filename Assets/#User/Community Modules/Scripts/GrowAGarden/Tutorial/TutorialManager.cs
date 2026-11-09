using System;
using UnityEngine;

namespace Vulps.GrowAGarden.Tutorial
{
    /// <summary>Whether this player has done the tutorial, and whether it is running now. Local only; see docs/tutorial.md.</summary>
    [DisallowMultipleComponent]
    public class TutorialManager : MonoBehaviour
    {
        private const string StorageKey = "tutorial_mk2";

        [Tooltip("Whether finishing the tutorial is saved. TRUE ships; off only to walk it again while building it.")]
        [SerializeField] private bool _rememberProgress = true;

        /// <summary>The tutorial began: the platform takes the player up.</summary>
        public static event Action Started;
        /// <summary>The player asked to leave it early (Settings → World → Skip).</summary>
        public static event Action SkipRequested;
        /// <summary>The tutorial is over, finished or skipped.</summary>
        public static event Action Ended;
        /// <summary>Flight was locked or unlocked for the tutorial.</summary>
        public static event Action FlightLockChanged;

        private static TutorialManager _instance;

        /// <summary>Finished or skipped at least once, ever (or this session, with remembering off).</summary>
        public bool IsDone { get; private set; }
        /// <summary>The player is on the platform now.</summary>
        public bool IsRunning { get; private set; }
        /// <summary>Flight is off while the tutorial runs, until its send-off.</summary>
        public static bool FlightLocked => _instance != null && _instance._flightLocked;

        private bool _flightLocked;

        [Serializable]
        public class Stored
        {
            public bool done;
        }

        public static TutorialManager GetInstance()
        {
            if (_instance == null) _instance = FindFirstObjectByType<TutorialManager>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            Load();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Starts the tutorial unless it is already running.</summary>
        public void Begin()
        {
            if (IsRunning) return;
            IsRunning = true;
            SetFlightLocked(true);
            Logger.Info($"Begin() '{gameObject.name}' — tutorial starting (done before: {IsDone})");
            Started?.Invoke();
        }

        /// <summary>Asks the platform to end the tutorial early.</summary>
        public void Skip()
        {
            if (!IsRunning) return;
            Logger.Info($"Skip() '{gameObject.name}' — skip asked for");
            SkipRequested?.Invoke();
        }

        /// <summary>Runs the tutorial again from the start (Settings → World → Repeat).</summary>
        public void Repeat() => Begin();

        /// <summary>The tutorial is over and the player is back in the world: saved as done.</summary>
        public void Complete(bool skipped)
        {
            if (!IsRunning) return;
            IsRunning = false;
            IsDone = true;
            SetFlightLocked(false);
            Save();
            Logger.Info($"Complete() '{gameObject.name}' — tutorial {(skipped ? "skipped" : "finished")}");
            Ended?.Invoke();
        }

        /// <summary>Locks or unlocks flight while the tutorial runs.</summary>
        public void SetFlightLocked(bool locked)
        {
            if (_flightLocked == locked) return;
            _flightLocked = locked;
            Logger.Info($"SetFlightLocked() '{gameObject.name}' — flight {(locked ? "locked" : "unlocked")}");
            FlightLockChanged?.Invoke();
        }

        private void Load()
        {
            if (!_rememberProgress)
            {
                Logger.Warn($"Load() '{gameObject.name}' — _rememberProgress is OFF: the tutorial runs every session. Test builds only.");
                return;
            }
            if (!StorageBridge.Exists(StorageKey, gameObject.name)) return;
            Stored stored = StorageBridge.Read<Stored>(StorageKey, gameObject.name);
            IsDone = stored != null && stored.done;
            Logger.Info($"Load() '{gameObject.name}' — tutorial done: {IsDone}");
        }

        private void Save()
        {
            if (!_rememberProgress) return;
            StorageBridge.Write(StorageKey, new Stored { done = IsDone }, gameObject.name);
        }
    }
}
