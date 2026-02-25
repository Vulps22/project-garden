using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;

namespace GrowAGarden
{
    public class HarvestInteractable : MonoBehaviour
    {
        public float harvestRotationThreshold = 45f;
        public float rotationSpeed = 3f;

        private XRGrabInteractable _grabInteractable;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private bool _harvested = false;
        private bool _isBeingPulled = false;
        private IXRSelectInteractor _currentInteractor;

        private void Start()
        {
            _grabInteractable = GetComponent<XRGrabInteractable>();

            Debug.Log($"[HarvestInteractable] Start() on '{gameObject.name}' — grabInteractable={(_grabInteractable != null ? "found" : "NULL")}, threshold={harvestRotationThreshold}, pos={_originalPosition}");
            if (_grabInteractable == null)
            {
                Debug.LogError($"[HarvestInteractable] '{gameObject.name}' has NO XRGrabInteractable — harvesting won't work!");
                return;
            }

            _grabInteractable.selectEntered.AddListener(OnGrabbed);
            _grabInteractable.selectExited.AddListener(OnReleased);
            Debug.Log($"[HarvestInteractable] Registered grab listeners on '{gameObject.name}'");
        }

        private void OnDestroy()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
                _grabInteractable.selectExited.RemoveListener(OnReleased);
            }
        }

        /// <summary>
        /// Call this when the plant is placed in a slot (or reclaimed from the pool)
        /// to reset all harvest state and capture the new resting transform.
        /// </summary>
        public void ResetState()
        {
            _originalPosition = transform.position;
            _originalRotation = transform.rotation;
            _harvested = false;
            _isBeingPulled = false;
            _currentInteractor = null;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            Debug.Log($"[HarvestInteractable] OnGrabbed() '{gameObject.name}' — interactor='{args.interactorObject.transform.name}', harvested={_harvested}");
            _isBeingPulled = true;
            _currentInteractor = args.interactorObject;
            // Capture resting transform at grab time so it is correct after pooling
            _originalPosition = transform.position;
            _originalRotation = transform.rotation;
        }

        private void Update()
        {
            if (!_isBeingPulled || _harvested) return;

            // Lock position
            transform.position = _originalPosition;

            // Rotate toward interactor direction
            Vector3 pullDirection = _currentInteractor.transform.position - _originalPosition;
            pullDirection.y = 0f;
            Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, pullDirection.normalized) * _originalRotation;
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

            // Check threshold
            float angle = Quaternion.Angle(_originalRotation, transform.rotation);

            // Log periodically (every ~0.5s equivalent via frame skip)
            if (Time.frameCount % 30 == 0)
                Debug.Log($"[HarvestInteractable] Pulling '{gameObject.name}' � angle={angle:F1}/{harvestRotationThreshold}, pullDir={pullDirection}");

            if (angle >= harvestRotationThreshold)
            {
                Debug.Log($"[HarvestInteractable] HARVEST THRESHOLD REACHED on '{gameObject.name}' � angle={angle:F1} >= {harvestRotationThreshold}");
                _harvested = true;
                _isBeingPulled = false;
                var slot = GetComponentInParent<PlantSlot>();
                if (slot == null)
                    Debug.LogError($"[HarvestInteractable] '{gameObject.name}' has no PlantSlot in parents � Harvest() won't be called!");
                else
                    Debug.Log($"[HarvestInteractable] Found PlantSlot '{slot.gameObject.name}' � calling Harvest()");
                slot?.Harvest();
            }
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            Debug.Log($"[HarvestInteractable] OnReleased() '{gameObject.name}' — harvested={_harvested}");
            if (_harvested) return;
            _isBeingPulled = false;
            transform.position = _originalPosition;
            transform.rotation = _originalRotation;
            Debug.Log($"[HarvestInteractable] Reset position/rotation on '{gameObject.name}'");
        }
    }
}
