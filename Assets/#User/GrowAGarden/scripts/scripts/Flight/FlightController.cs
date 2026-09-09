using System.Collections.Generic;
using SomniumSpace.Bridge.Player;
using UnityEngine;
using UnityEngine.XR;

namespace GrowAGarden
{
    /// <summary>
    /// Drives the local player with <see cref="FlightModel"/>, reading the wrists and writing the
    /// player's Root.
    ///
    /// **Lives on SceneManager, not on the player**, for the same reason PlayerManager does: the
    /// player does not exist at scene load — the Somnium client spawns it — so there is nothing to
    /// attach to. This is a scene-resident manager that looks the player up at runtime and reaches
    /// into it, and SomniumPlayersContainer is already sat beside it doing exactly that.
    ///
    /// Entirely local. The player moves their own Root and Somnium replicates the avatar as it
    /// always has, so there is no RPC, no master decision and no authority anywhere in here.
    ///
    /// ⚠ **Play mode flies the wrong rig.** SomniumSpace.SDK.Controllers.PlayerController is the
    /// SDK's world-testing stand-in; in-world you get Somnium's own networked player. Nothing here
    /// can be judged in the Editor, so the logging is not an afterthought.
    /// </summary>
    public class FlightController : MonoBehaviour
    {
        [Tooltip("Master switch. Off leaves Somnium's own flight and glide alone entirely.")]
        [SerializeField] private bool _enabled = true;

        [SerializeField] private FlightModel.Settings _settings = FlightModel.Settings.Default;

        [Header("Ground")]

        [Tooltip("Layers that count as solid world. Touching any of them suspends flight and hands " +
                 "the player back to Somnium's own locomotion.")]
        [SerializeField] private LayerMask _terrainMask = ~0;

        [Tooltip("Radius of the contact test around the player. Generous on purpose — this is " +
                 "'am I touching the island at all', not 'am I stood on it'.")]
        [SerializeField] private float _contactRadius = 1.0f;

        [Tooltip("How far up from Root to centre the contact sphere, roughly mid-torso.")]
        [SerializeField] private float _contactHeight = 1.0f;

        [Tooltip("How far out from the head, horizontally, both hands must be before flight will " +
                 "engage at all. A small adult female arm is roughly 0.65m shoulder to fingertip, " +
                 "and the head sits inboard of the shoulder, so arms out measures well past this " +
                 "while arms at the sides measures around 0.25m. Deliberately under arm length so " +
                 "nobody has to stretch. Entry condition only — bringing the arms in mid-air does " +
                 "not drop the player out of flight.")]
        [SerializeField] private float _armExtension = 0.55f;

        [Tooltip("How far below the head a hand may be and still count as held out. A T-pose puts " +
                 "the hands roughly a shoulder's drop under the head; arms at the sides puts them " +
                 "around 0.7m under. Measured against the head rather than in absolute metres so " +
                 "it scales with the player.")]
        [SerializeField] private float _armDrop = 0.45f;

        [Tooltip("How far in the hands must come before flight ends and Somnium is handed back. " +
                 "Lower than the reach needed to start flying, on purpose: with one threshold for " +
                 "both, a hand hovering near it toggles flight on and off frame to frame.")]
        [SerializeField] private float _armRetract = 0.40f;

        [Header("Hands")]

        [Tooltip("Which local axis of the hand points along the arm. The first in-world run said " +
                 "Forward: left and right agree on it, while Right mirrors between hands.")]
        [SerializeField] private HandAxis _handAxis = HandAxis.Forward;

        [Tooltip("Subtracted from the measured wrist angle. Somnium's hand anchors are not level " +
                 "when an arm is level — the first run read 54-69 degrees with the arms out, which " +
                 "parked the player above the stall angle and left them at zero speed. Set from " +
                 "what Report() prints at a comfortable neutral, or use the auto-calibrate below.")]
        [SerializeField] private float _handPitchOffset = 55f;

