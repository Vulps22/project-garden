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
            Logger.Info($"Start() on '{gameObject.name}' — grabInteractable={(_grabInteractable != null ? "found" : "NULL")}, threshold={harvestRotationThreshold}");

            if (_grabInteractable == null)
            {
                Logger.Error($"'{gameObject.name}' has NO XRGrabInteractable — harvesting won't work!");
                return;
            }

            _grabInteractable.selectEntered.AddListener(OnGrabbed);
            _grabInteractable.selectExited.AddListener(OnReleased);
        }

        private void OnDestroy()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
                _grabInteractable.selectExited.RemoveListener(OnReleased);
            }
        }

        public void ResetState()
        {
            Logger.Log($"ResetState() '{gameObject.name}' — clearing harvested/pulled flags");
            _originalPosition = transform.position;
            _originalRotation = transform.rotation;
            _harvested = false;
            _isBeingPulled = false;
            _currentInteractor = null;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            Logger.Info($"OnGrabbed() '{gameObject.name}' — interactor='{args.interactorObject.transform.name}', already harvested={_harvested}");
            _isBeingPulled = true;
            _currentInteractor = args.interactorObject;
            _originalPosition = transform.position;
            _originalRotation = transform.rotation;
        }

        private void Update()
        {
            if (!_isBeingPulled || _harvested) return;

            transform.position = _originalPosition;

            Vector3 pullDirection = _currentInteractor.transform.position - _originalPosition;
            pullDirection.y = 0f;
            Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, pullDirection.normalized) * _originalRotation;
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

            float angle = Quaternion.Angle(_originalRotation, transform.rotation);

            if (angle >= harvestRotationThreshold)
            {
                Logger.Info($"HARVEST THRESHOLD REACHED on '{gameObject.name}' — angle={angle:F1} >= {harvestRotationThreshold}");
                _harvested = true;
                _isBeingPulled = false;

                var slot = GetComponentInParent<PlantSlot>();
                if (slot == null)
                    Logger.Error($"'{gameObject.name}' has no PlantSlot in parents — Harvest() won't be called!");
                else
                    slot.Harvest();
            }
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            Logger.Info($"OnReleased() '{gameObject.name}' — harvested={_harvested}");
            if (_harvested) return;
            _isBeingPulled = false;
            transform.position = _originalPosition;
            transform.rotation = _originalRotation;
            Logger.Log($"OnReleased() '{gameObject.name}' — reset to original position/rotation");
        }
    }
}
