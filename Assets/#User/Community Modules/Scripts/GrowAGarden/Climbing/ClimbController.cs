using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Climbing
{
    /// <summary>
    /// Climbing, ours rather than Somnium's: grip near anything under a <see cref="Climbable"/> and
    /// that point holds, so pulling the hand down lifts the body. The body capsule is off while
    /// climbing so ledges cannot snag it, and letting go stands the player on whatever is under
    /// their head.
    ///
    /// Lives on SceneManager beside FlightController, for the same reason: the player does not
    /// exist at scene load. Entirely local; Somnium replicates the avatar.
    /// </summary>
    public partial class ClimbController : MonoBehaviour
    {
        [Tooltip("Master switch.")]
        [SerializeField] private bool _enabled = true;

        [Tooltip("Layers a hand can take hold of. Only colliders under a Climbable count.")]
        [SerializeField] private LayerMask _climbableMask = ~0;

        [Tooltip("How close to a surface the hand must be to grab it, in metres.")]
        [SerializeField] private float _grabRadius = 0.12f;

        [Tooltip("How far below the head to look for somewhere to stand on letting go, in metres.")]
        [SerializeField] private float _standRayLength = 2.5f;

        [Tooltip("How flat a surface must be to stand on: 1 is level, 0 is a wall.")]
        [SerializeField] private float _standMinNormalY = 0.6f;

        [Tooltip("How far below the feet counts as standing, in metres. Off the ground, a held grip catches the first rock the hand crosses.")]
        [SerializeField] private float _groundCheck = 0.3f;

        [Header("Haptics")]

        [Tooltip("Buzz when a hand comes within reach of a hold: strength 0 to 1, and seconds.")]
        [SerializeField] private float _reachAmplitude = 0.25f;
        [SerializeField] private float _reachSeconds = 0.04f;

        [Tooltip("Buzz when a hand takes hold: strength 0 to 1, and seconds.")]
        [SerializeField] private float _grabAmplitude = 0.6f;
        [SerializeField] private float _grabSeconds = 0.12f;

        [Tooltip("Seconds between re-finding the controllers. They connect late and drop out.")]
        [SerializeField] private float _deviceScanInterval = 2f;

        /// <summary>Raised when the first hand takes hold, before the climb takes gravity. Flight lands on this.</summary>
        public static event Action ClimbStarted;

        /// <summary>Raised when the last hand lets go, after gravity and the body are handed back.</summary>
        public static event Action ClimbEnded;

        private sealed class Hand
        {
            public readonly string Name;
            public readonly bool Right;
            public Transform Anchor;
            public InputDevice Device;
            public bool WasDown;
            public bool Holding;
            public Transform HeldOn;
            public Vector3 HeldLocal;
            public Vector3 LastPosition;
            public bool HasLast;
            public bool InReach;

            public Hand(string name, bool right) { Name = name; Right = right; }
        }

        private readonly Hand _left = new Hand("left", false);
        private readonly Hand _right = new Hand("right", true);
        private readonly List<InputDevice> _devices = new();
        private static readonly Collider[] _hits = new Collider[16];
        private static readonly RaycastHit[] _rayHits = new RaycastHit[16];

        private Transform _root;
        private Transform _interactorsOf;
        private XRBaseInteractor[] _interactors = Array.Empty<XRBaseInteractor>();
        private Transform _head;
        private Hand _active;
        private bool _climbing;
        private bool _vr;
        private float _nextScanAt;

        private void OnDisable()
        {
            if (_climbing) EndClimb();
        }

        private void OnDestroy()
        {
            ClimbStarted = null;
            ClimbEnded = null;
        }

        private void Update()
        {
            if (!_enabled || !Resolve()) return;
            if (!_vr)
            {
                UpdateDesktop();
                return;
            }
            ScanDevices();

            bool airborne = !_climbing && !Grounded();
            Poll(_left, airborne);
            Poll(_right, airborne);

            if (_active != null) Pull(_active);

            Remember(_left);
            Remember(_right);
        }

        /// <summary>Where each hand ended the frame, for the next frame's sweep.</summary>
        private static void Remember(Hand hand)
        {
            if (hand.Anchor == null) { hand.HasLast = false; return; }
            hand.LastPosition = hand.Anchor.position;
            hand.HasLast = true;
        }

        /// <summary>Whether there is ground just under the feet. Root sits at the play-space origin, so look down from under the head.</summary>
        private bool Grounded()
        {
            Vector3 feet = _head != null ? new Vector3(_head.position.x, _root.position.y, _head.position.z) : _root.position;
            Vector3 from = feet + Vector3.up * 0.2f;
            return Nearest(from, Vector3.down, 0.2f + _groundCheck, out _, requireClimbable: false);
        }

        private bool Resolve()
        {
            PlayerRig rig = PlayerManager.LocalPlayerRig;
            if (!rig.IsUsable) return false;

            _root = rig.Root;
            _head = rig.Head;
            _vr = rig.IsVR;
            _left.Anchor = rig.LeftHand;
            _right.Anchor = rig.RightHand;
            if (_vr && _interactorsOf != rig.Root) FindInteractors(rig.Root);
            return true;
        }

        /// <summary>Somnium's XR interactors on this rig: the things that pick objects up. Read, never driven.</summary>
        private void FindInteractors(Transform root)
        {
            _interactorsOf = root;
            Transform search = root.parent != null ? root.parent : root;
            _interactors = search.GetComponentsInChildren<XRBaseInteractor>(true);
            var names = new System.Text.StringBuilder();
            foreach (XRBaseInteractor i in _interactors)
                names.Append($" '{i.name}'→{(Nearer(i.transform.position, _left) ? "left" : "right")}");
            Logger.Info($"FindInteractors() '{gameObject.name}' — {_interactors.Length} interactor(s):{names}");
        }

        private bool Nearer(Vector3 p, Hand hand)
        {
            Hand other = hand == _left ? _right : _left;
            return (p - hand.Anchor.position).sqrMagnitude <= (p - other.Anchor.position).sqrMagnitude;
        }

        /// <summary>
        /// Whether this hand is already carrying something: one of the rig's interactors nearer this hand
        /// has a selection. A full hand never takes hold of rock, or what it carries would be dropped
        /// and lost.
        /// </summary>
        private bool Carrying(Hand hand)
        {
            foreach (XRBaseInteractor i in _interactors)
                if (i != null && i.hasSelection && Nearer(i.transform.position, hand)) return true;
            return false;
        }

        private void ScanDevices()
        {
            if (Time.time < _nextScanAt) return;
            _nextScanAt = Time.time + Mathf.Max(0.5f, _deviceScanInterval);
            _left.Device = Find(InputDeviceCharacteristics.Left);
            _right.Device = Find(InputDeviceCharacteristics.Right);
        }

        private InputDevice Find(InputDeviceCharacteristics side)
        {
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | side, _devices);
            return _devices.Count > 0 ? _devices[0] : default;
        }

        /// <summary>Grip pressed: try to take hold. Grip held off the ground: catch the first rock the hand crosses. Grip released: let go.</summary>
        private void Poll(Hand hand, bool airborne)
        {
            bool down = hand.Device.isValid && hand.Device.TryGetFeatureValue(CommonUsages.gripButton, out bool grip) && grip;
            bool pressed = down && !hand.WasDown;
            bool released = !down && hand.WasDown;
            hand.WasDown = down;

            bool carrying = !hand.Holding && Carrying(hand);
            if (pressed && !carrying) Grab(hand, false);
            else if (down && airborne && !hand.Holding && !carrying) Grab(hand, true);
            else if (released && hand.Holding) Release(hand);

            Feel(hand, carrying);
        }

        /// <summary>A short buzz as a free hand comes within reach of a hold; nothing while holding or while it stays in reach.</summary>
        private void Feel(Hand hand, bool carrying)
        {
            bool inReach = !hand.Holding && !carrying && FindHold(hand, out _, out _, out _);
            if (inReach && !hand.InReach) Buzz(hand, _reachAmplitude, _reachSeconds);
            hand.InReach = inReach;
        }

        private static void Buzz(Hand hand, float amplitude, float seconds)
        {
            if (hand.Device.isValid) hand.Device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), Mathf.Max(0f, seconds));
        }

        private void Grab(Hand hand, bool catching)
        {
            if (!FindHold(hand, out Collider surface, out Vector3 point, out string how)) return;

            hand.Holding = true;
            hand.HeldOn = surface.transform;
            hand.HeldLocal = surface.transform.InverseTransformPoint(point);
            _active = hand;
            Buzz(hand, _grabAmplitude, _grabSeconds);

            float depth = Vector3.Distance(point, hand.Anchor.position);
            if (!_climbing) StartClimb(hand, surface, $"{how}{(catching ? ", caught mid-air" : "")}, hand {depth:F2}m from the hold");
        }

        /// <summary>
        /// Where the hand takes hold, if anywhere. Three tries, nearest first: touching a surface; the
        /// arm, head to hand, entering rock (the hand is inside it); and the hand's path since last
        /// frame crossing rock (a fall carried the rock through it between frames). The colliders are
        /// not convex, so "inside" cannot be asked directly; where the arm or the path went in is the
        /// edge that was meant.
        /// </summary>
        private bool FindHold(Hand hand, out Collider surface, out Vector3 point, out string how)
        {
            Vector3 at = hand.Anchor.position;
            point = at;
            how = "";
            surface = null;
            if (GrabbableNear(at)) return false;

            surface = SurfaceAt(at);
            if (surface != null) { how = "touch"; return true; }

            Vector3 from = _head != null ? _head.position : _root.position + Vector3.up * 1.6f;
            if (Cast(from, at, out RaycastHit hit)) { surface = hit.collider; point = hit.point; how = "arm inside rock"; return true; }

            if (hand.HasLast && Cast(hand.LastPosition, at, out hit)) { surface = hit.collider; point = hit.point; how = "swept through rock"; return true; }

            return false;
        }

        private bool Cast(Vector3 from, Vector3 to, out RaycastHit hit)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            hit = default;
            if (length < 0.001f) return false;
            return Nearest(from, d / length, length, out hit, requireClimbable: true);
        }

        /// <summary>The nearest hit along a ray that is not the player, and, if asked, is climbable.</summary>
        private bool Nearest(Vector3 from, Vector3 direction, float length, out RaycastHit nearest, bool requireClimbable)
        {
            nearest = default;
            int n = Physics.RaycastNonAlloc(from, direction, _rayHits, length, _climbableMask, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = _rayHits[i];
                if (h.collider == null || h.collider.transform.IsChildOf(_root)) continue;
                if (found && h.distance >= nearest.distance) continue;
                if (requireClimbable && h.collider.GetComponentInParent<Climbable>() == null) continue;
                nearest = h;
                found = true;
            }
            return found;
        }

        /// <summary>Anything grabbable within reach: a seed against a rock is a seed, so the climb stays out of it.</summary>
        private bool GrabbableNear(Vector3 point)
        {
            int n = Physics.OverlapSphereNonAlloc(point, _grabRadius, _hits, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
                if (_hits[i] != null && !_hits[i].transform.IsChildOf(_root) && _hits[i].GetComponentInParent<XRBaseInteractable>() != null)
                    return true;
            return false;
        }

        private void Release(Hand hand)
        {
            hand.Holding = false;
            hand.HeldOn = null;
            hand.InReach = true;   // no reach buzz for the rock it just let go of

            Hand other = hand == _left ? _right : _left;
            if (_active == hand) _active = other.Holding ? other : null;
            if (_active == null) EndClimb();
        }

        /// <summary>A climbable collider within reach of a point, or null.</summary>
        private Collider SurfaceAt(Vector3 point)
        {
            int n = Physics.OverlapSphereNonAlloc(point, _grabRadius, _hits, _climbableMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = _hits[i];
                if (c != null && !c.transform.IsChildOf(_root) && c.GetComponentInParent<Climbable>() != null) return c;
            }
            return null;
        }

        /// <summary>Moves the body so the holding hand stays where it took hold.</summary>
        private void Pull(Hand hand)
        {
            if (hand.HeldOn == null)
            {
                Release(hand);   // what it held is gone: an island unloaded under the hand
                return;
            }

            Vector3 held = hand.HeldOn.TransformPoint(hand.HeldLocal);
            _root.position += held - hand.Anchor.position;
        }

        private void StartClimb(Hand hand, Collider surface, string how)
        {
            bool body = TakeBody();
            Logger.Info($"StartClimb() '{gameObject.name}' — {hand.Name} hand took hold of '{surface.name}' at {hand.Anchor.position} ({how}); body off={body}");
        }

        /// <summary>The climb takes the body: flight lands, Somnium's gravity and walking stop, and the capsule goes off. False if there was no capsule.</summary>
        private bool TakeBody()
        {
            _climbing = true;
            ClimbStarted?.Invoke();

            PlayerManager.SetLocalGravityScale(0f);
            PlayerManager.SetLocalMovementScale(0f);
            return PlayerManager.SetLocalBodyCollision(false);
        }

        private void EndClimb()
        {
            _climbing = false;
            _active = null;
            _left.Holding = _right.Holding = false;
            _wall = null;

            string stood = StandOnLedge();

            PlayerManager.SetLocalBodyCollision(true);
            PlayerManager.SetLocalGravityScale(1f);
            PlayerManager.SetLocalMovementScale(1f);
            ClimbEnded?.Invoke();

            Logger.Info($"EndClimb() '{gameObject.name}' — let go at {(_root != null ? _root.position.ToString() : "?")}; {stood}");
        }

        /// <summary>
        /// If there is ground under the head and the feet are below it, puts the feet on it. Topping
        /// out leaves the body under the ledge, and the capsule coming back on there would push the
        /// player off rather than up.
        /// </summary>
        private string StandOnLedge()
        {
            if (_root == null) return "no rig";
            Vector3 from = _head != null ? _head.position : _root.position + Vector3.up * 1.6f;

            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, _standRayLength, _climbableMask, QueryTriggerInteraction.Ignore))
                return "nothing under the head; falling";
            if (hit.normal.y < _standMinNormalY) return $"'{hit.collider.name}' under the head is too steep to stand on";
            if (hit.point.y <= _root.position.y) return $"feet already above '{hit.collider.name}'";

            Vector3 p = _root.position;
            _root.position = new Vector3(p.x, hit.point.y, p.z);
            return $"stood on '{hit.collider.name}' at y={hit.point.y:F2} (feet were {hit.point.y - p.y:F2}m under it)";
        }

        private void OnValidate()
        {
            if (_grabRadius < 0.02f) _grabRadius = 0.02f;
            if (_standRayLength < 0.5f) _standRayLength = 0.5f;
            if (_groundCheck < 0.05f) _groundCheck = 0.05f;
            _standMinNormalY = Mathf.Clamp01(_standMinNormalY);
        }
    }
}