        [Tooltip("Take the offset from wherever the hands are on the first airborne frame, instead " +
                 "of the figure above. Whatever pose a player launches in becomes their neutral.")]
        [SerializeField] private bool _autoCalibrate = true;

        [Header("Flap")]


        [Tooltip("Seconds after leaving the ground before a flap can be spent. Stops a landing " +
                 "immediately re-arming and re-spending it, which read as bouncing.")]
        [SerializeField] private float _flapArmDelay = 0.4f;

        [Tooltip("Seconds of immunity from terrain contact straight after flight starts. Without " +
                 "it the surface just stepped off re-grounds the player before they clear it.")]
        [SerializeField] private float _launchGrace = 0.5f;

        [Header("Boost")]

        [Tooltip("Which hand fires the vertical boost. The primary button is A on the right " +
                 "controller and X on the left — Unity abstracts both to the same usage and tells " +
                 "the hands apart by device characteristics. Somnium binds jump to A, and that is " +
                 "deliberate rather than a clash: on the ground A jumps and the boost no-ops, in " +
                 "the air there is nothing to jump from and A boosts.")]
        [SerializeField] private bool _boostOnRightHand = true;

        [Tooltip("Seconds between re-finding the controller. They connect late and drop out when " +
                 "they sleep.")]
        [SerializeField] private float _deviceScanInterval = 2f;

        [Header("Somnium")]

        [Tooltip("Zero Somnium's gravity while flying. Our model owns vertical motion.")]
        [SerializeField] private bool _takeGravity = true;

        [Tooltip("Zero Somnium's walking speed while flying. Without this the joystick still " +
                 "moves the player and fights the model — it was still doing so on the first run, " +
                 "because disabling fly and glide does not touch ordinary locomotion.")]
        [SerializeField] private bool _takeLocomotion = true;

        [Tooltip("Seconds between instrumentation lines.")]
        [SerializeField] private float _logInterval = 2f;

        public enum HandAxis { Forward, Up, Right }

        private FlightModel _model;
        private ISomniumPlayer _player;
        private Transform _root, _head, _leftHand, _rightHand;

        private bool _flying;
        private bool _flapSpent;
        private float _airborneSince;
        private float _calibratedOffset;
        private bool _calibrated;

        private float _nextLogAt;
        private Vector3 _lastWrite;
        private bool _wroteLastFrame;
        private float _driftSum;
        private int _driftCount;
        private float _driftMax;

        private static readonly Collider[] _contacts = new Collider[8];
        private Collider[] _ownColliders = System.Array.Empty<Collider>();

        private void Awake() => _model = new FlightModel(_settings);

        private void OnValidate()
        {
            if (_settings.BestGlideSpeed <= 0f) _settings = FlightModel.Settings.Default;
            _model?.Configure(_settings);
        }

