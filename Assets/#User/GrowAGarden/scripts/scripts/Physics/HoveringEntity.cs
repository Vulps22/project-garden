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
        [SerializeField] private float _hoverHeight = 0.5f;
        [Tooltip("Rise rate towards the hover height, in metres per second.")]
        [SerializeField] private float _riseSpeed = 0.6f;

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

        private bool _hovering;
        private bool _falling;
        private float _groundY;
        private float _bobPhase;

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
            if (!_hovering || !_falling) return;

            // Landed. Hover relative to what it actually touched rather than to world zero, so
            // a table, a fence rail or a slope all work the same as the ground.
            _groundY = collision.GetContact(0).point.y;
            _falling = false;
        }

        private void FixedUpdate()
        {
            if (!_hovering) return;         // idle: touch nothing at all
            if (_rigidbody == null || _rigidbody.isKinematic) return;
            if (_falling) return;           // gravity is doing the work

            // Motion replicates from the state authority, so only the owner simulates it.
            var obj = _networkBridge == null ? null : _networkBridge.Object;
            if (obj == null || !obj.HasStateAuthority) return;

            _bobPhase += Time.fixedDeltaTime * _bobSpeed;
            float target = _groundY + _hoverHeight + Mathf.Sin(_bobPhase) * _bobAmplitude;

            // Cancel gravity rather than switching it off: useGravity has a single owner in
            // KinematicController, and hovering should be an active lift that a knock disturbs.
            _rigidbody.AddForce(-Physics.gravity, ForceMode.Acceleration);

            float nextY = Mathf.MoveTowards(_rigidbody.position.y, target,
                                            _riseSpeed * Time.fixedDeltaTime);
            var velocity = _rigidbody.linearVelocity;
            velocity.y = (nextY - _rigidbody.position.y) / Time.fixedDeltaTime;
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

        private void OnValidate()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkBridge>();
        }
    }
}
