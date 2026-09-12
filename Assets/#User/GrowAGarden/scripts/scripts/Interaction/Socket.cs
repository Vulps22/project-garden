using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>A fixture that displays one object and tidies it.</summary>
    public class Socket : MonoBehaviour
    {
        [Tooltip("The volume that decides whether something is still here. Defaults to the " +
                 "collider on this object.")]
        [SerializeField] private Collider _bounds;


        /// <summary>Something is in the volume and nothing is held.</summary>
        public event Action<GameObject> Entered;

        /// <summary>The held object has genuinely left the volume. Raised on every client.</summary>
        public event Action<GameObject> Leaving;

        /// <summary>The socket has let go. It is empty and wants restocking.</summary>
        public event Action<GameObject> Left;

        public GameObject Held { get; private set; }
        public bool IsEmpty => Held == null;

        private Collider[] _propColliders;


        /// <summary>Takes an object onto the shelf.</summary>
        public void Hold(GameObject held)
        {
            if (held == null) return;
            if (Held == held) { IgnorePropCollisions(held, true); return; }   // re-arming, not a new hold

            Held = held;
            IgnorePropCollisions(held, true);

            if (!PlayerManager.IsMaster) return;

            var ret = held.GetComponent<ReturnableEntity>();
            if (ret != null)
            {
                ret.SetReturnTarget(transform);
                ret.SetAutoRecall(true);   // a nudge tidies itself up
            }

            var align = held.GetComponent<AlignableEntity>();
            if (align != null)
            {
                align.CaptureAlignTarget();
                align.SetAutoRealign(true);
            }
        }

        /// <summary>Drops every claim on the held object and announces the socket is empty.</summary>
        public void Release()
        {
            GameObject released = Held;
            if (released == null) return;

            Held = null;
            IgnorePropCollisions(released, false);

            if (PlayerManager.IsMaster)
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

        /// <summary>Brings the held object home, taking state authority first.</summary>
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

            bool granted = false;
            yield return WorldManager.TakeAuthority(obj, r => granted = r);

            if (!granted)
            {
                Logger.Warn($"TakeOwnershipAndRecall() '{gameObject.name}' — could not take ownership of '{held.name}' within {WorldManager.AuthorityTimeout}s; leaving it where it is");
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

        /// <summary>Whether the object is genuinely outside the volume, rather than a stale trigger event.</summary>
        public bool HasLeft(GameObject candidate)
        {
            if (_bounds == null) _bounds = GetComponent<Collider>();
            if (_bounds == null) return true;   // no volume to test against; trust the event
            return !_bounds.bounds.Contains(candidate.transform.position);
        }

        // ── The prop this socket sits in ──────────────────────────────────────────

        /// <summary>Lets the held object and the prop it sits in pass through each other.</summary>
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