        private void Update()
        {
            if (!_enabled) return;
            MeasurePreviousWrite();
            if (!Resolve()) return;

            PollBoostButton();

            // ── Contact and pose gate flight ──────────────────────────────────────
            //
            // Touching the world at all hands the player back to Somnium: walking, gravity, the
            // lot. Not a downward raycast, because a raycast only answers "is there ground below
            // me" — a player can be hanging off the underside of an island, or buried head-first
            // in one from above, and in both cases flying is the wrong answer.
            //
            // ⚠ UNRESOLVED: there is no specified way to leave the ground.
            //
            // Contact suspends flight and hands the player back to Somnium. But standing on an
            // island *is* contact, so the only route back into flight is to stop touching — which
            // needs flight. The first in-world run deadlocked on exactly this: one landing and the
            // player was grounded for the rest of the session, 34 grounded reports and not one
            // flying frame.
            //
            // Walking off an edge is the intended route, and arms have to be out for flight to
            // engage at all — which also settles an on/off flicker seen while simply walking near
            // an edge, where the contact sphere alternated frame to frame and took flight with it.
            // Arms at your sides now means walking, whatever the geometry says.
            bool touching = Touching();

            if (_flying)
            {
                // Arms drawn in ends flight rather than parking. Drawing the hands to the chest
                // bends the wrists past the stall angle as a side effect, so it used to read as
                // "park" — which is the opposite of what pulling your arms in should mean. Now it
                // hands Somnium back its gravity and its walking, and the player falls.
                bool grounded = touching && Time.time - _airborneSince > _launchGrace;
                if (grounded || !ArmsOut(_armRetract)) SetFlying(false);
            }
            else if (!touching && ArmsOut(_armExtension))
            {
                SetFlying(true);
            }

            if (!_flying)
            {
                Report(0f, 0f, Vector3.zero, true);
                return;
            }

            // ── Input ─────────────────────────────────────────────────────────────
            float leftRaw  = Elevation(_leftHand);
            float rightRaw = Elevation(_rightHand);

            if (!_calibrated)
            {
                _calibrated = true;
                _calibratedOffset = _autoCalibrate ? (leftRaw + rightRaw) * 0.5f : _handPitchOffset;
                Logger.Info($"Calibrate() '{gameObject.name}' — neutral wrist pitch {_calibratedOffset:F1}° " +
                            $"({(_autoCalibrate ? "auto, from launch pose" : "serialized")})");
            }

            float leftPitch  = leftRaw - _calibratedOffset;
            float rightPitch = rightRaw - _calibratedOffset;
            float pitch      = (leftPitch + rightPitch) * 0.5f;
            float difference = rightPitch - leftPitch;   // the offset cancels, but read it from the same place

            // ── Move ──────────────────────────────────────────────────────────────
            Vector3 local = _model.Step(pitch, difference, Time.deltaTime, out float yawDelta);

            _root.Rotate(0f, yawDelta, 0f, Space.World);

            // Along the head, not along Root. Root resolved to 'XR.Body', whose forward is not
            // where the player is looking — the first run flew backwards because of it.
            Vector3 facing = _head != null ? _head.forward : _root.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.0001f) facing = _root.forward;
            facing.Normalize();

            Vector3 move = facing * local.z + Vector3.up * local.y;
            Vector3 target = _root.position + move * Time.deltaTime;

            _root.position = target;
            _lastWrite = target;
            _wroteLastFrame = true;

            Report(pitch, difference, local, false);
        }

