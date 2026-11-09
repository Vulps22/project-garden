using System.Collections;
using System.Collections.Generic;
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
        private enum Step { None, Arriving, Welcome, Plot, Hut, Claimed, Barrel, Scoreboard, Plant, Growing, Harvest, Sell, Tablet, Almanac, SeedTypes, SendOff, Leaving }

        [Header("Where the player goes")]
        [Tooltip("Where the player stands on arrival, facing its forward.")]
        [SerializeField] private Transform _arrival;
        [Tooltip("Where the tutorial sends them: the world spawn point (WorldFloor's return point).")]
        [SerializeField] private Transform _worldReturn;

        [Header("Helpers")]
        [SerializeField] private InstructionPanel _panel;
        [SerializeField] private TutorialGlow _glow;
        [SerializeField] private FlightController _flight;
        [Tooltip("The carrot: its price, sale value, tagline and growing time are the tutorial's.")]
        [SerializeField] private SeedDefinition _carrot;

        [Header("Voice")]
        [Tooltip("Plays the step lines. In the scene from load, so Somnium routes it through the player's volume.")]
        [SerializeField] private AudioSource _voice;
        [Tooltip("The step lines, in any order: matched to their line by asset name (tutorial_1, tutorial_2, ...; docs/tutorial.md → Voice lines). A line with no clip is text only.")]
        [SerializeField] private AudioClip[] _voiceClips = new AudioClip[0];

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
        [Tooltip("The barrel's own labels, filled as StallDisplay fills them.")]
        [SerializeField] private TMP_Text _barrelName;
        [SerializeField] private TMP_Text _barrelTagline;
        [SerializeField] private TMP_Text _barrelBuy;
        [SerializeField] private TMP_Text _barrelSell;
        [SerializeField] private TMP_Text _scoreboardText;
        [Tooltip("The dirt pile, highlighted while the player has the seed to plant.")]
        [SerializeField] private GameObject _dirtPile;

        [Header("Timing")]
        [Tooltip("Seconds to read a passage with no voice line: this many per word, never under the minimum below.")]
        [SerializeField] private float _secondsPerWord = 0.4f;
        [SerializeField] private float _minimumReadSeconds = 4f;
        [Tooltip("Seconds after a voice line ends before the next step starts.")]
        [SerializeField] private float _afterVoiceSeconds = 1f;
        [Tooltip("Seconds between two lines said one after another.")]
        [SerializeField] private float _betweenLinesSeconds = 0.3f;
        [Tooltip("Seconds without a T pose, after the send-off lines finish, before the avatar-size hint shows.")]
        [SerializeField] private float _tPoseHintAfter = 5f;
        [Tooltip("Seconds the planted aside shows.")]
        [SerializeField] private float _asideSeconds = 5f;
        [Tooltip("Seconds flight stays off after the player lands back on the island.")]
        [SerializeField] private float _flightLockAfterLanding = 5f;

        [Header("Placement")]
        [Tooltip("How far from the void's centre a piece may stand: inside its walls, with room to walk round it.")]
        [SerializeField] private float _reachRadius = 12f;

        private const string TPOSE = "Make a T pose: hold both arms straight out to your sides.";

        private Step _step;
        private int _run;
        private float _stepStartedAt;
        private bool _posed;
        private bool _hinted;
        private Transform _tablet;
        private Coroutine _entering;
        private Coroutine _speaking;
        private readonly Dictionary<int, AudioClip> _clips = new Dictionary<int, AudioClip>();
        private readonly Dictionary<Transform, Pose> _layout = new Dictionary<Transform, Pose>();
        private Vector3 _framePosition;
        private Quaternion _frameRotation = Quaternion.identity;

        private void OnValidate()
        {
            if (_voice == null) _voice = GetComponentInChildren<AudioSource>();
        }

        private void Awake()
        {
            HideEverything();
            enabled = false;   // Update runs only while a step watches the player's body
            foreach (AudioClip clip in _voiceClips) if (LineNumber(clip, out int line)) _clips[line] = clip;
            RememberLayout();
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
            StartCoroutine(TeleportWhenAble(_arrival, _run, () => Go(Step.Welcome)));
        }

        private void OnSkipRequested()
        {
            if (_step == Step.None || _step == Step.Leaving) return;
            Leave(true);
        }

        /// <summary>Takes the player down to the world with flight locked, and ends the tutorial once they are standing.</summary>
        private void Leave(bool skipped)
        {
            _step = Step.Leaving;
            enabled = false;
            StopAllCoroutines();
            int run = ++_run;
            StopVoice();
            TutorialManager.GetInstance()?.SetFlightLocked(true);
            foreach (TutorialItem item in new[] { _scroll, _seed, _produce }) if (item != null) item.Release();
            StartCoroutine(TeleportWhenAble(_worldReturn, run, () => StartCoroutine(FinishOnLanding(run, skipped))));
        }

        /// <summary>Flight stays locked until the player has stood on the island a few seconds, so arriving never flies.</summary>
        private IEnumerator FinishOnLanding(int run, bool skipped)
        {
            HidePieces();
            float giveUpAt = Time.time + 10f;
            while (run == _run && _flight != null && !_flight.Grounded && Time.time < giveUpAt) yield return null;
            yield return new WaitForSeconds(_flightLockAfterLanding);
            if (run != _run) yield break;
            _step = Step.None;
            TutorialManager.GetInstance()?.Complete(skipped);
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
            HidePieces();
        }

        /// <summary>Every piece back in its place and hidden, without stopping what is running.</summary>
        private void HidePieces()
        {
            StopVoice();
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
            enabled = false;
            if (_glow != null) _glow.Clear();
            if (_panel != null) _panel.Hide();
            Logger.Info($"Go() '{gameObject.name}' — step {step}");
            if (_entering != null) StopCoroutine(_entering);
            _entering = StartCoroutine(Enter(step, _run));
        }

        /// <summary>Takes the last step's pieces apart, builds this step's, then says what to do. Every change waits for the one before.</summary>
        private IEnumerator Enter(Step step, int run)
        {
            switch (step)
            {
                case Step.Welcome:
                    yield return Read("Welcome! Before we can let you loose on the Fractured Isles, I want to give you a rundown of the " +
                                      "basics of growing your garden.\n" +
                                      "You can skip the tutorial at any time: open the Settings app on your tablet and choose the World " +
                                      "tab at the bottom. There's a skip option in there.\n" +
                                      "You will also be able to use the Settings app to return to the tutorial at any time in the future.", 25, 26, 27);
                    Next(Step.Plot, run);
                    break;

                case Step.Plot:
                    yield return Build(_plot, Stage(_scroll));
                    Bring(_scroll, true);
                    Say("This is a plot. You must claim a plot before you can plant in it.", "Grab the highlighted scroll.", 1);
                    Glow(_scroll.gameObject);
                    break;

                case Step.Hut:
                    yield return Dissolve(_plot);
                    yield return Build(_hut);
                    Say(string.Empty, "Take the scroll into your hut to claim the plot.", 2);
                    Glow(_hutDoor);
                    break;

                case Step.Claimed:
                    _scroll.SetGrabbable(false);
                    yield return Dissolve(Stage(_scroll));
                    if (_namedDeedText != null) _namedDeedText.text = PlayerManager.GetLocalPlayer().Name;
                    if (_namedDeed != null) _namedDeed.SetActive(true);
                    foreach (GameObject piece in _fire) if (piece != null) piece.SetActive(true);
                    yield return Read("This is your hut. This is where you manage your plot. It's a bit bare right now, but more features are coming.", 3);
                    Next(Step.Barrel, run);
                    break;

                case Step.Barrel:
                    yield return Dissolve(_hut);
                    FillBarrel();
                    yield return Build(_barrel, Stage(_seed));
                    Bring(_seed, true);
                    Say("This is a buy point. You will find these dotted throughout the Fractured Isles. If there is a seed " +
                        "inside it, you can buy the seed for the price listed on the barrel.", "Buy the seed.", 4, 5);
                    Glow(_seed.gameObject);
                    break;

                case Step.Scoreboard:
                    yield return Dissolve(_barrel);
                    if (_scoreboardText != null) _scoreboardText.text = $"{PlayerManager.GetLocalPlayer().Name}    0";
                    yield return Build(_scoreboard);
                    yield return Read("This is your scoreboard. This is how you see how much Thatch you currently own. Thatch is " +
                                     "the currency we use in the Fractured Isles.", 6);
                    Next(Step.Plant, run);
                    break;

                case Step.Plant:
                    yield return Dissolve(_scoreboard);
                    yield return Build(_plot);
                    Say(string.Empty, "Plant the seed by pushing it into the dirt pile on the ground.", 7);
                    Glow(_dirtPile);
                    break;

                case Step.Growing:
                    _seed.SetGrabbable(false);
                    yield return Dissolve(Stage(_seed));
                    Say(string.Empty, $"Wait for the seed to turn into a carrot. Carrots take {Seconds(_carrot)} seconds to grow.", 8, 9);
                    _panel.Aside("Yes, I know it looks like an anus. But it's a free asset.", _asideSeconds);
                    yield return Grow(run);
                    Next(Step.Harvest, run);
                    break;

                case Step.Harvest:
                    _produce.SetGrabbable(true);
                    Notifier.Push("Something is ready to harvest");
                    Say(string.Empty, "Grab the carrot out of the ground.", 10);
                    Glow(_produce.gameObject);
                    break;

                case Step.Sell:
                    yield return Dissolve(_plot);
                    yield return Build(_sellPoint);
                    Say(string.Empty, "Bring the carrot to the sell point to sell it.", 11);
                    Glow(_sellPoint.gameObject);
                    break;

                case Step.Tablet:
                    _produce.SetGrabbable(false);
                    yield return Dissolve(Stage(_produce), _sellPoint);
                    Say(string.Empty, "Open your tablet.", 12);
                    _tablet = TabletManager.FindTablet(PlayerManager.LocalPlayerRig.Root);
                    if (_tablet == null) Logger.Warn($"Enter() '{gameObject.name}' — no tablet found yet; the step keeps looking");
                    enabled = true;
                    break;

                case Step.Almanac:
                    yield return Read("You can use the almanac to see all the seeds available in the game. It shows how often they spawn, " +
                                     "and how many there are in the world. If you haven't found one yet, you will see a silhouette.", 13, 14, 15);
                    Next(Step.SeedTypes, run);
                    break;

                case Step.SeedTypes:
                    FacePlayer(_seedTypes);
                    Place(_seedTypes.transform);
                    _seedTypes.Play();
                    yield return Read("There are three types of seed in the world.\n" +
                                     "<b>Crops</b> grow into things you harvest and sell.\n" +
                                     "<b>Buffing seeds</b> give you multipliers to the size and value of your crop. Buffs always " +
                                     "last for a limited time.\n" +
                                     "<b>Upgrade seeds</b> raise the base value of everything you grow, without applying a " +
                                     "multiplier. They last for as long as you're in the world.", 16, 17, 18, 19);
                    Next(Step.SendOff, run);
                    break;

                case Step.SendOff:
                    _seedTypes.Stop();
                    yield return Read("That's it! That's the basics, the rest you can find out for yourself! You're a grown-up " +
                                      "(I hope), so I'm sure you're smart enough.", 29);
                    yield return Dissolve(_voidFloor);
                    yield return Build(_disc);
                    _posed = false;
                    _hinted = false;
                    TutorialManager.GetInstance()?.SetFlightLocked(false);
                    Say("It is now time to spread your wings and fly.", TPOSE, 20, 21);
                    _stepStartedAt = Time.time + Speaking(new[] { 20, 21 });   // the hint waits for the send-off lines to finish
                    enabled = true;
                    break;
            }
        }

        private void Next(Step step, int run)
        {
            if (run == _run) Go(step);
        }

        /// <summary>Shows a passage and plays its lines; waits for them to end, or for reading time if none is recorded.</summary>
        private IEnumerator Read(string body, params int[] lines)
        {
            Say(body, string.Empty, lines);
            float seconds = Speaking(lines);
            if (seconds > 0f) seconds += _afterVoiceSeconds;
            else
            {
                int words = body.Split(new[] { ' ', '\n' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
                seconds = Mathf.Max(_minimumReadSeconds, words * _secondsPerWord);
            }
            yield return new WaitForSeconds(seconds);
        }

        /// <summary>Shows the text and plays the given lines (tutorial_N) one after another, those that are recorded.</summary>
        private void Say(string body, string instruction, params int[] lines)
        {
            _panel.Show(body, instruction);
            PlayVoice(lines);
        }

        private void PlayVoice(params int[] lines)
        {
            StopVoice();
            if (_voice != null) _speaking = StartCoroutine(Speak(lines));
        }

        private IEnumerator Speak(int[] lines)
        {
            bool first = true;
            foreach (int line in lines)
            {
                if (!_clips.TryGetValue(line, out AudioClip clip)) continue;
                if (!first) yield return new WaitForSeconds(_betweenLinesSeconds);
                first = false;
                _voice.clip = clip;
                _voice.Play();
                yield return new WaitForSeconds(clip.length);
            }
            _speaking = null;
        }

        /// <summary>How long the recorded ones among these lines take to say, gaps included; 0 if none is recorded.</summary>
        private float Speaking(int[] lines)
        {
            float seconds = 0f;
            int recorded = 0;
            foreach (int line in lines)
                if (_clips.TryGetValue(line, out AudioClip clip)) { seconds += clip.length; recorded++; }
            return recorded > 1 ? seconds + (recorded - 1) * _betweenLinesSeconds : seconds;
        }

        /// <summary>The line a clip records: tutorial_N, or tutorial-N as the recording script names it.</summary>
        public static bool LineNumber(AudioClip clip, out int line)
        {
            line = 0;
            if (clip == null) return false;
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(clip.name, @"^tutorial[_-](\d+)$");
            return match.Success && int.TryParse(match.Groups[1].Value, out line);
        }

        private void StopVoice()
        {
            if (_speaking != null) { StopCoroutine(_speaking); _speaking = null; }
            if (_voice != null) _voice.Stop();
        }

        /// <summary>Dissolves every given piece at once and waits until they are gone.</summary>
        private static IEnumerator Dissolve(params TutorialStage[] stages)
        {
            bool any = false;
            foreach (TutorialStage stage in stages)
                if (stage != null && stage.gameObject.activeInHierarchy) { stage.Vanish(); any = true; }
            if (any) yield return new WaitForSeconds(TutorialStage.SECONDS);
        }

        /// <summary>Builds every given piece at once in front of the player, and waits until they are whole.</summary>
        private IEnumerator Build(params TutorialStage[] stages)
        {
            FacePlayer(stages);
            foreach (TutorialStage stage in stages) if (stage != null) { Place(stage.transform); stage.Appear(); }
            yield return new WaitForSeconds(TutorialStage.SECONDS);
        }

        // ── Placement ─────────────────────────────────────────────────────────────

        /// <summary>Every piece's pose as the builder laid it out, relative to the arrival point.</summary>
        private void RememberLayout()
        {
            if (_arrival == null) return;
            Quaternion inverse = Quaternion.Inverse(_arrival.rotation);
            foreach (Component piece in new Component[] { _plot, _hut, _barrel, _scoreboard, _sellPoint, _seedTypes, _disc, _scroll, _seed, _produce })
                if (piece != null)
                    _layout[piece.transform] = new Pose(inverse * (piece.transform.position - _arrival.position), inverse * piece.transform.rotation);
            if (_disc != null)
            {
                Pose disc = _layout[_disc.transform];
                _layout[_disc.transform] = new Pose(new Vector3(0f, disc.position.y, 0f), disc.rotation);   // under the player's feet
            }
        }

        /// <summary>Takes where the player stands and looks as the frame the next pieces are laid out in, kept inside the void's walls.</summary>
        private void FacePlayer(params Component[] pieces)
        {
            Transform head = PlayerManager.LocalPlayerHead;
            if (head == null || _arrival == null) return;

            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = _arrival.forward;
            _frameRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            _framePosition = new Vector3(head.position.x, _arrival.position.y, head.position.z);

            Vector3 centre = _arrival.parent != null ? _arrival.parent.position : _arrival.position;
            float overshoot = 0f;
            foreach (Component piece in pieces)
            {
                if (piece == null || !_layout.TryGetValue(piece.transform, out Pose pose)) continue;
                Vector3 at = _framePosition + _frameRotation * pose.position - centre;
                overshoot = Mathf.Max(overshoot, new Vector2(at.x, at.z).magnitude - _reachRadius);
            }
            if (overshoot > 0f) _framePosition -= _frameRotation * Vector3.forward * overshoot;
        }

        /// <summary>Puts a piece where the layout has it, in the frame <see cref="FacePlayer"/> last took.</summary>
        private void Place(Transform piece)
        {
            if (piece == null || !_layout.TryGetValue(piece, out Pose pose)) return;
            piece.SetPositionAndRotation(_framePosition + _frameRotation * pose.position, _frameRotation * pose.rotation);
        }

        /// <summary>The planted carrot grows out of the dirt pile over the carrot's growing time.</summary>
        private IEnumerator Grow(int run)
        {
            _produce.PutBack();
            Place(_produce.transform);   // in the dirt pile, in the frame the plot was built in
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
                _panel.Instruct(posed ? "Press jump." : TPOSE);
                if (posed) PlayVoice(23);
            }
            if (!posed && !_hinted && _speaking == null && Time.time - _stepStartedAt >= _tPoseHintAfter)
            {
                _hinted = true;
                _panel.Aside("If your avatar is too small, the game will not detect your T pose. Try scaling up your avatar.", 0f);
                PlayVoice(22);
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

        /// <summary>Makes an item, already built by its stage, ready in the hand's reach.</summary>
        private static void Bring(TutorialItem item, bool grabbable)
        {
            item.gameObject.SetActive(true);
            item.SetGrabbable(grabbable);
        }

        /// <summary>The stage an item comes and goes by, put back at its start first if it is not out yet.</summary>
        private static TutorialStage Stage(TutorialItem item)
        {
            if (item == null) return null;
            if (!item.gameObject.activeSelf) item.PutBack();
            return item.GetComponent<TutorialStage>();
        }

        private void FillBarrel()
        {
            if (_carrot == null) return;
            if (_barrelName != null) _barrelName.text = _carrot.displayName;
            if (_barrelTagline != null) _barrelTagline.text = _carrot.tagline ?? string.Empty;
            if (_barrelBuy != null) _barrelBuy.text = $"B: {_carrot.buyPrice}";
            if (_barrelSell != null) _barrelSell.text = _carrot.sellValue > 0 ? $"S: {_carrot.sellValue}" : "S: -";
        }

        private void Glow(GameObject target)
        {
            if (_glow != null) _glow.Show(target);
        }

        private static int Seconds(SeedDefinition seed) => seed != null ? Mathf.RoundToInt(seed.growthDuration) : 10;
    }
}
