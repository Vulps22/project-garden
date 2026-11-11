using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Vulps.GrowAGarden.Crops;
using Vulps.GrowAGarden.Interaction;
using Vulps.GrowAGarden.Modules;
using Vulps.GrowAGarden.Notifications;
using Vulps.GrowAGarden.Ownership;
using Vulps.GrowAGarden.Settings;
using Vulps.GrowAGarden.Tutorial;

namespace Vulps.GrowAGarden.Desktop
{
    /// <summary>
    /// A 2D player's hands and the K key. What they look at within reach is highlighted blue with "Press K to …";
    /// what they take sits on one of two sockets above their head, where everyone sees it; 1 and 2 pick a hand, K
    /// drops it, Z eats it or puts a splif in the mouth. A VR player never sees any of it. See docs/2d-players.md.
    /// </summary>
    public partial class DesktopInteraction : MonoBehaviour
    {
        [Tooltip("The highlight drawn over what K would act on: a shell with the GrowAGarden/TutorialGlow shader, in blue.")]
        [SerializeField] private Material _highlight;

        [Tooltip("The HUD's and the prompt's font.")]
        [SerializeField] private TMP_FontAsset _font;

        [Tooltip("How far from the head something can be acted on, in metres.")]
        [SerializeField] private float _reach = 3f;

        [Tooltip("How high above the head the two carried items sit, in metres.")]
        [SerializeField] private float _above = 0.45f;

        [Tooltip("How far apart the two carried items sit, in metres.")]
        [SerializeField] private float _apart = 0.5f;

        [Tooltip("How fast an item settles onto its socket, per second.")]
        [SerializeField] private float _settle = 8f;

        [Tooltip("How fast an item is carried to where it is put, in metres per second.")]
        [SerializeField] private float _putSpeed = 6f;

        [Tooltip("Where a dropped item is let go: this far ahead of the head, in metres...")]
        [SerializeField] private float _dropAhead = 0.8f;

        [Tooltip("...and this far below it.")]
        [SerializeField] private float _dropBelow = 0.6f;

        private enum Place { Overhead, Mouth, Moving }

        /// <summary>What K would act on now.</summary>
        private struct Target
        {
            public string Verb;
            public XRGrabInteractable Grab;
            public SettingsButton Button;
            public IItemReceiver Receiver;
            public PlantSlot Slot;
            public GameObject Shown;
            public bool IsSome => Verb != null;
        }

        private static readonly RaycastHit[] _hits = new RaycastHit[64];
        private static readonly Collider[] _overlaps = new Collider[32];

        private readonly HandSocket[] _hands = new HandSocket[2];
        private readonly Transform[] _attach = new Transform[2];
        private readonly Place[] _place = new Place[2];
        private readonly Vector3[] _offset = new Vector3[2];
        private readonly Vector3[] _moveTo = new Vector3[2];
        private readonly float[] _movingSince = new float[2];
        private readonly System.Action<XRGrabInteractable>[] _arrived = new System.Action<XRGrabInteractable>[2];
        private readonly bool[] _had = new bool[2];
        private int _selected;
        private bool _built;
        private Transform _ownRoot;
        private Collider[] _own = System.Array.Empty<Collider>();
        private Target _target;

        private void Update()
        {
            Transform head = PlayerManager.LocalPlayerHead;
            if (head == null) return;                                    // not spawned: VR or 2D is not asked before then
            if (PlayerManager.LocalIsVR) { enabled = false; return; }    // a VR player never sees any of this

            Build();
            RefreshOwnColliders();
            ForgetLost();

            int hand = DesktopControls.HandPressed;
            if (hand >= 0 && hand != _selected) { _selected = hand; Logger.Info($"Update() '{gameObject.name}' — hand {hand + 1} selected ({NameOf(HeldIn(hand))})"); }

            PlaceHands(head);
            _target = FindTarget(head);
            ShowTarget(_target, head);

            if (DesktopControls.InteractPressed) Act(_target, head);
            if (DesktopControls.EatPressed) ToMouth();

            UpdateHud();
        }

        private void OnDestroy() => TearDown();

        // ── Hands ─────────────────────────────────────────────────────────────────

