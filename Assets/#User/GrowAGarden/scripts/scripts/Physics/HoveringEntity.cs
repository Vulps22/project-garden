using SomniumSpace.Network.Bridge;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Makes an object float: it falls, lands, then rises to hover a set distance above whatever
    /// it came down on, bobbing gently, turning slowly on the spot and righting itself.
    ///
    /// Told when to start and stop rather than working it out for itself, because the thing that
    /// owns the object already knows whether it is loose and unheld — re-deriving that here would
    /// mean an unwritten contract with whoever sets the physical mode, and it would break
    /// silently if that mapping ever changed.
    ///
    /// Idle means idle: with hovering off, nothing is written to the rigidbody at all.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class HoveringEntity : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private NetworkBridge _networkBridge;

        [Header("Hover")]
        [Tooltip("Resting height above whatever it landed on.")]
        [SerializeField] private float _hoverHeight = 0.25f;
        [Tooltip("Rise rate towards the hover height, in metres per second.")]
        [SerializeField] private float _riseSpeed = 0.6f;

        [Header("Fall")]
        [Tooltip("Fraction of world gravity a dropped seed falls under. Real gravity reads as a " +
                 "yank on something a few centimetres across, because it crosses dozens of its own " +
                 "body lengths a second.")]
        [Range(0f, 1f)]
        [SerializeField] private float _fallGravityScale = 0.35f;
        [Tooltip("Fastest it may fall, in metres per second. Also keeps a hard throw slow enough " +
                 "to collide with the ground rather than tunnel through it.")]
        [SerializeField] private float _maxFallSpeed = 2.5f;

        [Header("Idle motion")]
        [SerializeField] private bool _rotateWhileHovering = true;
        [SerializeField] private float _spinDegreesPerSecond = 20f;

        [Tooltip("Right itself while hovering, keeping the spin free. Corrects a tumble from the fall.")]
        [SerializeField] private bool _keepUpright = true;
        [Tooltip("Righting rate, in degrees per second.")]
        [SerializeField] private float _uprightDegreesPerSecond = 90f;
        [Tooltip("How far it drifts above and below the hover height.")]
        [SerializeField] private float _bobAmplitude = 0.03f;
        [SerializeField] private float _bobSpeed = 1.2f;

        [Header("Settling")]
        [Tooltip("Seconds to bleed off horizontal drift once a knock has finished. Being shoved is " +
                 "fine; coasting away afterwards is not. Vertical motion is left alone.")]
        [SerializeField] private float _settleSeconds = 2f;

        // Long enough to outlast the gap between physics steps, short enough that settling starts
        // the moment a shove really is over.
        private const float ContactGrace = 0.1f;

        private bool _hovering;
        private bool _falling;
        private float _groundY;
        private float _bobPhase;

        private bool _inContact;

        /// <summary>Temporary instrumentation: something is touching this, so hover damping is off.</summary>
        public bool IsInContact => _inContact;
        private float _lastContactTime;
        private float _horizontalDampRate;

        /// <summary>True between BeginHovering and StopHovering.</summary>
        public bool IsHovering => _hovering;

        /// <summary>
        /// Start floating.
        /// </summary>
        /// <param name="shouldFallFirst">
        /// True to drop and hover relative to whatever it lands on. False to rise from where it
        /// already is, for something placed rather than dropped.
        /// </param>
        public void BeginHovering(bool shouldFallFirst)
        {
            if (_rigidbody == null) return;

            _hovering = true;
            _bobPhase = 0f;
            _falling = shouldFallFirst;
            _inContact = false;
            _horizontalDampRate = 0f;

            // Not falling: hover relative to here, so it lifts from where it was put.
            if (!shouldFallFirst) _groundY = _rigidbody.position.y;
        }

        /// <summary>Stop floating and leave the rigidbody alone.</summary>
        public void StopHovering()
        {
            _hovering = false;
            _falling = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            NoteContact();
            if (!_hovering || !_falling) return;

            // Landed. Hover relative to what it actually touched rather than to world zero, so
            // a table, a fence rail or a slope all work the same as the ground.
            _groundY = collision.GetContact(0).point.y;
            _falling = false;

            // Kill the throw dead on impact rather than at release: the arc through the air is
            // worth keeping, the bounce and the skid afterwards are not. Hovering takes over from
            // rest on the very next step.
            if (!HasStateAuthority) return;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _horizontalDampRate = 0f;
        }

        // Contact is tracked by timestamp rather than by pairing Enter with Exit. An Exit that
        // never arrives — colliders and isKinematic both toggle on these objects, and that fakes
        // the event either way — would otherwise leave it "in contact" forever and never settling.
        private void OnCollisionStay(Collision collision) => NoteContact();

        private void NoteContact()
        {
            _inContact = true;
            _lastContactTime = Time.fixedTime;
        }

        /// <summary>Motion replicates from the state authority, so only the owner simulates it.</summary>
        private bool HasStateAuthority
        {
            get
            {
                var obj = _networkBridge == null ? null : _networkBridge.Object;
                return obj != null && obj.HasStateAuthority;
            }
        }

        private void FixedUpdate()
        {
            if (!_hovering) return;         // idle: touch nothing at all
            if (_rigidbody == null || _rigidbody.isKinematic) return;
            if (!HasStateAuthority) return;

            if (_falling)
            {
                ApplyFallGravity();
                return;
            }

            // A shove that has finished leaves the drift behind it. Capture the speed at the
            // moment contact ends and bleed exactly that much away over _settleSeconds, so how
            // long it takes to stop does not depend on how hard it was hit.
            if (_inContact && Time.fixedTime - _lastContactTime > ContactGrace)
            {
                _inContact = false;
                var drift = _rigidbody.linearVelocity;
                float horizontalSpeed = new Vector2(drift.x, drift.z).magnitude;
                _horizontalDampRate = _settleSeconds > 0f
                    ? horizontalSpeed / _settleSeconds
                    : float.PositiveInfinity;
            }

            _bobPhase += Time.fixedDeltaTime * _bobSpeed;
            float target = _groundY + _hoverHeight + Mathf.Sin(_bobPhase) * _bobAmplitude;

            // Cancel gravity rather than switching it off: useGravity has a single owner in
            // KinematicController, and hovering should be an active lift that a knock disturbs.
            _rigidbody.AddForce(-Physics.gravity, ForceMode.Acceleration);

            float nextY = Mathf.MoveTowards(_rigidbody.position.y, target,
                                            _riseSpeed * Time.fixedDeltaTime);
            // One read-modify-write for the whole vector: the lift and the settling both own a
            // part of it, and assigning a fresh velocity in either place would clobber the other.
            var velocity = _rigidbody.linearVelocity;
            velocity.y = (nextY - _rigidbody.position.y) / Time.fixedDeltaTime;

            if (!_inContact && _horizontalDampRate > 0f)
            {
                var horizontal = Vector2.MoveTowards(new Vector2(velocity.x, velocity.z),
                                                     Vector2.zero,
                                                     _horizontalDampRate * Time.fixedDeltaTime);
                velocity.x = horizontal.x;
                velocity.z = horizontal.y;
            }

            _rigidbody.linearVelocity = velocity;

            // Angular velocity is world space, so a Y-only spin turns about world up and cannot
            // introduce a tilt of its own. It also cancels whatever tumble the fall produced.
            _rigidbody.angularVelocity = _rotateWhileHovering
                ? new Vector3(0f, _spinDegreesPerSecond * Mathf.Deg2Rad, 0f)
                : Vector3.zero;

            // Righting lives here rather than in AlignableEntity because the spin is this
            // component's doing, so the conflict it creates is this component's to resolve.
            // Aligning the up vector leaves yaw untouched, so it never fights the spin — which
            // is also why this is not a per-axis mask: rotations do not decompose into
            // independent axes, and an euler-based "align X and Z but not Y" misbehaves once
            // something is tipped a long way over.
            if (!_keepUpright) return;

            Quaternion upright = Quaternion.FromToRotation(_rigidbody.transform.up, Vector3.up)
                                 * _rigidbody.rotation;
            _rigidbody.rotation = Quaternion.RotateTowards(_rigidbody.rotation, upright,
                                                           _uprightDegreesPerSecond * Time.fixedDeltaTime);
        }

        /// <summary>
        /// Thins out gravity for the drop and caps the speed it can reach. Countered with a force
        /// rather than by switching useGravity off, because that property has a single owner in
        /// KinematicController.
        /// </summary>
        private void ApplyFallGravity()
        {
            _rigidbody.AddForce(-Physics.gravity * (1f - _fallGravityScale), ForceMode.Acceleration);

            var velocity = _rigidbody.linearVelocity;
            if (velocity.y < -_maxFallSpeed)
            {
                velocity.y = -_maxFallSpeed;
                _rigidbody.linearVelocity = velocity;
            }
        }

        private void OnValidate()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkBridge>();
        }
    }
}
