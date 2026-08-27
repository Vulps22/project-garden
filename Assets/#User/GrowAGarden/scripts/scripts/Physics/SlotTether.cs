using SomniumSpace.Network.Bridge;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Holds a rigidbody near an anchor with a spring, so it can be knocked about and settles
    /// back. Knows nothing about seeds, shops or money — it is given an anchor and pulls
    /// towards it, and does nothing at all without one.
    ///
    /// Only the state authority integrates the spring. Fusion replicates the resulting motion
    /// to everyone else through NetworkRigidbody3D, so running it on a proxy would just be a
    /// second simulation fighting the one being received.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SlotTether : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private NetworkBridge _networkBridge;

        [Header("Spring")]
        [Tooltip("Pull towards the anchor. Higher snaps back harder.")]
        [SerializeField] private float _spring = 60f;
        [Tooltip("Resistance to motion. Too low oscillates, too high feels sticky.")]
        [SerializeField] private float _damper = 12f;
        [Tooltip("How quickly it rights itself to the anchor's rotation.")]
        [SerializeField] private float _rotationLerp = 8f;

        /// <summary>Where to hold it. Null means idle — the object is left to its own devices.</summary>
        public Transform Anchor { get; private set; }

        public void Attach(Transform anchor) => Anchor = anchor;

        public void Detach() => Anchor = null;

        private void FixedUpdate()
        {
            if (Anchor == null || _rigidbody == null || _rigidbody.isKinematic) return;

            var obj = _networkBridge == null ? null : _networkBridge.Object;
            if (obj == null || !obj.HasStateAuthority) return;

            // Critically-damped-ish spring: pull towards the anchor, bleed off the velocity
            // that pull creates so it settles instead of oscillating.
            Vector3 toAnchor = Anchor.position - _rigidbody.position;
            Vector3 acceleration = toAnchor * _spring - _rigidbody.linearVelocity * _damper;
            _rigidbody.AddForce(acceleration, ForceMode.Acceleration);

            // Rotation is cosmetic here, so it is eased rather than simulated -- torque would
            // need its own tuning and a bumped seed spinning realistically adds nothing.
            _rigidbody.MoveRotation(Quaternion.Slerp(_rigidbody.rotation, Anchor.rotation,
                                                     _rotationLerp * Time.fixedDeltaTime));
        }

        private void OnValidate()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkBridge>();
        }
    }
}
