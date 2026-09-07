using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// A fixture that displays one object and tidies it. It knows nothing about seeds, crops,
    /// prices or shops — a barrow, a scroll barrel, a shed shelf and an exploration cache are all
    /// this component with something else deciding what goes in them.
    ///
    /// It is the anchor side of what ReturnableEntity and AlignableEntity are the object side of,
    /// and it belongs to the same family: it writes nothing until it is told to act.
    ///
    /// Its one piece of real knowledge is geometry. OnTriggerExit cannot be trusted — Unity
    /// re-creates the PhysX actor whenever isKinematic, detectCollisions or a collider's enabled
    /// flag changes, and fires exits for everything it was overlapping with nothing having moved.
    /// Grabbing a seed therefore raised an exit while it sat motionless on the shelf, and the
    /// purchase path charged for it. Turning that untrustworthy event into a fact by checking
    /// where the object actually is, is the reason this class owns the trigger at all.
    /// </summary>
    public class Socket : MonoBehaviour
    {
        [Tooltip("The volume that decides whether something is still here. Defaults to the " +
                 "collider on this object.")]
        [SerializeField] private Collider _bounds;

        [Tooltip("How long to wait to be given ownership of a held object before giving up on " +
                 "bringing it home. A safety net, not a normal path.")]
        [SerializeField] private float _authorityTimeout = 2f;

        /// <summary>Something is in the volume and nothing is held. Whoever owns this socket
        /// decides whether it is worth holding, and calls Hold() if so.</summary>
        public event Action<GameObject> Entered;

        /// <summary>The held object has genuinely left the volume. Nothing has been decided yet
        /// — this is the alarm, not the verdict, and the socket still holds it. Raised on every
        /// client, because the decision that follows is split between the holder and the master.</summary>
        public event Action<GameObject> Leaving;

        /// <summary>The socket has let go. It is empty and wants restocking.</summary>
        public event Action<GameObject> Left;

        public GameObject Held { get; private set; }
        public bool IsEmpty => Held == null;

        private Collider[] _propColliders;


        /// <summary>
        /// Takes an object onto the shelf.
        ///
        /// Ignoring prop collisions runs on every client: Physics.IgnoreCollision is a local
        /// setting, so skipping it on proxies leaves the stock bouncing around inside the barrow
        /// on everyone else's screen. Arming recall and realign is bookkeeping about where the
        /// object *should* be, so only the master does it — armed everywhere, each client would
        /// independently decide the object had wandered while only the clearing half is
        /// authority-gated, which is how a bought seed gets dragged back to the shop.
        /// </summary>
        public void Hold(GameObject held)
        {
            if (held == null) return;
            if (Held == held) { IgnorePropCollisions(held, true); return; }   // re-arming, not a new hold

            Held = held;
            IgnorePropCollisions(held, true);

            if (!SceneNetworking.IsMasterClient) return;

            var ret = held.GetComponent<ReturnableEntity>();
            if (ret != null)
            {
                ret.SetReturnTarget(transform);
                ret.SetAutoRecall(true);   // a nudge tidies itself up
            }

            // Whatever it is facing now is correct — the socket never names a rotation, because
            // whoever placed it here has already set it to this transform's.
            var align = held.GetComponent<AlignableEntity>();
            if (align != null)
            {
                align.CaptureAlignTarget();
                align.SetAutoRealign(true);
            }
        }

        /// <summary>
        /// Lets go. The object belongs to someone else now, so every claim on it is dropped and
        /// the socket announces that it is empty.
        /// </summary>
        public void Release()
        {
            GameObject released = Held;
            if (released == null) return;

            Held = null;
            IgnorePropCollisions(released, false);

            if (SceneNetworking.IsMasterClient)
            {
                var ret = released.GetComponent<ReturnableEntity>();
                if (ret != null)
                {
                    ret.SetAutoRecall(false);
                    ret.ClearReturnTarget();
                }

                var align = released.GetComponent<AlignableEntity>();
                if (align != null)
                {
                    align.SetAutoRealign(false);
                    align.ClearAlignTarget();
                }
            }

            Left?.Invoke(released);
        }

        /// <summary>
        /// Brings the held object home — taking ownership of it first.
        ///
        /// Recall moves a rigidbody, and a client without state authority cannot move a networked
        /// one: ReturnableEntity.FixedUpdate bails at `if (!hasAuthority) return;`. The socket
        /// used to suspend its own copy's collisions and grab, wait for a trip that could never
        /// start, and time out after twenty seconds — during which its copy was untouchable while
        /// the player carried the real one away. A shelf has to own what it puts back on itself.
        /// </summary>
        public void Recall()
        {
            if (Held == null) return;

            var ret = Held.GetComponent<ReturnableEntity>();
            if (ret == null) return;

            ret.SetReturnTarget(transform);
            StartCoroutine(TakeOwnershipAndRecall(Held, ret));
        }

        private IEnumerator TakeOwnershipAndRecall(GameObject held, ReturnableEntity ret)
        {
            NetworkObject obj = held.GetComponent<NetworkObject>();
            if (obj == null) yield break;

            if (!obj.HasStateAuthority)
            {
                obj.RequestStateAuthority();
                float waited = 0f;
                while (!obj.HasStateAuthority && waited < _authorityTimeout)
                {
                    yield return null;
                    waited += Time.deltaTime;
                }
            }

            if (!obj.HasStateAuthority)
            {
                Logger.Warn($"TakeOwnershipAndRecall() '{gameObject.name}' — could not take ownership of '{held.name}' within {_authorityTimeout}s; leaving it where it is");
                yield break;
            }

            ret.Recall(shouldIgnoreCollisions: true);

            var align = held.GetComponent<AlignableEntity>();
            if (align != null) align.Realign();
        }

        // ── Geometry ──────────────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!IsEmpty) return;

            GameObject candidate = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject
                : other.gameObject;

            Entered?.Invoke(candidate);
        }

        private void OnTriggerExit(Collider other)
        {
            if (Held == null) return;

            GameObject leaving = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject
                : other.gameObject;

            if (leaving != Held) return;
            if (!HasLeft(leaving)) return;   // the actor was re-created; nothing moved

            Leaving?.Invoke(leaving);
        }

        /// <summary>Whether the object is genuinely outside the volume, rather than the trigger
        /// event having come from a physics-state toggle. See the class note.</summary>
        public bool HasLeft(GameObject candidate)
        {
            if (_bounds == null) _bounds = GetComponent<Collider>();
            if (_bounds == null) return true;   // no volume to test against; trust the event
            return !_bounds.bounds.Contains(candidate.transform.position);
        }

        // ── The prop this socket sits in ──────────────────────────────────────────

        /// <summary>
        /// Lets the held object and the prop it sits in pass through each other.
        ///
        /// The anchor is inside the prop's mesh — the barrow collider spans roughly y=0.4 to 1.8
        /// and the anchor sits at 1.16 — so stock hangs in a bowl and any nudge bounced it off the
        /// inside walls once it became physical. The tether is anchored to this transform rather
        /// than to the prop, so removing the collision between them changes nothing about where
        /// the object is held.
        ///
        /// Done per collider pair rather than through layers: the physics collision matrix lives
        /// in ProjectSettings, which does not travel inside an exported asset bundle, so a
        /// layer-based rule would work in the Editor and quietly do nothing in-world.
        ///
        /// Re-applied on every Hold() because Unity drops ignored pairs when a collider is
        /// disabled and re-enabled, which a visual state change does.
        /// </summary>
        private void IgnorePropCollisions(GameObject held, bool ignore)
        {
            if (_propColliders == null) CachePropColliders();
            if (_propColliders.Length == 0 || held == null) return;

            foreach (Collider heldCol in held.GetComponentsInChildren<Collider>(true))
            {
                if (heldCol.isTrigger) continue;   // triggers do not collide anyway
                foreach (Collider propCol in _propColliders)
                {
                    if (propCol == null) continue;
                    Physics.IgnoreCollision(heldCol, propCol, ignore);
                }
            }
        }

        private void CachePropColliders()
        {
            Transform root = transform.parent != null ? transform.parent : transform;
            var found = new List<Collider>();
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;                              // our own volume
                if (c.attachedRigidbody != null) continue;              // something being held, not the prop
                found.Add(c);
            }
            _propColliders = found.ToArray();
        }

        private void OnValidate()
        {
            if (_bounds == null) _bounds = GetComponent<Collider>();
        }
    }
}