        private void Build()
        {
            if (_built) return;
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(i == 0 ? "GAG_Hand1" : "GAG_Hand2");
                go.transform.SetParent(transform, false);
                var attach = new GameObject("Attach").transform;
                attach.SetParent(go.transform, false);
                _attach[i] = attach;
                _hands[i] = go.AddComponent<HandSocket>();
                _hands[i].attachTransform = attach;
                _place[i] = Place.Overhead;
            }
            BuildHud();
            _built = true;
            Logger.Info($"Build() '{gameObject.name}' — 2D hands and HUD ready");
        }

        private void TearDown()
        {
            for (int i = 0; i < 2; i++)
            {
                if (_hands[i] != null) Destroy(_hands[i].gameObject);
                _hands[i] = null;
                _attach[i] = null;
                _arrived[i] = null;
            }
            ClearHighlight();
            PlantingBeam.Mark(null, null);
            DestroyHud();
            _built = false;
        }

        /// <summary>The item a hand holds, or null.</summary>
        private XRGrabInteractable HeldIn(int hand)
        {
            HandSocket socket = hand >= 0 && hand < 2 ? _hands[hand] : null;
            return socket != null && socket.hasSelection ? socket.firstInteractableSelected as XRGrabInteractable : null;
        }

        private bool IsInMyHands(XRGrabInteractable grab) => grab != null && (HeldIn(0) == grab || HeldIn(1) == grab);

        /// <summary>A hand whose item went (sold, planted, eaten, taken back) is empty again; if it was selected, the selection moves to the other.</summary>
        private void ForgetLost()
        {
            for (int i = 0; i < 2; i++)
            {
                bool has = HeldIn(i) != null;
                bool lost = _had[i] && !has;
                _had[i] = has;
                if (has) continue;
                _place[i] = Place.Overhead;
                _arrived[i] = null;
                if (lost && i == _selected && HeldIn(1 - i) != null) _selected = 1 - i;
            }
        }

        private void PlaceHands(Transform head)
        {
            Quaternion yaw = Yaw(head);
            for (int i = 0; i < 2; i++)
            {
                Transform attach = _attach[i];
                if (attach == null) continue;
                Vector3 goal = Goal(i, head, yaw);
                if (_place[i] == Place.Moving)
                {
                    attach.position = Vector3.MoveTowards(attach.position, goal, _putSpeed * Time.deltaTime);
                    if ((attach.position - goal).sqrMagnitude < 0.0025f || Time.time - _movingSince[i] > 1.5f) Arrive(i, head, yaw);
                }
                else
                {
                    _offset[i] *= Mathf.Exp(-_settle * Time.deltaTime);
                    attach.position = goal + _offset[i];
                }
                attach.rotation = yaw;
            }
        }

        private Vector3 Goal(int hand, Transform head, Quaternion yaw)
        {
            switch (_place[hand])
            {
                case Place.Mouth: return PlayerManager.LocalMouthPosition ?? head.position;
                case Place.Moving: return _moveTo[hand];
                default: return head.position + Vector3.up * _above + yaw * Vector3.right * (hand == 0 ? -_apart : _apart) * 0.5f;
            }
        }

        private void SetPlace(int hand, Place place, Transform head)
        {
            _place[hand] = place;
            if (place != Place.Moving && _attach[hand] != null) _offset[hand] = _attach[hand].position - Goal(hand, head, Yaw(head));
        }

        private void Arrive(int hand, Transform head, Quaternion yaw)
        {
            System.Action<XRGrabInteractable> arrived = _arrived[hand];
            XRGrabInteractable item = HeldIn(hand);
            _arrived[hand] = null;
            SetPlace(hand, Place.Overhead, head);   // back up, unless what it arrived for takes it
            if (arrived != null && item != null) arrived(item);
        }

        /// <summary>Carries a hand's item to a point, then calls <paramref name="arrived"/> with it there.</summary>
        private void Put(int hand, Vector3 point, System.Action<XRGrabInteractable> arrived)
        {
            _place[hand] = Place.Moving;
            _moveTo[hand] = point;
            _movingSince[hand] = Time.time;
            _arrived[hand] = arrived;
        }

