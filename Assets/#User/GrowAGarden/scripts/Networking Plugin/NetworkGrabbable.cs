using Fusion.Addons.Physics;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    /// <summary>
    ///     This component is used to transfer network control of an object to the last player that grabbed it.
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
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _lastScale;
        private bool _isKinematic;

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
            _isKinematic = _rigidbody.isKinematic;

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
            _rigidbody.isKinematic = _isKinematic;

            // # Setup to correct the non kinematic state
            float startTime = Time.time;
            while (!_networkRigidbody.HasStateAuthority && Time.time - startTime < TIMEOUT)
            {
                _rigidbody.isKinematic = _isKinematic;
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

            _rigidbody.isKinematic = _isKinematic;
            if (_ungrabbed)
            {
                _rigidbody.transform.SetPositionAndRotation(_lastPosition, _lastRotation);
                _rigidbody.transform.localScale = _lastScale;
            }
            Debug.Log($"[{nameof(NetworkGrabbable)}] Object control received");
        }

        private void OnHover(HoverEnterEventArgs arg)
        {
            if (!_networkRigidbody.HasStateAuthority)
            {
                _rigidbody.isKinematic = _isKinematic; // Sketchy but fix kinematic issue // TODO: Find better solution
            }
        }

        private void OnUngrabbed(SelectExitEventArgs arg)
        {
            _ungrabbed = true;
        }

        private void OnGrabbed(SelectEnterEventArgs arg)
        {
            if (!_networkRigidbody.HasStateAuthority)
            {
                _rigidbody.isKinematic = _isKinematic;
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
