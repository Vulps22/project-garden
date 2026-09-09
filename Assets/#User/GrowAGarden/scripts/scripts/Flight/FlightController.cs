using SomniumSpace.Bridge.Player;
using UnityEngine;

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

        [Tooltip("Downward hand speed, m/s, that counts as a flap. The first run used 2.5 and " +
                 "ordinary walking triggered it.")]
        [SerializeField] private float _flapSpeedThreshold = 4f;

        [Tooltip("Seconds after leaving the ground before a flap can be spent. Stops a landing " +
                 "immediately re-arming and re-spending it, which read as bouncing.")]
        [SerializeField] private float _flapArmDelay = 0.4f;

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

        private static readonly Collider[] _contacts = new Collider[4];

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

            // ── Contact gates everything ──────────────────────────────────────────
            //
            // Touching the world at all hands the player back to Somnium: walking, gravity, the
            // lot. Not a downward raycast, because a raycast only answers "is there ground below
            // me" — a player can be hanging off the underside of an island, or buried head-first
            // in one from above, and in both cases flying is the wrong answer. An overlap test
            // asks the question actually being asked.
            bool touching = Touching();
            if (touching == _flying) SetFlying(!touching);

            if (!_flying)
            {
                _flapSpent = true;              // no flap until properly airborne again
                _airborneSince = Time.time;
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

            if (DetectFlap() && !_flapSpent && Time.time - _airborneSince >= _flapArmDelay)
            {
                _flapSpent = true;
                _model.Flap();
                Logger.Info($"Flap() '{gameObject.name}' — thrown at {_root.position.y:F1}m, speed={_model.Speed:F1}");
            }

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

        private bool Touching()
        {
            Vector3 centre = _root.position + Vector3.up * _contactHeight;
            int n = Physics.OverlapSphereNonAlloc(centre, _contactRadius, _contacts,
                                                  _terrainMask, QueryTriggerInteraction.Ignore);
            return n > 0;
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

            Logger.Info($"Resolve() '{gameObject.name}' — took the player: root='{_root.name}' " +
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

        private Vector3 _prevLeft, _prevRight;
        private bool _havePrev;

        /// <summary>
        /// Both hands sweeping down hard. A gesture rather than a pose, because a pose cannot feel
        /// like effort. World-space hand movement rather than a button, so it stays true whatever
        /// a player is carrying.
        /// </summary>
        private bool DetectFlap()
        {
            Vector3 l = _leftHand.position, r = _rightHand.position;
            if (!_havePrev) { _prevLeft = l; _prevRight = r; _havePrev = true; return false; }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float lv = (_prevLeft.y - l.y) / dt;
            float rv = (_prevRight.y - r.y) / dt;
            _prevLeft = l; _prevRight = r;

            return lv > _flapSpeedThreshold && rv > _flapSpeedThreshold;
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
                $"speed={_model.Speed:F1} path={_model.PathAngleDeg:F1} stall={_model.Stall:F2} " +
                $"vert={local.y:F2} fwd={local.z:F2} y={_root.position.y:F1} " +
                $"flapSpent={_flapSpent} offset={_calibratedOffset:F1} | {drift} | " +
                $"axes L fwd={Elevation(_leftHand, HandAxis.Forward):F0} up={Elevation(_leftHand, HandAxis.Up):F0} right={Elevation(_leftHand, HandAxis.Right):F0} " +
                $"R fwd={Elevation(_rightHand, HandAxis.Forward):F0} up={Elevation(_rightHand, HandAxis.Up):F0} right={Elevation(_rightHand, HandAxis.Right):F0}");
        }
    }
}