        private static Quaternion Yaw(Transform head)
        {
            Transform view = DesktopControls.View;
            Vector3 forward = Vector3.ProjectOnPlane(view != null ? view.forward : head.forward, Vector3.up);
            return forward.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        // ── Acting ────────────────────────────────────────────────────────────────

        private void Act(Target target, Transform head)
        {
            int hand = _selected;
            XRGrabInteractable held = HeldIn(hand);

            if (target.Grab != null) { Take(target.Grab, head); return; }
            if (target.Button != null) { target.Button.Press(); return; }
            if (held == null) return;

            if (target.Receiver != null)
            {
                IItemReceiver receiver = target.Receiver;
                Logger.Info($"Act() '{gameObject.name}' — {target.Verb} '{held.name}' at '{((Component)receiver).name}'");
                Put(hand, receiver.ReceivePoint, item => receiver.Receive(item.gameObject));
                return;
            }

            if (target.Slot != null && held.TryGetComponent(out Seed seed))
            {
                PlantSlot slot = target.Slot;
                Logger.Info($"Act() '{gameObject.name}' — planting '{seed.name}' in '{slot.name}'");
                Put(hand, slot.transform.position + Vector3.up * 0.1f, item => slot.RequestPlant(seed));
                return;
            }

            Logger.Info($"Act() '{gameObject.name}' — dropping '{held.name}'");
            Vector3 at = head.position + Yaw(head) * Vector3.forward * _dropAhead + Vector3.down * _dropBelow;
            Put(hand, at, item => Release(hand, item));
        }

        /// <summary>Takes something into the selected hand, or the other if that one is full; both full says so.</summary>
        private void Take(XRGrabInteractable grab, Transform head)
        {
            int hand = HeldIn(_selected) == null ? _selected : HeldIn(1 - _selected) == null ? 1 - _selected : -1;
            if (hand < 0) { Notifier.Push("Your hands are full"); return; }

            XRInteractionManager manager = grab.interactionManager;
            HandSocket socket = _hands[hand];
            if (manager == null || socket == null) { Logger.Warn($"Take() '{gameObject.name}' — '{grab.name}' has no interaction manager; cannot take it"); return; }
            if (socket.interactionManager != manager) socket.interactionManager = manager;

            _attach[hand].SetPositionAndRotation(grab.transform.position, Yaw(head));   // it is seen to rise from where it was
            if (!manager.IsSelectPossible(socket, grab)) { Logger.Info($"Take() '{gameObject.name}' — '{grab.name}' refused the grab"); return; }
            manager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)grab);
            if (!socket.IsSelecting((IXRSelectInteractable)grab)) { Logger.Warn($"Take() '{gameObject.name}' — '{grab.name}' was not taken"); return; }

