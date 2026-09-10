using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The flight physics, with no Somnium, no bridge and no scene in it.
    ///
    /// Separated for the same reason IslandFragmentGenerator has no UnityEditor dependency: the
    /// only thing that legitimately differs between callers is where the numbers come from and
    /// where the result is written, and neither is physics. This class can be reasoned about, and
    /// its numbers argued over, without anyone thinking about transforms.
    ///
    /// It is an energy-conserving glider. One scalar <see cref="Speed"/> along the body's facing,
    /// and the wrists command a path angle that trades that speed against height. See
    /// docs/flight.md.
    /// </summary>
    public sealed class FlightModel
    {
        [System.Serializable]
        public struct Settings
        {
            [Tooltip("Forward speed at which the glider is in equilibrium — holding speed while " +
                     "sinking at SinkAtBestGlide. m/s.")]
            public float BestGlideSpeed;

            [Tooltip("How fast it sinks at BestGlideSpeed with the wrists level. Together these " +
                     "two are the glide ratio, which is the real world-size constant: 18/1.5 is " +
                     "12:1, so a kilometre of height crosses twelve. m/s.")]
            public float SinkAtBestGlide;

            [Tooltip("Wrist angle above horizon at which the hands stall out. Past this, forward " +
                     "decays to nothing and the player parks.")]
            public float StallAngleDeg;

            [Tooltip("Descent rate once stalled. Deliberately not zero — a gentle drift reads as " +
                     "hovering while keeping the world in motion.")]
            public float ParkSink;


            [Tooltip("Degrees of wrist difference ignored before yaw begins. Hands are never level.")]
            public float YawDeadzoneDeg;

            [Tooltip("Degrees per second of turn per degree of wrist difference, past the dead zone.")]
            public float YawGain;

            [Tooltip("Ceiling on turn rate, deg/s.")]
            public float MaxYawRateDeg;

            [Tooltip("How high one flap throws the player, in metres. Applied as a ballistic " +
                     "impulse that then decays under gravity, so it reads as a throw rather than " +
                     "a teleport.")]
            public float FlapHeight;

            [Tooltip("Airspeed at which the player has full control of the path angle. Below it " +
                     "the nose drops toward straight down however the wrists are held, which is " +
                     "what a wing with no air over it does and what makes a stall recoverable.\n\n" +
                     "Set too low and it creates a trap rather than a recovery. At 8 a player " +
                     "holding 55 degrees of wrist — just under the stall — still had 64% " +
                     "authority at 5 m/s, which pulled the commanded 52 degree climb back to " +
                     "exactly level. A level path neither gains nor loses speed, so it is a " +
                     "stable equilibrium: they hovered at 5 m/s indefinitely and could not tell " +
                     "it from a park. The value has to be high enough that low speed genuinely " +
                     "drops the nose.")]
            public float ControlSpeed;

            [Tooltip("How much of the real cost of climbing is charged. 1 is honest physics, " +
                     "which reads as brutally harsh — a climb strips speed at up to 9.8 m/s². " +
                     "Lower is more forgiving. Below about 0.5 a dive-then-climb cycle starts " +
                     "returning more height than it spent, which makes porpoising a way to gain " +
                     "altitude rather than merely trade it.")]
            public float ClimbCost;

            [Tooltip("Hard ceiling on airspeed, m/s. Quadratic drag already implies a terminal " +
                     "velocity, but it comes out of the glide ratio rather than being chosen — " +
                     "one drag coefficient cannot set both, and the one that makes 18 m/s an " +
                     "equilibrium puts terminal at 76 m/s, head-down skydiving speed. This is the " +
                     "size of the pool a fall can fill however far it lasts, and with ClimbCost " +
                     "it is what stops a drop from world height buying a climb back to it. Real " +
                     "belly-to-earth is about 55.")]
            public float TerminalSpeed;

            [Tooltip("Path angle at or below which you count as diving into a trough. Steep enough " +
                     "that it has to be meant.")]
            public float TroughDiveAngleDeg;

            [Tooltip("Metres you must actually descend in that dive before the trough will pay " +
                     "out. The cost half of the trade — without it, a flick of the wrists earns " +
                     "energy for nothing.")]
            public float TroughMinDescent;

            [Tooltip("Path angle the pull-out has to reach to convert the dive into speed.")]
            public float TroughPullUpDeg;

            [Tooltip("Pull up steeper than this and you get nothing — the script's 'careful not " +
                     "to come up too sharp or you'll hit the breaks'. Below StallAngleDeg, so it " +
                     "is a distinct mistake from stalling rather than the same one.")]
            public float TroughBrakeAngleDeg;

            [Tooltip("Seconds after leaving the dive in which the pull-out still counts. The dive " +
                     "is stored energy and it leaks: hesitate and it is gone.")]
            public float TroughWindowSeconds;

            [Tooltip("Metres per second of bonus speed per metre descended in the dive. This is " +
                     "the slingshot: what you get out scales with what you committed to going " +
                     "down, so a deeper trough pays more.")]
            public float TroughGain;

            [Tooltip("Ceiling on a single trough's bonus, m/s. Stops one enormous drop from " +
                     "buying the rest of the world.")]
            public float TroughMaxBonus;

            public static Settings Default => new Settings
            {
                BestGlideSpeed  = 18f,
                SinkAtBestGlide = 1.0f,
                StallAngleDeg   = 60f,
                ParkSink        = 0.5f,
                YawDeadzoneDeg  = 5f,
                YawGain         = 1.5f,
                MaxYawRateDeg   = 60f,
                FlapHeight      = 20f,
                ControlSpeed    = 8f,
                ClimbCost       = 0.45f,
                TerminalSpeed   = 55f,

                TroughDiveAngleDeg  = -45f,
                TroughMinDescent    = 25f,
                TroughPullUpDeg     = 35f,
                TroughBrakeAngleDeg = 55f,
                TroughWindowSeconds = 1.5f,
                TroughGain          = 0.2f,
                TroughMaxBonus      = 15f,
            };
        }

        private const float G = 9.81f;

        private Settings _s;

        /// <summary>Forward speed along the body's facing, m/s. The whole state of the glider —
        /// how fast you are going is how much height you have banked.</summary>
        public float Speed { get; private set; }

        /// <summary>Vertical velocity contributed by a flap, decaying under gravity. Kept apart
        /// from the glider so a flap reads as a throw layered over flight rather than as a
        /// discontinuity in the glide.</summary>
        public float FlapVelocity { get; private set; }

        /// <summary>Instrumentation: the path angle actually flown this step, degrees.</summary>
        public float PathAngleDeg { get; private set; }

        /// <summary>Instrumentation: 0 flying, 1 fully stalled.</summary>
        public float Stall { get; private set; }

        /// <summary>Instrumentation: 0 no control of the path angle, 1 full control.</summary>
        public float Authority { get; private set; }

        /// <summary>Instrumentation: metres descended in the dive currently being banked, or in the
        /// one still inside its pull-out window. 0 when there is nothing stored.</summary>
        public float TroughDescent { get; private set; }

        /// <summary>Instrumentation: m/s awarded by the last trough shot. Never reset, so the log
        /// says what the last one was worth rather than only that one happened.</summary>
        public float LastTroughBonus { get; private set; }

        /// <summary>Whether the wings are currently below TroughDiveAngleDeg, banking descent.</summary>
        private bool _diving;

        /// <summary>Seconds left to pull out and convert a banked dive; 0 when nothing is banked.</summary>
        private float _troughWindowLeft;

        public FlightModel(Settings s) => Configure(s);

        public void Configure(Settings s)
        {
            _s = s;
            if (_s.BestGlideSpeed <= 0f) _s.BestGlideSpeed = 18f;
            if (_s.ControlSpeed <= 0f) _s.ControlSpeed = 8f;
            if (_s.ClimbCost <= 0f) _s.ClimbCost = 0.45f;
            if (_s.TerminalSpeed <= 0f) _s.TerminalSpeed = 55f;

            // A Settings struct already serialized in a scene deserializes new fields as 0, not as
            // Default — so without these a scene saved before the trough existed would call every
            // shallow descent a dive and pay out nothing, which reads as the feature being broken
            // rather than unset. TroughGain is deliberately not guarded: 0 there is a legitimate
            // "turn the mechanic off" and the only value that means it.
            if (_s.TroughDiveAngleDeg >= 0f)  _s.TroughDiveAngleDeg = -45f;
            if (_s.TroughMinDescent <= 0f)    _s.TroughMinDescent = 25f;
            if (_s.TroughPullUpDeg <= 0f)     _s.TroughPullUpDeg = 35f;
            if (_s.TroughBrakeAngleDeg <= 0f) _s.TroughBrakeAngleDeg = 55f;
            if (_s.TroughWindowSeconds <= 0f) _s.TroughWindowSeconds = 1.5f;
            if (_s.TroughMaxBonus <= 0f)      _s.TroughMaxBonus = 15f;
            if (Speed <= 0f) Speed = _s.BestGlideSpeed;
        }

        /// <summary>
        /// The natural glide slope — the path angle at which drag is exactly paid for by sinking.
        /// Negative. Wrists level means flying this, which is why level flight still costs height.
        /// </summary>
        private float GlideAngleRad => Mathf.Atan2(-_s.SinkAtBestGlide, _s.BestGlideSpeed);

        /// <summary>
        /// Quadratic drag coefficient, derived rather than tuned: it is whatever makes
        /// BestGlideSpeed an equilibrium at the natural glide slope. Expose one of these as a
        /// number and the other stops agreeing with it, so only the two readable ones are settings.
        /// </summary>
        private float DragK => -G * Mathf.Sin(GlideAngleRad) / (_s.BestGlideSpeed * _s.BestGlideSpeed);

        public void Reset()
        {
            Speed = _s.BestGlideSpeed;
            FlapVelocity = 0f;
            ClearTrough();
        }

        /// <summary>Forget any banked dive. A trough that spans a park or a landing is not a
        /// trough — the energy it represents was spent stopping.</summary>
        private void ClearTrough()
        {
            _diving = false;
            TroughDescent = 0f;
            _troughWindowLeft = 0f;
        }

        /// <summary>Throws the player upward. The caller owns "once per airborne period".</summary>
        public void Flap() => FlapVelocity = Mathf.Sqrt(2f * G * Mathf.Max(0f, _s.FlapHeight));

        /// <summary>
        /// Advances one step.
        /// </summary>
        /// <param name="pitchDeg">Wrist angle above horizon, averaged across both hands.</param>
        /// <param name="wristDifferenceDeg">Right minus left wrist angle; drives yaw.</param>
        /// <returns>Velocity in local terms: x unused, y vertical, z forward along facing.</returns>
        public Vector3 Step(float pitchDeg, float wristDifferenceDeg, float dt, out float yawDeltaDeg)
        {
            dt = Mathf.Clamp(dt, 0f, 0.1f);   // a hitch must not launch anyone into orbit

            // ── Stall ─────────────────────────────────────────────────────────────
            // Wrists above the stall angle: parked. Not a blend — above the angle you are
            // parked, below it you are flying, and that is the whole rule.
            bool parked = pitchDeg >= _s.StallAngleDeg;
            Stall = parked ? 1f : 0f;

            if (parked)
            {
                Speed = Mathf.MoveTowards(Speed, 0f, _s.BestGlideSpeed * dt);
                Authority = 0f;
                PathAngleDeg = -90f;
                FlapVelocity = Mathf.Max(0f, FlapVelocity - G * dt);
                yawDeltaDeg = YawFrom(wristDifferenceDeg, dt);
                ClearTrough();
                return new Vector3(0f, -_s.ParkSink + FlapVelocity, 0f);
            }

            // ── Path angle ────────────────────────────────────────────────────────
            // The wrists command a path angle relative to the natural glide slope, not an
            // absolute one. That is what makes wrists-level mean "hold speed, sink gently"
            // instead of "fly level and slowly stop".
            float commandedDeg = Mathf.Clamp(pitchDeg, -85f, _s.StallAngleDeg);
            float commandedRad = GlideAngleRad + commandedDeg * Mathf.Deg2Rad;

            // Control authority scales with airspeed, and this is what stops a stall being a
            // trap. Both velocities below are proportional to Speed, so at zero airspeed the
            // model froze solid — nothing moved and nothing fell — and with the wrists up the
            // energy term kept pushing Speed *down* into its own clamp. A wing with no air over
            // it has no say in anything, so the path angle falls away toward straight down as
            // speed bleeds off, gravity does what it always does, and the recovery is the dive
            // the player would have had to make anyway.
            // Squared, not linear, and the shape matters more than the threshold.
            //
            // A linear ramp starts taking control away the moment speed dips below the threshold
            // — and climbing *costs* speed by design, so a climb pushed the player under it and
            // the fading authority then flattened the climb out. Holding 26 degrees of wrist, the
            // path fell from 22.9 to 0.1 and a five-metre-a-second climb died to nothing in about
            // four seconds. The act of climbing removed the ability to climb.
            //
            // Squaring holds full control across the whole of normal flight and collapses only
            // when speed is genuinely gone, which is the one case it was added for.
            float speedRatio = Mathf.Clamp01(Speed / _s.ControlSpeed);
            Authority = speedRatio * speedRatio;
            float pathRad = Mathf.Lerp(-Mathf.PI * 0.5f, commandedRad, Authority);
            PathAngleDeg = pathRad * Mathf.Rad2Deg;

            // ── Energy ────────────────────────────────────────────────────────────
            // Climbing spends speed, diving earns it, and drag takes its cut regardless. There
            // is no sustained climb anywhere in here, deliberately: altitude is bought with
            // speed, and speed is bought with altitude, and the only thing that adds to the
            // system is a flap.
            // Climbing is charged at a fraction of what it really costs. Honest physics strips
            // speed at up to 9.8 m/s² on the way up, which reads as being punished for every
            // metre gained rather than trading for it. Diving still earns at the full rate, so
            // the exchange is deliberately in the player's favour — see ClimbCost.
            float gravity = -G * Mathf.Sin(pathRad);
            if (gravity < 0f) gravity *= _s.ClimbCost;          // negative == climbing == losing speed

            float dSpeed = gravity - DragK * Speed * Speed;
            Speed += dSpeed * dt;

            Speed = Mathf.Max(0f, Speed);

            // ── Trough shot ───────────────────────────────────────────────────────
            Speed += TroughStep(PathAngleDeg, Speed * Mathf.Sin(pathRad), dt);
            if (_s.TerminalSpeed > 0f) Speed = Mathf.Min(Speed, _s.TerminalSpeed);

            // ── Velocity ──────────────────────────────────────────────────────────
            // Sink is linear in speed, and stays that way until a group playtest says otherwise.
            // The inverse curve (sink scaled by BestGlideSpeed/Speed) was flown on 2026-09-09 and
            // did not work: scaling only this component leaves Speed no longer the magnitude of the
            // velocity the energy step above just charged for, so a dive earns speed as if
            // descending steeply while actually descending shallowly. See the parked section of
            // docs/flight.md — a real speed-dependent sink is a change to the glide angle, not a
            // multiplier on the answer.
            float vertical = Speed * Mathf.Sin(pathRad);
            float forward = Speed * Mathf.Cos(pathRad);

            // ── Flap ──────────────────────────────────────────────────────────────
            FlapVelocity -= G * dt;
            if (FlapVelocity < 0f) FlapVelocity = 0f;   // only ever a throw upward; the glider owns falling
            vertical += FlapVelocity;

            // ── Yaw ───────────────────────────────────────────────────────────────
            yawDeltaDeg = YawFrom(wristDifferenceDeg, dt);

            return new Vector3(0f, vertical, forward);
        }

        /// <summary>
        /// The trough shot: dive hard, pull out hard, come up with more than you left with.
        ///
        /// The rest of this model conserves energy on purpose — "altitude is bought with speed, and
        /// speed is bought with altitude, and the only thing that adds to the system is a flap".
        /// This is the deliberate exception, and it is a *skill* exception: the bonus scales with
        /// how far you actually committed to going down, so it cannot be farmed with a flick of the
        /// wrists, and it is thrown away if the pull-out is late or too sharp.
        ///
        /// Modelled on a slingshot with the gravity well taken out. What a real one rewards is
        /// burning deep, where you are fastest; what this one rewards is diving deep, and for the
        /// same reason — the payout is per metre descended rather than a flat bonus, so a deeper
        /// trough is worth more than two shallow ones covering the same height.
        ///
        /// Returns the speed to add this step, which is zero on all but the one frame that pays out.
        /// </summary>
        private float TroughStep(float pathDeg, float verticalSpeed, float dt)
        {
            bool diving = pathDeg <= _s.TroughDiveAngleDeg;

            if (diving)
            {
                // Bank real descent, not intent: a steep nose that is not actually going down —
                // stalled, or held up by a flap — has not paid for anything.
                if (!_diving) { _diving = true; TroughDescent = 0f; }
                if (verticalSpeed < 0f) TroughDescent += -verticalSpeed * dt;
                _troughWindowLeft = _s.TroughWindowSeconds;
                return 0f;
            }

            if (_diving)
            {
                _diving = false;                       // left the dive; the window is now running
                if (TroughDescent < _s.TroughMinDescent)
                {
                    TroughDescent = 0f;                // too shallow to have been a trough at all
                    _troughWindowLeft = 0f;
                }
            }

            if (_troughWindowLeft <= 0f) return 0f;

            _troughWindowLeft -= dt;
            if (_troughWindowLeft <= 0f)
            {
                TroughDescent = 0f;                    // hesitated; the stored energy is gone
                return 0f;
            }

            // Too sharp is its own mistake, and it is silent: you keep the height you clawed back
            // and simply do not get paid, which is what "you'll hit the breaks" should feel like.
            if (pathDeg > _s.TroughBrakeAngleDeg)
            {
                TroughDescent = 0f;
                _troughWindowLeft = 0f;
                return 0f;
            }

            if (pathDeg < _s.TroughPullUpDeg) return 0f;

            float bonus = Mathf.Min(TroughDescent * _s.TroughGain, _s.TroughMaxBonus);
            LastTroughBonus = bonus;
            TroughDescent = 0f;
            _troughWindowLeft = 0f;
            return bonus;
        }

        private float YawFrom(float wristDifferenceDeg, float dt)
        {
            float signed = Mathf.Sign(wristDifferenceDeg)
                         * Mathf.Max(0f, Mathf.Abs(wristDifferenceDeg) - _s.YawDeadzoneDeg);
            return Mathf.Clamp(signed * _s.YawGain, -_s.MaxYawRateDeg, _s.MaxYawRateDeg) * dt;
        }
    }
}
