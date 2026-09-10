using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SomniumSpace.Bridge;

namespace GrowAGarden
{
    /// <summary>
    /// The narrator. Plays one recorded line the first time the player reaches the thing it
    /// describes, remembers what it has said between sessions, and never says it twice.
    ///
    /// Entirely local. No RPCs, no master decisions, nothing replicated — every client hears its
    /// own tutorial on its own schedule, which is why none of the networking rules in CLAUDE.md
    /// apply to any of it.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialManager : MonoBehaviour
    {
        /// <summary>
        /// The tooltip ids. Strings rather than an enum because they are persisted by name — a
        /// reordered enum would silently re-play everything the player has already heard, which is
        /// the same class of mistake as renumbering a wire id.
        /// </summary>
        public static class Tooltips
        {
            public static class Tutorial
            {
                /// <summary>
                /// Script §0. Warns that the build is ahead of, or behind, what the tutorial
                /// describes. Sits before Welcome and hands straight over to it.
                /// </summary>
                public const string BetaNotice = "BetaNotice";

                public const string Welcome = "Welcome";
                public const string Hut = "Hut";
                public const string Planting = "Planting";
                public const string Selling = "Selling";
                public const string SellingBasics = "SellingBasics";
                public const string ExplorationIntro = "ExplorationIntro";

                /// <summary>
                /// Script steps 7 and 8 — the oops moment and the flight controls — are one
                /// recording (`tutorial_flight_1`), so they are one tooltip. There was a separate
                /// FlyingOopsMoment id; both pointed at the same clip, which meant whichever fired
                /// second played it again.
                /// </summary>
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

        /// <summary>
        /// The linear progression, in order. A step only plays if the player has reached it:
        /// triggering step 5 while on step 3 plays 5 and leaves 3 and 4 unheard forever, because a
        /// tutorial line delivered out of its context is worse than no line. Tips are not in here
        /// and are not gated.
        /// </summary>
        private static readonly string[] TutorialOrder =
        {
            Tooltips.Tutorial.BetaNotice,
            Tooltips.Tutorial.Welcome,
            Tooltips.Tutorial.Hut,
            Tooltips.Tutorial.Planting,
            Tooltips.Tutorial.Selling,
            Tooltips.Tutorial.SellingBasics,
            Tooltips.Tutorial.ExplorationIntro,
            Tooltips.Tutorial.FlightControls,
            Tooltips.Tutorial.TroughShotTechnique,
            Tooltips.Tutorial.FlyingWrapUp,
        };

        /// <summary>
        /// Tooltip id to the name of its AudioClip asset. Hardcoded for the same reason
        /// SeedDefinition hardcodes prices: one place per line to read and to change, and no
        /// Inspector state that can silently disagree with the code.
        ///
        /// Clips are matched to ids by asset name rather than by array position on purpose. An
        /// ordered array means a mis-drag in the Inspector gives the player the wrong narration
        /// with nothing to notice, and the order is invisible once the scene is saved.
        /// </summary>
        private static readonly Dictionary<string, string> ClipNames = new Dictionary<string, string>
        {
            { Tooltips.Tutorial.BetaNotice,          "tutorial_beta"       },
            { Tooltips.Tutorial.Welcome,             "tutorial_Welcome"    },
            { Tooltips.Tutorial.Hut,                 "tutorial_hut"        },
            { Tooltips.Tutorial.Planting,            "tutorial_planting"   },
            { Tooltips.Tutorial.Selling,             "tutorial_selling"    },
            { Tooltips.Tutorial.SellingBasics,       "tutorial_first_100"  },
            { Tooltips.Tutorial.ExplorationIntro,    "tutorial_first_flight" },
            { Tooltips.Tutorial.FlightControls,      "tutorial_flight_1"   },
            { Tooltips.Tutorial.TroughShotTechnique, "tutorial_flight_2"   },
            { Tooltips.Tutorial.FlyingWrapUp,        "tutorial_final"      },

            { Tooltips.Tips.Falling,  "tip_falling"    },
            { Tooltips.Tips.Climbing, "tip_climbing"   },
            { Tooltips.Tips.Upgrades, "tip_upgrades"   },
            { Tooltips.Tips.Buffing,  "tip_buffing"    },
            { Tooltips.Tips.Teams,    "tutorial_teams" },
        };

        /// <summary>
        /// Tips that must wait for the linear tutorial to finish before they are allowed to speak.
        ///
        /// Both of these describe *falling*, and falling is exactly what happens while the flight
        /// lesson is still being delivered. One session had the 20.6-second falling tip queued
        /// between the flight controls and the trough-shot line, so the player was told what to do
        /// about the world floor in the middle of being taught how to fly at all.
        ///
        /// Not every tip needs this. An upgrade or a buff can be found at any point and the line
        /// still makes sense on its own; these two only make sense once the player knows what
        /// flying is supposed to feel like.
        /// </summary>
        private static readonly HashSet<string> TipsAfterTutorial = new HashSet<string>
        {
            Tooltips.Tips.Falling,
            Tooltips.Tips.Climbing,
        };

        /// <summary>
        /// Whether the script should be running at all: the player turned it off, or there is no
        /// plot for them to follow it with.
        ///
        /// **The world has four plots.** Every line of the tutorial assumes the player is getting
        /// one — the very first one tells them to pick up their deed and carry it into their hut —
        /// and flight is gated behind reaching the exploration intro. So a fifth player, or anyone
        /// arriving when the plots are all taken, would be talked at about a garden they cannot
        /// have and then locked out of flying for the whole session. Suppressing it is the lesser
        /// wrong: they lose the narration, not the game.
        ///
        /// Silent by design. There is no line for "sorry, the world is full", and inventing one
        /// out of a suppression rule would be worse than saying nothing.
        /// </summary>
        public bool TutorialSuppressed
        {
            get
            {
                GameSettings settings = GameSettings.GetInstance();
                if (settings != null && settings.SkipTutorial) return true;
                return NoPlotForPlayer();
            }
        }

        /// <summary>
        /// True when every plot is claimed and none of them is this player's.
        ///
        /// Belonging to one as a teammate counts — they can plant and harvest on it, so the script
        /// still describes something they can actually do.
        ///
        /// Fails open: if the plots cannot be found at all, the tutorial runs. Silently disabling
        /// the narration because a lookup came back empty is the kind of failure that gets reported
        /// as "the tutorial just stopped working" months later.
        /// </summary>
        private bool NoPlotForPlayer()
        {
            if (_plots == null || _plots.Length == 0) return false;

            string localId = PlayerManager.LocalPlayerId;
            bool anyFree = false;

            foreach (PlotLeaseManager plot in _plots)
            {
                if (plot == null) continue;
                if (!plot.IsClaimed) { anyFree = true; continue; }
                if (plot.OwnerId == localId) return false;
                if (localId != null && plot.TeammateIds.Contains(localId)) return false;
            }

            return !anyFree;
        }

        private PlotLeaseManager[] _plots;

        /// <summary>
        /// Whether the linear script is done with — heard to the end, or not running at all.
        ///
        /// Suppression counts as finished on purpose: a player who silenced the tutorial, or who
        /// never had a plot to do it with, has not earned a permanent ban on every tip that waits
        /// behind it.
        /// </summary>
        public bool TutorialFinished =>
            TutorialSuppressed || _played.Contains(TutorialOrder[TutorialOrder.Length - 1]);

        /// <summary>
        /// Lines that follow another line rather than a world event, and how long after it finishes.
        ///
        /// The Hut was a late addition to the script and sits between Welcome and First Thatch:
        /// both are triggered by the same moment — the deed going through the door — so the second
        /// cannot also hang off an event without the two talking over each other. Timing it from
        /// the end of the first is what the script actually describes.
        ///
        /// The delay is measured from the clip *finishing*, not from it starting, so it stays
        /// correct if a recording is ever re-cut to a different length.
        /// </summary>
        private static readonly Dictionary<string, FollowUp> FollowUps = new Dictionary<string, FollowUp>
        {
            // The notice and the welcome are one continuous thought, so the second is timed off
            // the first rather than waiting for the player to do something again — they have
            // already moved, and nothing else is going to happen to re-trigger it.
            { Tooltips.Tutorial.BetaNotice, new FollowUp(Tooltips.Tutorial.Welcome, 1f) },
            { Tooltips.Tutorial.Hut, new FollowUp(Tooltips.Tutorial.Planting, 3f) },
        };

        private readonly struct FollowUp
        {
            public readonly string Next;
            public readonly float Delay;
            public FollowUp(string next, float delay) { Next = next; Delay = delay; }
        }

        private const string StorageKey = "tutorial_progress";

        [Tooltip("Where the narration comes out. Auto-filled from this GameObject by OnValidate.")]
        [SerializeField] private AudioSource _audioSource;

        [Tooltip("Every tutorial and tip clip, in any order — they are matched to their tooltip by " +
                 "asset name, not by position. A plain array because arrays of custom [Serializable] " +
                 "classes arrive empty from the bundle export; see CLAUDE.md.")]
        [SerializeField] private AudioClip[] _clips;

        [Tooltip("Whether what the player has heard survives the session. TRUE is the shipping " +
                 "value — turn it off only to walk the whole tutorial again while building it, and " +
                 "put it back before any upload that is not a test build. Off also ignores what is " +
                 "already stored, so a session starts genuinely clean rather than clean-but-muted.")]
        [SerializeField] private bool _rememberProgress = true;

        private static TutorialManager _instance;

        private readonly Dictionary<string, AudioClip> _byId = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> _played = new HashSet<string>();
        private readonly Queue<QueuedLine> _pending = new Queue<QueuedLine>();

        private readonly struct QueuedLine
        {
            public readonly string Id;
            public readonly AudioClip Clip;
            public QueuedLine(string id, AudioClip clip) { Id = id; Clip = clip; }
        }

        /// <summary>What is coming out of the speaker right now, so its end can be noticed.</summary>
        private string _playingId;

        private string _followUpId;
        private float _followUpAt;

        /// <summary>Set once LocalStorage has thrown, so it is not asked again this session.</summary>
        private bool _storageFailed;

        /// <summary>
        /// The shape LocalStorage round-trips. A flat string array rather than a dictionary or a
        /// set, because JsonUtility serializes neither.
        /// </summary>
        [Serializable]
        public class TooltipProgress
        {
            public string[] played = new string[0];
        }

        public static TutorialManager GetInstance()
        {
            if (_instance == null) _instance = FindFirstObjectByType<TutorialManager>();
            return _instance;
        }

        /// <summary>
        /// Whether this line has already been heard this run — the question anything gated on
        /// tutorial progress needs to ask.
        ///
        /// Note it answers "heard", not "reached": with _rememberProgress off, a fresh session
        /// starts with nothing heard, so gates re-arm along with the narration and the two cannot
        /// disagree about how far the player has got.
        /// </summary>
        public bool HasPlayed(string tooltipId) =>
            !string.IsNullOrEmpty(tooltipId) && _played.Contains(tooltipId);

        private void OnValidate()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                Logger.Error($"Awake() '{gameObject.name}' — no AudioSource; the tutorial will stay silent");

            _plots = FindObjectsByType<PlotLeaseManager>(FindObjectsSortMode.None);

            IndexClips();
            LoadProgress();
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

                bool matched = false;
                foreach (KeyValuePair<string, string> pair in ClipNames)
                {
                    if (pair.Value != clip.name) continue;
                    _byId[pair.Key] = clip;
                    matched = true;
                    break;
                }

                if (!matched)
                    Logger.Warn($"IndexClips() '{gameObject.name}' — clip '{clip.name}' matches no tooltip id");
            }

            foreach (KeyValuePair<string, string> pair in ClipNames)
                if (!_byId.ContainsKey(pair.Key))
                    Logger.Warn($"IndexClips() '{gameObject.name}' — tooltip '{pair.Key}' has no clip " +
                                $"(expected an asset named '{pair.Value}')");
        }

        /// <summary>
        /// Every LocalStorage call is wrapped, because the read and write halves of that API are
        /// not confirmed in-world — docs/flight.md records that the SDK's XML documents only the
        /// housekeeping calls (JsonExists, DeleteJson, GetAllJsonNames...) and not these two. An
        /// unguarded throw here happens inside Awake and takes the whole narrator down with it, so
        /// the failure mode would be total silence with no clue why.
        ///
        /// Forgetting is the right way to fail: a player who hears a line twice has a blemish, a
        /// player who hears nothing has no tutorial.
        /// </summary>
        private void LoadProgress()
        {
            if (!_rememberProgress)
            {
                Logger.Warn($"LoadProgress() '{gameObject.name}' — _rememberProgress is OFF: stored " +
                            $"progress ignored and nothing will be saved. Test builds only.");
                return;
            }

            try
            {
                if (!SomniumBridge.LocalStorage.JsonExists(StorageKey)) return;

                TooltipProgress progress = SomniumBridge.LocalStorage.ReadJson<TooltipProgress>(StorageKey);
                if (progress == null || progress.played == null) return;

                foreach (string tooltipId in progress.played)
                    if (!string.IsNullOrEmpty(tooltipId)) _played.Add(tooltipId);

                Logger.Info($"LoadProgress() '{gameObject.name}' — {_played.Count} tooltips already heard, " +
                            $"resuming at step {NextStep()}");
            }
            catch (Exception e)
            {
                // Start from nothing rather than from half a file. A partial read would leave the
                // step counter somewhere the player has never actually been.
                _played.Clear();
                _storageFailed = true;
                Logger.Error($"LoadProgress() '{gameObject.name}' — LocalStorage read failed, starting " +
                             $"fresh and not saving: {e.GetType().Name}: {e.Message}");
            }
        }

        private void SaveProgress()
        {
            if (!_rememberProgress || _storageFailed) return;

            try
            {
                TooltipProgress progress = new TooltipProgress();
                progress.played = new List<string>(_played).ToArray();
                SomniumBridge.LocalStorage.WriteJson(StorageKey, progress);
            }
            catch (Exception e)
            {
                // Latched, not retried. Whatever makes WriteJson throw will make the next one throw
                // too, and a line per tooltip is how a client log reaches hundreds of megabytes.
                _storageFailed = true;
                Logger.Error($"SaveProgress() '{gameObject.name}' — LocalStorage write failed, progress " +
                             $"will not persist this session: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>
        /// The linear step the player is up to — derived from what they have heard rather than
        /// stored beside it, so there is only one thing to persist and the two can never disagree.
        /// </summary>
        private int NextStep()
        {
            int step = 0;
            for (int i = 0; i < TutorialOrder.Length; i++)
                if (_played.Contains(TutorialOrder[i])) step = i + 1;
            return step;
        }

        /// <summary>
        /// Offer a tooltip. Plays it if the player has not heard it, is far enough along for it to
        /// make sense, and there is a clip for it. Safe to call from anywhere, every frame — it is
        /// a no-op after the first time.
        /// </summary>
        public void TriggerTooltip(string tooltipId)
        {
            if (string.IsNullOrEmpty(tooltipId)) return;
            if (_played.Contains(tooltipId)) return;

            int step = Array.IndexOf(TutorialOrder, tooltipId);

            // Silenced lines are not marked heard. A player who turns the tutorial back on should
            // get it from where they were, not discover that everything they muted is now
            // permanently unplayable — and TutorialOrder is already the only thing that knows which
            // ids are linear steps and which are tips, so nothing new has to be kept in step.
            bool isTutorialStep = step >= 0;

            if (isTutorialStep && TutorialSuppressed)
            {
                Logger.Log($"TriggerTooltip() '{gameObject.name}' — '{tooltipId}' suppressed (tutorial off or no plot free)");
                return;
            }

            GameSettings settings = GameSettings.GetInstance();
            if (!isTutorialStep && settings != null && settings.DisableVoiceTips) return;

            // Not marked heard, so it still gets its moment once the script is done — the player
            // will fall off an island again.
            if (TipsAfterTutorial.Contains(tooltipId) && !TutorialFinished)
            {
                Logger.Log($"TriggerTooltip() '{gameObject.name}' — '{tooltipId}' held back until the tutorial finishes");
                return;
            }

            if (step >= 0 && step < NextStep())
            {
                Logger.Log($"TriggerTooltip() '{gameObject.name}' — '{tooltipId}' is step {step} and the " +
                           $"player is past it; skipped steps stay skipped");
                return;
            }

            if (!_byId.TryGetValue(tooltipId, out AudioClip clip) || clip == null)
            {
                // Deliberately not marked played: a missing clip is a wiring fault, and burning the
                // tooltip would mean the player never hears it even once it is assigned.
                Logger.Warn($"TriggerTooltip() '{gameObject.name}' — no clip for '{tooltipId}', not marking it heard");
                return;
            }

            if (_audioSource == null)
            {
                Logger.Warn($"TriggerTooltip() '{gameObject.name}' — no AudioSource for '{tooltipId}', not marking it heard");
                return;
            }

            _pending.Enqueue(new QueuedLine(tooltipId, clip));
            _played.Add(tooltipId);
            SaveProgress();

            Logger.Info($"TriggerTooltip() '{gameObject.name}' — '{tooltipId}' queued ({clip.length:F1}s), " +
                        $"{_pending.Count} waiting, next step {NextStep()}");
        }

        /// <summary>
        /// Queued rather than played on the spot. Two triggers can land in the same second — a
        /// harvest that completes a sale, say — and PlayOneShot would put both voices over each
        /// other, which is unlistenable and loses whichever line mattered.
        /// </summary>
        private void Update()
        {
            if (_followUpId != null && Time.time >= _followUpAt)
            {
                string due = _followUpId;
                _followUpId = null;
                TriggerTooltip(due);
            }

            if (_audioSource == null) { _pending.Clear(); return; }
            if (_audioSource.isPlaying) return;

            // Nothing is coming out of the speaker, so whatever was is finished. Noticed here
            // rather than with a coroutine timed to clip.length, because that would drift past a
            // pause, an interruption, or a clip that never actually started.
            if (_playingId != null)
            {
                if (FollowUps.TryGetValue(_playingId, out FollowUp follow) && !_played.Contains(follow.Next))
                {
                    _followUpId = follow.Next;
                    _followUpAt = Time.time + follow.Delay;
                    Logger.Log($"Update() '{gameObject.name}' — '{_playingId}' finished, '{follow.Next}' follows in {follow.Delay:F1}s");
                }
                _playingId = null;
            }

            if (_pending.Count == 0) return;

            QueuedLine next = _pending.Dequeue();
            _playingId = next.Id;
            _audioSource.clip = next.Clip;
            _audioSource.Play();
        }
    }
}