        // ── Contact ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Whether the player is in contact with the world.
        ///
        /// The player's own colliders are skipped explicitly rather than being excluded by layer.
        /// A CharacterController is not a trigger, so QueryTriggerInteraction does not help, and a
        /// mask of "everything" put the player's own capsule dead centre of a sphere centred on
        /// the player — which reads as permanently touching the world. Filtering by identity
        /// survives whatever the mask is later set to.
        /// </summary>
        private bool Touching()
        {
            Vector3 centre = _root.position + Vector3.up * _contactHeight;
            int n = Physics.OverlapSphereNonAlloc(centre, _contactRadius, _contacts,
                                                  _terrainMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = _contacts[i];
                if (c == null) continue;
                if (IsOwn(c)) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Both arms held out and up — a T-pose, near enough. Takes the reach it must clear, so
        /// starting flight and staying in it can use different thresholds and not chatter.
        ///
        /// Two conditions, and both are needed. **Reach** is horizontal rather than straight-line,
        /// because arms hanging at the sides are nearly as far from the head in 3D as arms held
        /// out are; it is the *outward* distance that separates the pose from standing. **Drop**
        /// is how far under the head the hands sit, which is what actually distinguishes arms out
        /// at shoulder height from arms out and hanging at forty-five degrees — reach alone
        /// accepts both, and only the first is the pose being asked for.
        /// </summary>
        private bool ArmsOut(float reachNeeded)
        {
            Vector3 origin = Origin();
            return Out(origin, _leftHand, reachNeeded) && Out(origin, _rightHand, reachNeeded);
        }

        private bool Out(Vector3 origin, Transform hand, float reachNeeded) =>
            Reach(origin, hand) >= reachNeeded && (origin.y - hand.position.y) <= _armDrop;

        private Vector3 Origin() => _head != null ? _head.position : _root.position;

        private static float Reach(Vector3 origin, Transform hand)
        {
            Vector3 d = hand.position - origin;
            d.y = 0f;
            return d.magnitude;
        }

        private bool IsOwn(Collider c)
        {
            for (int i = 0; i < _ownColliders.Length; i++) if (_ownColliders[i] == c) return true;
            return c.transform.IsChildOf(_root);
        }

        /// <summary>
        /// Enters or leaves flight, and hands Somnium's locomotion back when leaving.
        ///
        /// Both directions matter. Taking gravity and walking away without restoring them left a
        /// grounded player floating and unable to walk, and re-arming the flap on every grounded
        /// frame is what made a landing bounce.
        /// </summary>
        private void SetFlying(bool flying)
        {
            _flying = flying;
            var motion = _player?.Features?.Motion;
            if (motion == null) return;

            if (flying)
            {
                if (_takeGravity) motion.SetGravity(0f, 0f);
                if (_takeLocomotion) motion.SetMovementSpeed(0f, 0f, 0f);
                _airborneSince = Time.time;
                _flapSpent = false;
                _model.Reset();
            }
            else
            {
                if (_takeGravity) motion.SetGravity(1f, 1f);
                if (_takeLocomotion) motion.SetMovementSpeed(1f, 1f, 1f);
                _calibrated = false;   // next launch re-reads the neutral pose
            }

            Logger.Info($"SetFlying() '{gameObject.name}' — {(flying ? "airborne" : "grounded")} at {_root.position}");
        }

        // ── The player ────────────────────────────────────────────────────────────

        private bool Resolve()
        {
            if (_root != null && _leftHand != null && _rightHand != null) return true;

            _player = PlayerManager.GetLocalPlayer();
            var body = _player?.References?.Body;
            if (body == null) return false;

            if (body.Root == null || body.LeftHand == null || body.RightHand == null) return false;

            _root = body.Root;
            _head = body.Head;
            _leftHand = body.LeftHand;
            _rightHand = body.RightHand;

            var motion = _player.Features?.Motion;
            if (motion == null)
                Logger.Error($"Resolve() '{gameObject.name}' — no Motion feature; Somnium's flight stays on and will fight this");
            else
            {
                motion.SetFlyModeDisableState(true);
                motion.SetGlideDisableState(true);
            }

            _ownColliders = _root.GetComponentsInChildren<Collider>(true);

            Logger.Info($"Resolve() '{gameObject.name}' — took the player: root='{_root.name}' " +
                        $"ownColliders={_ownColliders.Length} " +
                        $"head='{(_head == null ? "<none>" : _head.name)}' left='{_leftHand.name}' right='{_rightHand.name}'");
            return true;
        }

        // ── Input ─────────────────────────────────────────────────────────────────

        private float Elevation(Transform hand) => Elevation(hand, _handAxis);

        private static float Elevation(Transform hand, HandAxis axis)
        {
            Vector3 v = axis switch
            {
                HandAxis.Up => hand.up,
                HandAxis.Right => hand.right,
                _ => hand.forward,
            };
            return Mathf.Asin(Mathf.Clamp(v.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Throws the player upward, once between leaving the ground and touching it again.
        ///
        /// **Nothing calls this yet.** It was driven by a gesture — both hands sweeping down
        /// through a distance — and that gesture is the same movement as drawing the arms in to
        /// end flight, so exiting a glide threw the player 20m into the air instead. The boost
        /// itself is fine and is left intact; it is waiting on a button, which the bridge does not
        /// expose (SomniumPlayerAction.DoAction triggers actions, it cannot listen for one). See
        /// ButtonProbe for what is being done about that.
        /// </summary>
        public void Boost()
        {
            if (!_flying || _flapSpent || Time.time - _airborneSince < _flapArmDelay) return;
            _flapSpent = true;
            _model.Flap();
            Logger.Info($"Boost() '{gameObject.name}' — thrown at {_root.position.y:F1}m, speed={_model.Speed:F1}");
        }

        // ── Boost input ───────────────────────────────────────────────────────────

        private readonly List<InputDevice> _devices = new();
        private InputDevice _boostDevice;
        private float _nextDeviceScanAt;
        private bool _boostWasDown;

        /// <summary>
        /// Reads the controller directly rather than through any action map.
        ///
        /// Somnium's own input bindings are not on the bridge — SomniumPlayerAction.DoAction can
        /// trigger an action but cannot listen for one — so there is no "jump" event to subscribe
        /// to. Reading the device sits below whatever Somnium consumes, which is why the same
        /// button can drive their jump and our boost without either noticing the other.
        /// </summary>
        private void PollBoostButton()
        {
            if (Time.time >= _nextDeviceScanAt)
            {
                _nextDeviceScanAt = Time.time + Mathf.Max(0.5f, _deviceScanInterval);
                InputDeviceCharacteristics want = InputDeviceCharacteristics.Controller
                    | (_boostOnRightHand ? InputDeviceCharacteristics.Right : InputDeviceCharacteristics.Left);
                InputDevices.GetDevicesWithCharacteristics(want, _devices);
                _boostDevice = _devices.Count > 0 ? _devices[0] : default;
            }

            if (!_boostDevice.isValid) return;
            if (!_boostDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool down)) return;

            if (down && !_boostWasDown) Boost();
            _boostWasDown = down;
        }

        // ── Instrumentation ───────────────────────────────────────────────────────

        /// <summary>
        /// Accumulates how far Root drifts from what we wrote, and reports it with everything else.
        ///
        /// The first version logged on every transition and produced an alternating WARN/INFO pair
        /// per frame — the per-frame spam it existed to avoid. Drift turned out to be a steady
        /// downward correction rather than an occasional event, so an average and a peak say more
        /// than any number of individual lines.
        /// </summary>
        private void MeasurePreviousWrite()
        {
            if (!_wroteLastFrame || _root == null) return;
            _wroteLastFrame = false;

            float drift = Vector3.Distance(_root.position, _lastWrite);
            _driftSum += drift;
            _driftCount++;
            if (drift > _driftMax) _driftMax = drift;
        }

        private void Report(float pitch, float difference, Vector3 local, bool grounded)
        {
            if (Time.time < _nextLogAt) return;
            _nextLogAt = Time.time + Mathf.Max(0.25f, _logInterval);

            float avg = _driftCount > 0 ? _driftSum / _driftCount : 0f;
            string drift = $"drift avg={avg:F3} max={_driftMax:F2} n={_driftCount}";
            _driftSum = 0f; _driftCount = 0; _driftMax = 0f;

            if (grounded)
            {
                Logger.Info($"Report() '{gameObject.name}' — GROUNDED at {_root.position} | {drift}");
                return;
            }

            Logger.Info(
                $"Report() '{gameObject.name}' — pitch={pitch:F1} diff={difference:F1} " +
                $"speed={_model.Speed:F1} path={_model.PathAngleDeg:F1} stall={_model.Stall:F2} auth={_model.Authority:F2} " +
                $"vert={local.y:F2} fwd={local.z:F2} y={_root.position.y:F1} " +
                $"flapSpent={_flapSpent} offset={_calibratedOffset:F1} " +
                $"reach L={Reach(Origin(), _leftHand):F2} R={Reach(Origin(), _rightHand):F2} " +
                $"drop L={(Origin().y - _leftHand.position.y):F2} R={(Origin().y - _rightHand.position.y):F2} | {drift} | " +
                $"axes L fwd={Elevation(_leftHand, HandAxis.Forward):F0} up={Elevation(_leftHand, HandAxis.Up):F0} right={Elevation(_leftHand, HandAxis.Right):F0} " +
                $"R fwd={Elevation(_rightHand, HandAxis.Forward):F0} up={Elevation(_rightHand, HandAxis.Up):F0} right={Elevation(_rightHand, HandAxis.Right):F0}");
        }
    }
}
