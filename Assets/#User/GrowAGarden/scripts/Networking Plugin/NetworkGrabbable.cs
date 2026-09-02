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
        private IHeldObject _held;
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
            _held = GetComponent<IHeldObject>();

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
                if (Time.time - startTime >= TIMEOUT)
                {
                    Debug.LogWarning($"[{nameof(NetworkGrabbable)}] Timeout, can't get object control");
                    yield break;
                }
                yield return new WaitForFixedUpdate();
            }

            if (_ungrabbed)
            {
                _rigidbody.transform.SetPositionAndRotation(_lastPosition, _lastRotation);
                _rigidbody.transform.localScale = _lastScale;
            }
            Debug.Log($"[{nameof(NetworkGrabbable)}] Object control received");
        }

        /// <summary>
        /// Asks for control as the hand comes near, rather than waiting for the grab.
        ///
        /// The transfer is a network round trip — about 200ms from Edinburgh to a Central US
        /// master. Until it lands the grabber is still a Fusion proxy, so NetworkRigidbody3D
        /// keeps applying the owner's replicated pose every tick: the hand moves the object and
        /// the network puts it back, which reads as the object flickering or vanishing out of
        /// the hand. Asking on hover spends that trip during the reach, so the object is already
        /// this client's by the time the hand closes.
        ///
        /// Skipped when someone else is holding it — SingleHolderFilter will refuse that grab
        /// anyway, so taking control off the holder would achieve nothing but disruption.
        /// </summary>
        private void OnHover(HoverEnterEventArgs arg)
        {
            if (_held != null && !string.IsNullOrEmpty(_held.HolderId)) return;
            RequestControl();
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
