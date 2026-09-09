using Fusion.Addons.Physics;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    /// <summary>
    ///     This component is used to transfer network control of an object to the last player that grabbed it.
    ///
    ///     DIVERGES from the CommunityModules original: the isKinematic caching has been removed.
    ///     CM captured Rigidbody.isKinematic once in Awake and re-asserted that value on hover, on
    ///     grab, and every physics frame while waiting for authority, to stop Fusion overwriting it
    ///     on a proxy. That works only while the intended value never changes after startup. Seeds
    ///     change physical mode as they move through their lifecycle, so the cached value became a
    ///     lie that got written into live grabs. KinematicController now owns isKinematic and asks
    ///     an IKinematicSource for the live answer. Re-apply this deletion if CM is ever re-vendored.
    ///
    ///     SUSPECTED, NOT CONFIRMED — this deletion may be behind the long-standing "I can't see
    ///     what a peer is carrying" report, which has never been pinned down and appeared somewhere
    ///     in the run of locally-tested-only work around the Seed/Plant/Produce split. The two
    ///     components disagree about Fusion: CM re-asserted isKinematic on proxies every physics
    ///     frame *to stop Fusion overwriting it on a proxy*, while KinematicController.Apply()
    ///     deliberately returns before writing it without authority, because *writing it on a proxy
    ///     would be undone on the next sync*. Both cannot be right, and deleting CM's version left
    ///     nothing writing isKinematic on a proxy at all. Seed.ShouldBeKinematic is permanently
    ///     false, yet the 2026-09-08 client log splits perfectly along authority: 141
    ///     ReportLooseState samples with authority=True are kinematic=False, and all 3 with
    ///     authority=False are kinematic=True. If a proxy's copy is kinematic when it should not
    ///     be, its rigidbody stops responding the way the owner's does and the object can sit still
    ///     on that client while the carrier walks off with it — which is the symptom. Not yet
    ///     tested; the check is a ReportLooseState-style sample on a proxy while a peer carries
    ///     something, and the fix, if it is this, is to give proxies a writer again rather than to
    ///     restore CM's cached value.
    ///
    ///     There was a SECOND divergence, added undocumented and reverted 2026-09-09: OnHover
    ///     called RequestControl(), so authority transferred on reach rather than on grab. It was
    ///     added to spend the ~200ms transfer during the reach instead of during the grab, which
    ///     did remove a flicker. But GetStateAuthority() ends by restoring a cached pose and scale
    ///     whenever _ungrabbed is set — and _ungrabbed is never reset to false, so from an object's
    ///     first release onward every run of that coroutine teleports it to a stale pose. Upstream
    ///     that block can only run during an actual grab; firing it from hover meant merely
    ///     reaching towards a dropped scroll or deed snapped it back to where it was last picked
    ///     up, and scaled it to _lastScale — which Awake never initialises, so zero, i.e. invisible.
    ///     Reverted to CM's grab-only transfer to test that. The three defects in
    ///     GetStateAuthority() are still reachable through OnGrabbed, just far more narrowly.
    /// </summary>
    [HelpURL("https://incrediworlds.gitbook.io/somnium-space-dendoc/worlds-creation/community-modules/community-networking")]
    public class NetworkGrabbable : MonoBehaviour
    {
        [Header("Version [2026, 04, 23]")]
        [SerializeField] private XRGrabInteractable _grabInteracable;
        [SerializeField] private NetworkRigidbody3D _networkRigidbody;
        [SerializeField] private Rigidbody _rigidbody;

        private const float TIMEOUT = 2.0f;

        private bool _ungrabbed = false;
        // Reverted with the hover transfer — read only by OnHover. See the class note.
        //private IHeldObject _held;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _lastScale;

        private void Awake()
        {
            if (_grabInteracable == null)
            {
                Debug.LogError($"[{nameof(NetworkGrabbable)}] GrabInteracable reference is Null.");
                return;
            }

            // Get components in case they have not been assigned in the inspector
            if (_networkRigidbody == null)
                _networkRigidbody = _grabInteracable.GetComponent<NetworkRigidbody3D>();

            // Validate components
            if (_rigidbody == null || _networkRigidbody == null)
            {
                Debug.LogError($"[{nameof(NetworkGrabbable)}] An inspector reference is Null.");
                return;
            }

            _lastPosition = _rigidbody.transform.position;
            _lastRotation = _rigidbody.transform.rotation;
            // CM leaves _lastScale unset here, so it is Vector3.zero until a caching pass runs —
            // and the restore block below applies it unconditionally. An object that reached that
            // block without ever caching was scaled to nothing, i.e. it vanished. Initialising it
            // cannot make anything worse; the underlying _ungrabbed bug is described in the class
            // note and is deliberately left alone so this upload tests one thing.
            _lastScale = _rigidbody.transform.localScale;
            //_held = GetComponent<IHeldObject>();   // reverted with the hover transfer

            // Connect Grab event
            _grabInteracable.firstSelectEntered.AddListener(OnGrabbed);
            _grabInteracable.firstHoverEntered.AddListener(OnHover);
            _grabInteracable.lastSelectExited.AddListener(OnUngrabbed);
        }

        private void OnDestroy()
        {
            if (_grabInteracable != null)
            {
                // Disconnect Grab event
                _grabInteracable.firstSelectEntered.RemoveListener(OnGrabbed);
                _grabInteracable.firstHoverEntered.RemoveListener(OnHover);
                _grabInteracable.lastSelectExited.RemoveListener(OnUngrabbed);
            }
        }

        // Request control of the object over networking
        public void RequestControl()
        {
            if (_networkRigidbody != null && !_networkRigidbody.HasStateAuthority)
            {
                StartCoroutine(GetStateAuthority());
            }
        }

        private IEnumerator GetStateAuthority()
        {
            _networkRigidbody.Object.RequestStateAuthority();

            float startTime = Time.time;
            while (!_networkRigidbody.HasStateAuthority && Time.time - startTime < TIMEOUT)
            {
                if (!_ungrabbed)
                {
                    _lastPosition = _rigidbody.transform.position;
                    _lastRotation = _rigidbody.transform.rotation;
                    _lastScale = _rigidbody.transform.localScale;
                }
                yield return new WaitForFixedUpdate();
            }

            // GrowAGarden divergence from CM V3: instrumentation, no behaviour change.
            //
            // CM's timeout branch sat here, inside the loop, testing the same condition the while
            // had just used to let us in — Time.time cannot advance between the two, so it was
            // unreachable and "Timeout, can't get object control" has never once been printed.
            // The loop exits through its own condition on success AND on timeout, and both fell
            // through to an unconditional "Object control received". Every authority transfer in
            // the project has therefore been reported as a success whether or not it happened,
            // which is why "does a peer ever get authority" has stayed unanswerable across five
            // rounds of diagnosis. Removing the dead branch changes nothing; the pair of logs
            // below is the whole point.
            bool granted = _networkRigidbody.HasStateAuthority;
            float waitedMs = (Time.time - startTime) * 1000f;

            if (_ungrabbed)
            {
                _rigidbody.transform.SetPositionAndRotation(_lastPosition, _lastRotation);
                _rigidbody.transform.localScale = _lastScale;
            }

            if (granted)
            {
                Debug.Log($"[{nameof(NetworkGrabbable)}] '{name}' — control received after {waitedMs:F0}ms, restored={_ungrabbed}");
            }
            else
            {
                Debug.LogWarning($"[{nameof(NetworkGrabbable)}] '{name}' — NO CONTROL after {waitedMs:F0}ms; " +
                                 $"authority={_networkRigidbody.Object.StateAuthority} local={_networkRigidbody.Runner.LocalPlayer} " +
                                 $"master={_networkRigidbody.Runner.IsSharedModeMasterClient} restored={_ungrabbed}");
            }
        }

        /// <summary>
        /// Does nothing, deliberately — this is CM V3's OnHover with its one statement removed by
        /// the isKinematic divergence, so hovering no longer touches the network at all.
        ///
        /// The listener stays registered rather than being unhooked, so the seam is visible and
        /// restoring the hover transfer is a one-line change if the retest exonerates it. What it
        /// used to do, and why it was reverted, is in the class note.
        /// </summary>
        private void OnHover(HoverEnterEventArgs arg)
        {
            //if (_held != null && !string.IsNullOrEmpty(_held.HolderId)) return;
            //RequestControl();
        }

        private void OnUngrabbed(SelectExitEventArgs arg)
        {
            _ungrabbed = true;
        }

        private void OnGrabbed(SelectEnterEventArgs arg)
        {
            if (!_networkRigidbody.HasStateAuthority)
            {
                RequestControl();
            }
        }

        [ContextMenu("- Auto Assign")]
        // Executed in editor to ensure that the required components are assigned.
        private void OnValidate()
        {
            if (_grabInteracable == null)
            {
                _grabInteracable = GetComponent<XRGrabInteractable>();
            }
            if (_networkRigidbody == null)
            {
                _networkRigidbody = GetComponent<NetworkRigidbody3D>();
            }
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }
        }
    }
}
