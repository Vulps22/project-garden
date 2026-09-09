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

            [Tooltip("How sharply forward speed dies as the stall angle is approached. Higher is " +
                     "a more abrupt stall.")]
            public float StallSharpness;

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

            public static Settings Default => new Settings
            {
                BestGlideSpeed  = 18f,
                SinkAtBestGlide = 1.5f,
                StallAngleDeg   = 60f,
                ParkSink        = 0.5f,
                StallSharpness  = 2f,
                YawDeadzoneDeg  = 5f,
                YawGain         = 1.5f,
                MaxYawRateDeg   = 60f,
                FlapHeight      = 20f,
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

        public FlightModel(Settings s) => Configure(s);

        public void Configure(Settings s)
        {
            _s = s;
            if (_s.BestGlideSpeed <= 0f) _s.BestGlideSpeed = 18f;
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
            // A blend rather than a switch, so the hands losing their grip on the air is
            // something a player feels coming rather than a state that snaps on.
            float overStall = (pitchDeg - _s.StallAngleDeg) / Mathf.Max(1f, _s.StallAngleDeg * 0.5f);
            Stall = Mathf.Clamp01(overStall * _s.StallSharpness);

            // ── Path angle ────────────────────────────────────────────────────────
            // The wrists command a path angle relative to the natural glide slope, not an
            // absolute one. That is what makes wrists-level mean "hold speed, sink gently"
            // instead of "fly level and slowly stop".
            float commandedDeg = Mathf.Clamp(pitchDeg, -85f, _s.StallAngleDeg);
            float pathRad = GlideAngleRad + commandedDeg * Mathf.Deg2Rad;
            PathAngleDeg = pathRad * Mathf.Rad2Deg;

            // ── Energy ────────────────────────────────────────────────────────────
            // Climbing spends speed, diving earns it, and drag takes its cut regardless. There
            // is no sustained climb anywhere in here, deliberately: altitude is bought with
            // speed, and speed is bought with altitude, and the only thing that adds to the
            // system is a flap.
            float dSpeed = -G * Mathf.Sin(pathRad) - DragK * Speed * Speed;
            Speed += dSpeed * dt;

            // Stalling kills forward motion; recovering it is what a dive is for.
            if (Stall > 0f) Speed = Mathf.Lerp(Speed, 0f, Stall * dt * 4f);
            Speed = Mathf.Max(0f, Speed);

            // ── Velocity ──────────────────────────────────────────────────────────
            float glideVertical = Speed * Mathf.Sin(pathRad);
            float parked = -_s.ParkSink;
            float vertical = Mathf.Lerp(glideVertical, parked, Stall);
            float forward = Speed * Mathf.Cos(pathRad) * (1f - Stall);

            // ── Flap ──────────────────────────────────────────────────────────────
            FlapVelocity -= G * dt;
            if (FlapVelocity < 0f) FlapVelocity = 0f;   // only ever a throw upward; the glider owns falling
            vertical += FlapVelocity;

            // ── Yaw ───────────────────────────────────────────────────────────────
            float d = wristDifferenceDeg;
            float signed = Mathf.Sign(d) * Mathf.Max(0f, Mathf.Abs(d) - _s.YawDeadzoneDeg);
            yawDeltaDeg = Mathf.Clamp(signed * _s.YawGain, -_s.MaxYawRateDeg, _s.MaxYawRateDeg) * dt;

            return new Vector3(0f, vertical, forward);
        }
    }
}
