using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Turns things that happen in the garden into tutorial lines.
    ///
    /// **All of the coupling lives here, on purpose.** The alternative was a
    /// `TutorialManager.GetInstance()?.TriggerTooltip(...)` sprinkled through SellPoint, Produce,
    /// DeedClaimZone and FlightController — which would make the narrator a dependency of the
    /// economy, and would mean deleting the tutorial later meant editing six unrelated classes.
    /// Instead each of those announces a fact named for itself and knows nothing about narration;
    /// this one file listens and decides that a fact deserves a line.
    ///
    /// Every trigger is local. The tutorial is one player's experience of the world, so nothing in
    /// here is replicated, decided by the master, or visible to anyone else.
    ///
    /// Ordering is not enforced here — TutorialManager owns that, gating each linear step on the
    /// player having reached it. This file's job is only to say "this just happened".
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialTriggers : MonoBehaviour
    {
        [Tooltip("Balance at which the exploration line plays. The script says 100 Thatch in so " +
                 "many words, so changing this makes the recording wrong.")]
        [SerializeField] private int _explorationThatch = 100;

        [Tooltip("Seconds continuously airborne before the trough-shot line plays. Deliberately " +
                 "shorter than tutorial_flight_1 (17.1s): the queue holds the second line until " +
                 "the first has finished, so a low threshold means they play back to back rather " +
                 "than over each other. Low because staying up is still hard — a threshold longer " +
                 "than a typical flight is one nobody ever reaches.")]
        [SerializeField] private float _troughShotAfterSeconds = 5f;

        [Tooltip("Seconds a flight must have lasted for the wrap-up to play on landing. Above the " +
                 "trough-shot threshold, so 'that's the basics of flying' only follows a flight " +
                 "that actually got as far as the basics.")]
        [SerializeField] private float _wrapUpAfterSeconds = 10f;

        [SerializeField] private FlightController _flight;

        /// <summary>Reset on every launch, so the line is considered once per flight.</summary>
        private bool _troughShotPlayedThisFlight;

        private void OnValidate()
        {
            if (_flight == null) _flight = FindFirstObjectByType<FlightController>();
        }

        private void OnEnable()
        {
            LocalPlayerMotionProbe.LocalPlayerMoved += OnLocalPlayerMoved;
            DeedClaimZone.DeedCarriedIn += OnDeedCarriedIn;
            Produce.AnyLifecycleChanged += OnProduceChanged;
            SellPoint.SaleCompleted += OnSaleCompleted;
            EconomyManager.OnPlayerBalanceChanged += OnBalanceChanged;
            FlightController.Launched += OnLaunched;
            FlightController.Landed += OnLanded;
        }

        private void OnDisable()
        {
            LocalPlayerMotionProbe.LocalPlayerMoved -= OnLocalPlayerMoved;
            DeedClaimZone.DeedCarriedIn -= OnDeedCarriedIn;
            Produce.AnyLifecycleChanged -= OnProduceChanged;
            SellPoint.SaleCompleted -= OnSaleCompleted;
            EconomyManager.OnPlayerBalanceChanged -= OnBalanceChanged;
            FlightController.Launched -= OnLaunched;
            FlightController.Landed -= OnLanded;
        }

        private static void Play(string tooltipId) =>
            TutorialManager.GetInstance()?.TriggerTooltip(tooltipId);

        /// <summary>
        /// §0 Beta Notice, which hands over to §1 Welcome a second later through TutorialManager's
        /// FollowUps.
        ///
        /// Movement, not PlayerManager.LocalPlayerJoined: joining happens before the headset is
        /// tracking and before the world has arrived around the player, so a line played then is a
        /// line nobody hears. See LocalPlayerMotionProbe.
        /// </summary>
        private void OnLocalPlayerMoved() => Play(TutorialManager.Tooltips.Tutorial.BetaNotice);

        /// <summary>
        /// §14 Hut Intro. The deed going through the door is also what claims the plot, so §2 First
        /// Thatch would fire at the same instant — it is chained off the end of this line instead,
        /// in TutorialManager's FollowUps.
        /// </summary>
        private void OnDeedCarriedIn() => Play(TutorialManager.Tooltips.Tutorial.Hut);

        /// <summary>
        /// §3 First Harvest. Fired from the lifecycle event rather than Produce.Harvested, which is
        /// master-only and so never reaches the player who did the harvesting unless they happen to
        /// be the master.
        ///
        /// Both conditions are re-checked on every lifecycle change because the harvest and the
        /// grabber RPC that names the holder arrive in either order; whichever lands second is the
        /// one that completes the picture.
        /// </summary>
        private void OnProduceChanged(Produce produce)
        {
            if (produce == null || !produce.IsHarvested) return;
            if (produce.HolderId != PlayerManager.LocalPlayerId) return;

            Play(TutorialManager.Tooltips.Tutorial.Selling);
        }

        /// <summary>
        /// §4 Selling Basics — this player's own sale, not anybody else's.
        ///
        /// The seller rides in the announcement, so this is a comparison rather than a guess. The
        /// first version kept a local set of "ids I offered" and matched against that; it never
        /// matched in-world and there was nothing in the log to say why, because a set that comes
        /// up empty looks exactly like a sale that never happened.
        /// </summary>
        private void OnSaleCompleted(string sellerId, int value)
        {
            if (sellerId != PlayerManager.LocalPlayerId) return;
            Play(TutorialManager.Tooltips.Tutorial.SellingBasics);
        }

        /// <summary>
        /// §5 Exploration Intro. OnPlayerBalanceChanged is already local — EconomyManager derives it
        /// from GetLocalPlayer() — so there is nobody else's balance to filter out.
        /// </summary>
        private void OnBalanceChanged(int newValue, int oldValue)
        {
            if (newValue < _explorationThatch) return;
            Play(TutorialManager.Tooltips.Tutorial.ExplorationIntro);
        }

        /// <summary>§6 Oops Moment and §7 Flight Controls, which are one recording.</summary>
        private void OnLaunched()
        {
            _troughShotPlayedThisFlight = false;
            Play(TutorialManager.Tooltips.Tutorial.FlightControls);
        }

        /// <summary>
        /// §9 Wrap-Up, and only after a flight worth wrapping up.
        ///
        /// Gated on the landing's own duration because skipped steps stay skipped: a player who
        /// hops and immediately lands would otherwise burn the wrap-up — and take the trough-shot
        /// line with it, since it sits earlier in the order — having been taught nothing at all.
        /// A short hop simply does not count, and the next real flight gets it.
        /// </summary>
        private void OnLanded(float airborneSeconds)
        {
            if (airborneSeconds < _wrapUpAfterSeconds) return;
            Play(TutorialManager.Tooltips.Tutorial.FlyingWrapUp);
        }

        /// <summary>§8 Trough-Shot, once they have been up long enough to have felt the speed
        /// change the line describes.</summary>
        private void Update()
        {
            if (_flight == null || !_flight.IsFlying) return;
            if (_troughShotPlayedThisFlight) return;
            if (_flight.AirborneSeconds < _troughShotAfterSeconds) return;

            _troughShotPlayedThisFlight = true;
            Play(TutorialManager.Tooltips.Tutorial.TroughShotTechnique);
        }
    }
}
