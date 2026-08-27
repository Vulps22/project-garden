using SomniumSpace.Network.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    /// <summary>
    /// Something that can be recalled to a place.
    ///
    /// Knows nothing about seeds, shops or slots — it is given a target and, when asked, travels
    /// to it at a steady speed, ignoring collisions so that whatever displaced it cannot shove it
    /// around on the way. Anything can recall anything to anywhere.
    ///
    /// Idle means idle: with no recall in progress nothing is written to the rigidbody at all, so
    /// the object behaves as an ordinary physics body and can be pushed, grabbed and knocked
    /// about with no interference. Movement only happens between <see cref="Recall"/> and arrival.
    /// </summary>
    public class ReturnableEntity : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private NetworkBridge _networkBridge;

        [Tooltip("Travel speed while returning, in metres per second.")]
        [SerializeField] private float _speed = 0.35f;
        [Tooltip("How far it may drift before coming home. It always lands exactly on the target.")]
        [SerializeField] private float _arriveDistance = 0.1f;

        [Tooltip("Where this returns to. Usually set at runtime by whatever owns the object.")]
        [SerializeField] private Transform _returnTo;

        [Tooltip("Bring itself back whenever it drifts further than the arrive distance.")]
        [SerializeField] private bool _autoRecall;

        [Tooltip("Optional. While this is held, auto-recall is suppressed.")]
        [SerializeField] private XRGrabInteractable _grabInteractable;

        private bool _returning;
        private bool _collisionsSuspended;
        private bool _grabSuspended;

        /// <summary>Where this will go when recalled, or null if it has nowhere to be.</summary>
        public Transform ReturnTarget => _returnTo;

        /// <summary>True while travelling. Collisions are disabled for the duration.</summary>
        public bool IsReturning => _returning;

        /// <summary>
        /// Sets where this belongs. A Transform rather than a position, so a recall always aims
        /// at where the target is now — the target is free to move.
        /// </summary>
        public void SetReturnTarget(Transform target) => _returnTo = target;

        /// <summary>Forgets the target and abandons any recall in progress.</summary>
        public void ClearReturnTarget()
        {
            _returnTo = null;
            StopReturning();
        }

        /// <summary>
        /// When on, it brings itself back any time it drifts further than the arrive distance
        /// from its target, so a nudge tidies itself up instead of accumulating.
        /// </summary>
        public void SetAutoRecall(bool enabled) => _autoRecall = enabled;

        /// <summary>True while something has hold of it, when an interactable is wired up.</summary>
        private bool IsHeld => _grabInteractable != null && _grabInteractable.isSelected;

        /// <summary>
        /// Come home. Does nothing without a target.
        /// </summary>
        /// <param name="shouldIgnoreCollisions">
        /// True for an enforced return: the object travels through anything in the way and
        /// cannot be picked up until it arrives. Use when a rule is being applied to it rather
        /// than when it is simply out of place — a grab beats a tidy-up, but a rule beats a grab.
        ///
        /// False for an ordinary tidy-up, which collides normally and leaves the grab alone,
        /// because bumping into things on the way is correct rather than a problem to bulldoze.
        /// </param>
        public void Recall(bool shouldIgnoreCollisions)
        {
            if (_returnTo == null || _rigidbody == null) return;

            _returning = true;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            if (!shouldIgnoreCollisions) return;

            // Enforced: take it out of the player's reach for the trip.
            _collisionsSuspended = true;
            _rigidbody.detectCollisions = false;

            if (_grabInteractable != null && _grabInteractable.enabled)
            {
                _grabSuspended = true;
                _grabInteractable.enabled = false;   // also cancels any grab in progress
            }
        }

        private void StopReturning()
        {
            if (!_returning) return;
            _returning = false;
            if (_rigidbody == null) return;

            // Only re-enable what this actually suspended, so a plain tidy-up never switches
            // collisions on for something that had them off for its own reasons.
            if (_collisionsSuspended)
            {
                _collisionsSuspended = false;
                _rigidbody.detectCollisions = true;
            }

            if (_grabSuspended)
            {
                _grabSuspended = false;
                if (_grabInteractable != null) _grabInteractable.enabled = true;
            }

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        private void FixedUpdate()
        {
            if (!_returning)
            {
                // Idle. Nothing is written to the rigidbody; the only work is noticing that it
                // has drifted and asking itself to come back — colliding normally on the way,
                // because a tidy-up has no business barging through anything.
                //
                // Held objects are skipped. The threshold is smaller than a typical trigger
                // volume, so a deliberate pull crosses it long before the object leaves that
                // volume, and without this picking something up would recall it out of a hand.
                if (_autoRecall && _returnTo != null && !IsHeld && _rigidbody != null
                    && Vector3.Distance(_rigidbody.position, _returnTo.position) > _arriveDistance)
                {
                    Recall(shouldIgnoreCollisions: false);
                }
                return;
            }

            if (_rigidbody == null || _returnTo == null) { StopReturning(); return; }

            // Position replicates from the state authority, so only the owner drives the trip
            // and everyone else receives the result.
            var obj = _networkBridge == null ? null : _networkBridge.Object;
            if (obj == null || !obj.HasStateAuthority) return;

            if (Vector3.Distance(_rigidbody.position, _returnTo.position) <= _arriveDistance)
            {
                // Land exactly on the target rather than stopping wherever the last step left
                // it. The arrive distance says when to stop travelling, not where it is allowed
                // to come to rest -- stopping short leaves it parked up to that far off centre,
                // and since auto-recall uses the same threshold nothing ever corrects it. The
                // object drifts a little further from centre with every nudge.
                _rigidbody.position = _returnTo.position;
                StopReturning();
                return;
            }

            _rigidbody.position = Vector3.MoveTowards(_rigidbody.position, _returnTo.position,
                                                      _speed * Time.fixedDeltaTime);
        }

        private void OnValidate()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkBridge>();
            if (_grabInteractable == null) _grabInteractable = GetComponent<XRGrabInteractable>();
        }
    }
}
