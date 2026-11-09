using System.Collections;
using TMPro;
using UnityEngine;
using Vulps.GrowAGarden.Flight;
using Vulps.GrowAGarden.Interaction;
using Vulps.GrowAGarden.Notifications;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Tutorial
{
    /// <summary>
    /// The tutorial: takes a new player up to the white void, walks them through ten steps with local copies of the game's
    /// pieces, and sends them down to the world. Nothing in it is networked. See docs/tutorial.md.
    /// </summary>
    public class TutorialPlatform : MonoBehaviour
    {
        private enum Step { None, Arriving, Plot, Hut, Claimed, Barrel, Scoreboard, Plant, Growing, Harvest, Sell, Tablet, Almanac, SeedTypes, SendOff, Leaving }

        [Header("Where the player goes")]
        [Tooltip("Where the player stands on arrival, facing its forward.")]
        [SerializeField] private Transform _arrival;
        [Tooltip("Where the tutorial sends them: the world spawn point (WorldFloor's return point).")]
        [SerializeField] private Transform _worldReturn;

        [Header("Helpers")]
        [SerializeField] private InstructionPanel _panel;
        [SerializeField] private TutorialGlow _glow;
        [SerializeField] private FlightController _flight;
        [Tooltip("The carrot: its price, sale value and growing time are the tutorial's.")]
        [SerializeField] private SeedDefinition _carrot;

        [Header("Stages, in the order they come")]
        [SerializeField] private TutorialStage _voidFloor;
        [SerializeField] private TutorialStage _plot;
        [SerializeField] private TutorialStage _hut;
        [SerializeField] private TutorialStage _barrel;
        [SerializeField] private TutorialStage _scoreboard;
        [SerializeField] private TutorialStage _sellPoint;
        [SerializeField] private SeedTypesShow _seedTypes;
        [SerializeField] private TutorialStage _disc;

        [Header("Carryables")]
        [SerializeField] private TutorialItem _scroll;
        [SerializeField] private TutorialItem _seed;
        [SerializeField] private TutorialItem _produce;

        [Header("Zones")]
        [Tooltip("Inside the hut's door: the scroll arriving here claims the plot.")]
        [SerializeField] private TutorialZone _claimZone;
        [Tooltip("The dirt pile: the seed arriving here is planted.")]
        [SerializeField] private TutorialZone _plantZone;
        [Tooltip("The sell point: the carrot arriving here is sold.")]
        [SerializeField] private TutorialZone _sellZone;

        [Header("Pieces that change")]
        [Tooltip("The hut's door, highlighted while the player carries the scroll to it.")]
        [SerializeField] private GameObject _hutDoor;
        [Tooltip("The hut's named deed, shown once the plot is claimed.")]
        [SerializeField] private GameObject _namedDeed;
        [SerializeField] private TMP_Text _namedDeedText;
        [Tooltip("The hut's fire, its light and its smoke: lit once the plot is claimed.")]
        [SerializeField] private GameObject[] _fire = new GameObject[0];
        [SerializeField] private TMP_Text _barrelLabel;
        [SerializeField] private TMP_Text _scoreboardText;
        [Tooltip("The dirt pile, highlighted while the player has the seed to plant.")]
        [SerializeField] private GameObject _dirtPile;

        [Header("Timing")]
        [Tooltip("Seconds to read a passage: this many per word, never under the minimum below.")]
        [SerializeField] private float _secondsPerWord = 0.4f;
        [SerializeField] private float _minimumReadSeconds = 4f;
        [Tooltip("Seconds without a T pose before the avatar-size hint shows.")]
        [SerializeField] private float _tPoseHintAfter = 5f;
        [Tooltip("Seconds the planted aside shows.")]
        [SerializeField] private float _asideSeconds = 5f;

        private Step _step;
        private int _run;
        private float _stepStartedAt;
        private bool _posed;
        private bool _hinted;
        private Transform _tablet;

        private void Awake()
        {
            HideEverything();
            enabled = false;   // Update runs only on the steps that watch the player's body
            PlayerManager.LocalPlayerJoined += OnLocalPlayerJoined;
            TutorialManager.Started += OnStarted;
            TutorialManager.SkipRequested += OnSkipRequested;
            FlightController.Launched += OnLaunched;
            if (_scroll != null) _scroll.Grabbed += OnGrabbed;
            if (_seed != null) _seed.Grabbed += OnGrabbed;
            if (_produce != null) _produce.Grabbed += OnGrabbed;
            if (_claimZone != null) _claimZone.Entered += OnClaimZone;
            if (_plantZone != null) _plantZone.Entered += OnPlantZone;
            if (_sellZone != null) _sellZone.Entered += OnSellZone;
        }

        private void OnDestroy()
        {
            PlayerManager.LocalPlayerJoined -= OnLocalPlayerJoined;
            TutorialManager.Started -= OnStarted;
            TutorialManager.SkipRequested -= OnSkipRequested;
            FlightController.Launched -= OnLaunched;
            if (_scroll != null) _scroll.Grabbed -= OnGrabbed;
            if (_seed != null) _seed.Grabbed -= OnGrabbed;
            if (_produce != null) _produce.Grabbed -= OnGrabbed;
            if (_claimZone != null) _claimZone.Entered -= OnClaimZone;
            if (_plantZone != null) _plantZone.Entered -= OnPlantZone;
            if (_sellZone != null) _sellZone.Entered -= OnSellZone;
        }

        // ── Starting and ending ───────────────────────────────────────────────────

        /// <summary>A player who has not done the tutorial starts it on arriving in the world.</summary>
        private void OnLocalPlayerJoined(string id, string name)
        {
            TutorialManager tutorial = TutorialManager.GetInstance();
            if (tutorial == null) { Logger.Warn($"OnLocalPlayerJoined() '{gameObject.name}' — no TutorialManager; no tutorial"); return; }
            if (!tutorial.IsDone) tutorial.Begin();
        }

        private void OnStarted()
        {
            _run++;
            HideEverything();
            int dropped = HeldObjects.ReleaseAll(this);
            if (dropped > 0) Logger.Info($"OnStarted() '{gameObject.name}' — let go of {dropped} held object(s) before going up");
            _step = Step.Arriving;
            StartCoroutine(TeleportWhenAble(_arrival, _run, () => Go(Step.Plot)));
        }

        private void OnSkipRequested()
        {
            if (_step == Step.None || _step == Step.Leaving) return;
            Leave(true);
        }

        /// <summary>Takes the player down to the world and tells the manager the tutorial is over.</summary>
        private void Leave(bool skipped)
        {
            _step = Step.Leaving;
            enabled = false;
            int run = ++_run;
            if (_scroll != null) _scroll.Release();
            if (_seed != null) _seed.Release();
            if (_produce != null) _produce.Release();
            StartCoroutine(TeleportWhenAble(_worldReturn, run, () =>
            {
                HideEverything();
                _step = Step.None;
                TutorialManager.GetInstance()?.Complete(skipped);
            }));
        }

        /// <summary>Teleports the local player once Somnium lets us drive them, then calls <paramref name="arrived"/> unless the run changed.</summary>
        private IEnumerator TeleportWhenAble(Transform to, int run, System.Action arrived)
        {
            if (to == null) { Logger.Error($"TeleportWhenAble() '{gameObject.name}' — no destination set"); yield break; }
            while (!PlayerManager.CanDriveLocalPlayer)
            {
                if (run != _run) yield break;
                yield return new WaitForSeconds(0.5f);
            }
            Logger.Info($"TeleportWhenAble() '{gameObject.name}' — to '{to.name}' at {to.position}");
            PlayerManager.TeleportLocalPlayer(to.position, to.eulerAngles, () =>
            {
                if (run == _run) arrived();
            });
        }

        private void HideEverything()
        {
            StopAllCoroutines();
            if (_panel != null) _panel.Hide();
            if (_glow != null) _glow.Clear();
            foreach (TutorialStage stage in new[] { _voidFloor, _plot, _hut, _barrel, _scoreboard, _sellPoint, _disc })
                if (stage != null) stage.Hide();
            if (_seedTypes != null) _seedTypes.Stop();
            foreach (TutorialItem item in new[] { _scroll, _seed, _produce })
                if (item != null) item.PutBack();
            if (_namedDeed != null) _namedDeed.SetActive(false);
            foreach (GameObject piece in _fire) if (piece != null) piece.SetActive(false);
            if (_voidFloor != null) _voidFloor.gameObject.SetActive(true);
        }

        // ── The steps ─────────────────────────────────────────────────────────────

        private void Go(Step step)
        {
            _step = step;
            _stepStartedAt = Time.time;
            enabled = false;
            if (_glow != null) _glow.Clear();
            Logger.Info($"Go() '{gameObject.name}' — step {step}");

            switch (step)
            {
                case Step.Plot:
                    _plot.Appear();
                    Bring(_scroll, true);
                    _panel.Show("This is a plot. You must claim a plot before you can plant in it.", "Grab the highlighted scroll.");
                    Glow(_scroll.gameObject);
                    break;

                case Step.Hut:
                    _plot.Vanish();
                    _hut.Appear();
                    _panel.Show(string.Empty, "Take the scroll into your hut to claim the plot.");
                    Glow(_hutDoor);
                    break;

                case Step.Claimed:
                    Consume(_scroll);
                    if (_namedDeedText != null) _namedDeedText.text = PlayerManager.GetLocalPlayer().Name;
                    if (_namedDeed != null) _namedDeed.SetActive(true);
                    foreach (GameObject piece in _fire) if (piece != null) piece.SetActive(true);
                    Read("This is your hut. This is where you manage your plot. It's a bit bare right now, but more features are coming.",
                         string.Empty, Step.Barrel);
                    break;

                case Step.Barrel:
                    _hut.Vanish();
                    _barrel.Appear();
                    if (_barrelLabel != null) _barrelLabel.text = $"{Name(_carrot)}\n{Price(_carrot)} Thatch";
                    Bring(_seed, true);
                    _panel.Show("This is a buy point. You will find these dotted throughout the Fractured Isles. If there is a seed " +
                                "inside it, you can buy the seed for the price listed on the barrel.", "Buy the seed.");
                    Glow(_seed.gameObject);
                    break;

                case Step.Scoreboard:
                    _barrel.Vanish();
                    _scoreboard.Appear();
                    if (_scoreboardText != null) _scoreboardText.text = $"{PlayerManager.GetLocalPlayer().Name}    0";
                    Read("This is your scoreboard. This is how you see how much Thatch you currently own. Thatch is the currency " +
                         "we use in the Fractured Isles.", string.Empty, Step.Plant);
                    break;

                case Step.Plant:
                    _scoreboard.Vanish();
                    _plot.Appear();
                    _panel.Show(string.Empty, "Plant the seed by pushing it into the dirt pile on the ground.");
                    Glow(_dirtPile);
                    break;

                case Step.Growing:
                    Consume(_seed);
                    _panel.Show(string.Empty, $"Wait for the seed to turn into a carrot. Carrots take {Seconds(_carrot)} seconds to grow.");
                    _panel.Aside("Yes, I know it looks like an anus. But it's a free asset.", _asideSeconds);
                    StartCoroutine(Grow(_run));
                    break;

                case Step.Harvest:
                    _produce.SetGrabbable(true);
                    Notifier.Push("Something is ready to harvest");
                    _panel.Show(string.Empty, "Grab the carrot out of the ground.");
                    Glow(_produce.gameObject);
                    break;

                case Step.Sell:
                    _plot.Vanish();
                    _sellPoint.Appear();
                    _panel.Show(string.Empty, "Bring the carrot to the sell point to sell it.");
                    Glow(_sellPoint.gameObject);
                    break;

                case Step.Tablet:
                    Consume(_produce);
                    StartCoroutine(After(0.6f, _run, () => _sellPoint.Vanish()));
                    _panel.Show(string.Empty, "Open your tablet.");
                    _tablet = TabletManager.FindTablet(PlayerManager.LocalPlayerRig.Root);
                    if (_tablet == null) Logger.Warn($"Go() '{gameObject.name}' — no tablet found; the step waits until one is");
                    enabled = true;
                    break;

                case Step.Almanac:
                    Read("You can use the almanac to see all the seeds available in the game. It shows how often they spawn, how many " +
                         "there are in the world, and if you haven't found one yet, you will see a silhouette.", string.Empty, Step.SeedTypes);
                    break;

                case Step.SeedTypes:
                    _seedTypes.Play();
                    Read("There are three types of seed in the world.\n" +
                         "<b>Crops</b> grow into things you harvest and sell.\n" +
                         "<b>Buffing seeds</b> give multipliers to the size and value of your crop, for example 3x its base value. " +
                         "Buffs always run on a timer.\n" +
                         "<b>Upgrade seeds</b> raise the base value of everything you grow, for example by 5%, without a multiplier. " +
                         "They last for as long as you're in the world.", string.Empty, Step.SendOff);
                    break;

                case Step.SendOff:
                    _seedTypes.Stop();
                    _voidFloor.Vanish();
                    _disc.Appear();
                    _posed = false;
                    _hinted = false;
                    TutorialManager.GetInstance()?.SetFlightLocked(false);
                    _panel.Show("It is now time to spread your wings and fly.", "Make a T pose: hold both arms straight out to your sides.");
                    enabled = true;
                    break;
            }
        }

        /// <summary>Shows a passage for as long as it takes to read, then goes on to <paramref name="next"/>.</summary>
        private void Read(string body, string instruction, Step next)
        {
            _panel.Show(body, instruction);
            int words = body.Split(new[] { ' ', '\n' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
            float seconds = Mathf.Max(_minimumReadSeconds, words * _secondsPerWord);
            StartCoroutine(After(seconds, _run, () => Go(next)));
        }

        private IEnumerator After(float seconds, int run, System.Action then)
        {
            yield return new WaitForSeconds(seconds);
            if (run == _run) then();
        }

        /// <summary>The planted carrot grows out of the dirt pile over the carrot's growing time.</summary>
        private IEnumerator Grow(int run)
        {
            _produce.PutBack();
            _produce.gameObject.SetActive(true);
            _produce.SetGrabbable(false);
            Transform carrot = _produce.transform;
            Vector3 full = carrot.localScale;
            float seconds = _carrot != null ? Mathf.Max(1f, _carrot.growthDuration) : 10f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (run != _run) yield break;
                carrot.localScale = full * Mathf.Lerp(0.1f, 1f, t / seconds);
                yield return null;
            }
            carrot.localScale = full;
            if (run == _run && _step == Step.Growing) Go(Step.Harvest);
        }

        /// <summary>The two steps that wait on the player's own body: the tablet coming out and the T pose.</summary>
        private void Update()
        {
            if (_step == Step.Tablet)
            {
                if (_tablet == null) _tablet = TabletManager.FindTablet(PlayerManager.LocalPlayerRig.Root);
                if (TabletManager.IsOut(_tablet)) Go(Step.Almanac);
                return;
            }

            if (_step != Step.SendOff || _flight == null) return;
            bool posed = _flight.ArmsOutNow;
            if (posed != _posed)
            {
                _posed = posed;
                _panel.Instruct(posed ? "Press jump." : "Make a T pose: hold both arms straight out to your sides.");
            }
            if (!posed && !_hinted && Time.time - _stepStartedAt >= _tPoseHintAfter)
            {
                _hinted = true;
                _panel.Aside("If your avatar is too small, the game will not detect your T pose. Try scaling up your avatar.", 0f);
            }
            if (posed && _hinted) { _hinted = false; _panel.Aside(string.Empty, 0f); _stepStartedAt = float.PositiveInfinity; }
        }

        // ── What the player does ──────────────────────────────────────────────────

        private void OnGrabbed(TutorialItem item)
        {
            if (item == _scroll && _step == Step.Plot) Go(Step.Hut);
            else if (item == _seed && _step == Step.Barrel) Go(Step.Scoreboard);
            else if (item == _produce && _step == Step.Harvest) Go(Step.Sell);
        }

        private void OnClaimZone(TutorialItem item)
        {
            if (item == _scroll && _step == Step.Hut) Go(Step.Claimed);
        }

        private void OnPlantZone(TutorialItem item)
        {
            if (item == _seed && _step == Step.Plant) Go(Step.Growing);
        }

        private void OnSellZone(TutorialItem item)
        {
            if (item == _produce && _step == Step.Sell) Go(Step.Tablet);
        }

        /// <summary>Jumping in a T pose takes off, and that is the send-off.</summary>
        private void OnLaunched()
        {
            if (_step != Step.SendOff) return;
            Logger.Info($"OnLaunched() '{gameObject.name}' — took off from the disc; sending the player down");
            Leave(false);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>Puts an item in its starting place and brings it in from smoke.</summary>
        private static void Bring(TutorialItem item, bool grabbable)
        {
            item.PutBack();
            item.gameObject.SetActive(true);
            item.SetGrabbable(grabbable);
            item.GetComponent<TutorialStage>()?.Appear();
        }

        /// <summary>Takes an item out of the hand and away in smoke.</summary>
        private static void Consume(TutorialItem item)
        {
            item.SetGrabbable(false);
            TutorialStage stage = item.GetComponent<TutorialStage>();
            if (stage != null) stage.Vanish();
            else item.gameObject.SetActive(false);
        }

        private void Glow(GameObject target)
        {
            if (_glow != null) _glow.Show(target);
        }

        private static string Name(SeedDefinition seed) => seed != null && !string.IsNullOrEmpty(seed.displayName) ? seed.displayName : "Carrot";
        private static int Price(SeedDefinition seed) => seed != null ? seed.buyPrice : 10;
        private static int Seconds(SeedDefinition seed) => seed != null ? Mathf.RoundToInt(seed.growthDuration) : 10;
    }
}
