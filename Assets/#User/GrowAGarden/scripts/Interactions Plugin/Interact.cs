using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    public class Interact : MonoBehaviour
    {
        [SerializeField] private XRBaseInteractable _interactable;
        [SerializeField] private UnityEvent _onGrab = new();
        [SerializeField] private UnityEvent _onClick = new();
        [SerializeField] private UnityEvent _onHoverEnter = new();
        [SerializeField] private UnityEvent _onHoverExit = new();

        void Start()
        {
            if (_interactable != null)
            {
                _interactable.firstSelectEntered.AddListener(OnSelect);
                _interactable.activated.AddListener(OnActivate);
                _interactable.hoverEntered.AddListener(OnHoverEnter);
                _interactable.hoverExited.AddListener(OnHoverExit);
            }
        }

        private void OnSelect(SelectEnterEventArgs arg0)
        {
            _onGrab?.Invoke();
        }

        private void OnActivate(ActivateEventArgs arg0)
        {
            _onClick?.Invoke();
        }

        private void OnHoverEnter(HoverEnterEventArgs arg0)
        {
            _onHoverEnter?.Invoke();
        }

        private void OnHoverExit(HoverExitEventArgs arg0)
        {
            _onHoverExit?.Invoke();
        }

        private void OnValidate()
        {
            if(_interactable == null)
            {
                _interactable = GetComponent<XRBaseInteractable>();
            }

        }
    }
}
