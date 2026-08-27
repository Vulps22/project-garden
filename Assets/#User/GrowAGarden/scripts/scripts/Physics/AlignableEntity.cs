using SomniumSpace.Network.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    /// <summary>
    /// Something that can be turned back to the way it should be facing.
    ///
    /// The rotational counterpart of <see cref="ReturnableEntity"/>, and deliberately separate
    /// from it: coming back to a place and facing a particular way are different wants, and
    /// plenty of things need one without the other — a recalled tool may legitimately keep
    /// whatever orientation it was dropped in.
    ///
    /// Idle means idle. With no realignment in progress nothing is written to the rigidbody, so
    /// the object turns freely under physics and in the hand.
    ///
    /// The target is a Quaternion rather than a Transform, so this is a snapshot and not a live
    /// reference: if whatever defined "upright" later moves, the stored rotation is stale. That
    /// is the trade for being able to say "this rotation is home" without needing an object to
    /// point at.
    /// </summary>
    public class AlignableEntity : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private NetworkBridge _networkBridge;

        [Tooltip("Turn rate while realigning, in degrees per second.")]
        [SerializeField] private float _degreesPerSecond = 90f;

        [Tooltip("How far out of true it may drift before righting itself, in degrees. It always lands exactly on the target.")]
        [SerializeField] private float _arriveAngle = 5f;

        [Tooltip("Right itself whenever it drifts further than the arrive angle from the target.")]
        [SerializeField] private bool _autoRealign;

        [Tooltip("Optional. While this is held, auto-realign is suppressed so it cannot fight the hand.")]
        [SerializeField] private XRGrabInteractable _grabInteractable;

        private Quaternion _alignTo;
        private bool _hasAlignTarget;
        private bool _realigning;

        /// <summary>The rotation this rights itself to, or null if it has none.</summary>
        public Quaternion? AlignTarget => _hasAlignTarget ? _alignTo : (Quaternion?)null;

        /// <summary>True while turning back to the target.</summary>
        public bool IsRealigning => _realigning;

        /// <summary>Declares which rotation counts as correct.</summary>
        public void SetAlignTarget(Quaternion rotation)
        {
            _alignTo = rotation;
            _hasAlignTarget = true;
        }

        /// <summary>
        /// However it is facing right now becomes correct. The same mechanism as
        /// <see cref="SetAlignTarget"/>, but it lets a caller that has just placed an object say
        /// so without having to know or name a rotation.
        /// </summary>
        public void CaptureAlignTarget() => SetAlignTarget(transform.rotation);

        /// <summary>Forgets the target and abandons any realignment in progress.</summary>
        public void ClearAlignTarget()
        {
            _hasAlignTarget = false;
            _realigning = false;
        }

        /// <summary>
        /// When on, it rights itself any time it drifts further than the arrive angle, so being
        /// knocked askew corrects rather than accumulating.
        /// </summary>
        public void SetAutoRealign(bool enabled) => _autoRealign = enabled;

        /// <summary>Turn back to the target. Does nothing without one.</summary>
        public void Realign()
        {
            if (!_hasAlignTarget || _rigidbody == null) return;

            _realigning = true;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        /// <summary>True while something has hold of it, when an interactable is wired up.</summary>
        private bool IsHeld => _grabInteractable != null && _grabInteractable.isSelected;

        private void FixedUpdate()
        {
            if (!_realigning)
            {
                // Idle. Nothing written to the rigidbody; the only work is noticing it has been
                // knocked out of true. Held objects are skipped, or turning something over in
                // your hands would be a fight.
                if (_autoRealign && _hasAlignTarget && !IsHeld && _rigidbody != null
                    && Quaternion.Angle(_rigidbody.rotation, _alignTo) > _arriveAngle)
                {
                    Realign();
                }
                return;
            }

            if (_rigidbody == null || !_hasAlignTarget) { _realigning = false; return; }

            // Rotation replicates from the state authority, so only the owner turns it and
            // everyone else receives the result.
            var obj = _networkBridge == null ? null : _networkBridge.Object;
            if (obj == null || !obj.HasStateAuthority) return;

            if (Quaternion.Angle(_rigidbody.rotation, _alignTo) <= _arriveAngle)
            {
                // Land exactly on the target. The arrive angle says when to stop turning, not
                // where it may come to rest — stopping short leaves it permanently askew, and
                // auto-realign uses the same threshold so nothing would ever correct it.
                _rigidbody.rotation = _alignTo;
                _rigidbody.angularVelocity = Vector3.zero;
                _realigning = false;
                return;
            }

            _rigidbody.rotation = Quaternion.RotateTowards(_rigidbody.rotation, _alignTo,
                                                           _degreesPerSecond * Time.fixedDeltaTime);
            _rigidbody.angularVelocity = Vector3.zero;
        }

        private void OnValidate()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkBridge>();
            if (_grabInteractable == null) _grabInteractable = GetComponent<XRGrabInteractable>();
        }
    }
}