            _selected = hand;
            SetPlace(hand, Place.Overhead, head);
            Logger.Info($"Take() '{gameObject.name}' — '{grab.name}' into hand {hand + 1}");
        }

        private void Release(int hand, XRGrabInteractable item)
        {
            HandSocket socket = _hands[hand];
            if (socket == null || item == null || !socket.IsSelecting((IXRSelectInteractable)item)) return;
            socket.interactionManager.SelectExit((IXRSelectInteractor)socket, (IXRSelectInteractable)item);
        }

        /// <summary>Z: a selected edible goes to the mouth and is eaten there; a splif goes to the mouth and back up again.</summary>
        private void ToMouth()
        {
            XRGrabInteractable held = HeldIn(_selected);
            Transform head = PlayerManager.LocalPlayerHead;
            if (held == null || head == null) return;

            if (_place[_selected] == Place.Mouth) { SetPlace(_selected, Place.Overhead, head); return; }
            bool edible = held.GetComponentInChildren<EdibleModule>() != null && held.TryGetComponent(out Produce produce) && produce.IsHarvested;
            bool smokeable = held.GetComponent<SmokeableEntity>() != null;
            if (!edible && !smokeable) { Logger.Info($"ToMouth() '{gameObject.name}' — '{held.name}' is neither eaten nor smoked"); return; }

            SetPlace(_selected, Place.Mouth, head);
            Logger.Info($"ToMouth() '{gameObject.name}' — '{held.name}' to the mouth ({(edible ? "eating" : "smoking")})");
        }

        // ── Looking ───────────────────────────────────────────────────────────────

        private void RefreshOwnColliders()
        {
            Transform root = PlayerManager.LocalPlayerRig.Root;
            if (root == _ownRoot) return;
            _ownRoot = root;
            _own = root != null ? root.GetComponentsInChildren<Collider>(true) : System.Array.Empty<Collider>();
        }

        private bool IsOwn(Collider c)
        {
            for (int i = 0; i < _own.Length; i++) if (_own[i] == c) return true;
            Rigidbody body = c.attachedRigidbody;
            return body != null && (IsInMyHands(body.GetComponent<XRGrabInteractable>()));
        }

        /// <summary>The first thing on the line of sight, within reach of the head, that K would act on; else a zone the body stands in.</summary>
        private Target FindTarget(Transform head)
        {
            XRGrabInteractable held = HeldIn(_selected);
            Transform view = DesktopControls.View;
            Vector3 origin = view != null ? view.position : head.position;
            Vector3 direction = view != null ? view.forward : head.forward;
            float behind = Vector3.Distance(origin, head.position);   // a third-person camera looks past the player

            int n = Physics.RaycastNonAlloc(origin, direction, _hits, behind + _reach, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(_hits, 0, n, HitDistance.Instance);
            for (int i = 0; i < n; i++)
            {
                Collider c = _hits[i].collider;
                if (c == null || IsOwn(c)) continue;
                if (_hits[i].distance < behind * 0.8f) continue;                                   // between the camera and the player
                if (Vector3.Distance(_hits[i].point, head.position) > _reach) continue;

                if (TryAct(c, _hits[i].point, held, out Target target)) return target;
                if (!c.isTrigger) break;                                                            // solid things hide what is behind them
            }

            if (held != null && PlayerManager.TryGetLocalBody(out Vector3 centre, out float radius, out float height))
            {
                int m = Physics.OverlapCapsuleNonAlloc(centre + Vector3.up * (height * 0.5f - radius), centre - Vector3.up * (height * 0.5f - radius),
                                                       radius, _overlaps, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < m; i++)
                    if (_overlaps[i] != null && !IsOwn(_overlaps[i]) && TryReceiver(_overlaps[i], held, out Target target)) return target;
            }

            return held != null ? new Target { Verb = "Drop" } : default;
        }

        private bool TryAct(Collider c, Vector3 point, XRGrabInteractable held, out Target target)
        {
            target = default;

            XRGrabInteractable grab = c.attachedRigidbody != null ? c.attachedRigidbody.GetComponent<XRGrabInteractable>() : c.GetComponentInParent<XRGrabInteractable>();
            if (grab != null && CanTake(grab))
            {
                target = new Target { Verb = VerbToTake(grab), Grab = grab, Shown = grab.gameObject };
                return true;
            }

            SettingsButton button = c.GetComponentInParent<SettingsButton>();
            if (button != null && button.isActiveAndEnabled)
            {
                target = new Target { Verb = "Use", Button = button, Shown = button.gameObject };
                return true;
            }

            if (held == null) return false;
            if (TryReceiver(c, held, out target)) return true;

            if (!c.isTrigger && held.TryGetComponent(out Seed seed) && seed.CanBePlanted && !seed.IsPlanted)
            {
                PlantSlot slot = PlantingBeam.NearestFreeSlot(c, point);
                if (slot != null)
                {
                    target = new Target { Verb = "Plant", Slot = slot, Shown = slot.gameObject };
                    return true;
                }
            }
            return false;
        }

        private static bool TryReceiver(Collider c, XRGrabInteractable held, out Target target)
        {
            target = default;
            IItemReceiver receiver = c.GetComponentInParent<IItemReceiver>();
            if (receiver == null || !(receiver is Behaviour behaviour) || !behaviour.isActiveAndEnabled) return false;
            string verb = receiver.VerbFor(held.gameObject);
            if (verb == null) return false;
            target = new Target { Verb = verb, Receiver = receiver, Shown = behaviour.gameObject };
            return true;
        }

        /// <summary>Ours, free and grabbable now: not ripening, not in someone else's hand.</summary>
        private bool CanTake(XRGrabInteractable grab)
        {
            if (!grab.isActiveAndEnabled || grab.isSelected || IsInMyHands(grab)) return false;
            IHeldObject held = grab.GetComponent<IHeldObject>();
            if (held == null) return grab.GetComponent<TutorialItem>() != null;
            return string.IsNullOrEmpty(held.HolderId) || held.HolderId == PlayerManager.LocalPlayerId;
        }

        private static string VerbToTake(XRGrabInteractable grab)
        {
            if (grab.TryGetComponent(out Seed seed) && seed.InShop && !seed.IsBought) return "Buy";
            if (grab.TryGetComponent(out Produce produce) && !produce.IsHarvested) return "Harvest";
            if (grab.TryGetComponent(out TutorialItem item)) return item.Verb;
            return "Grab";
        }

        private sealed class HitDistance : IComparer<RaycastHit>
        {
            public static readonly HitDistance Instance = new HitDistance();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
