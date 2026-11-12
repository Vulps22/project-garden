using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Vulps.GrowAGarden.Tutorial
{
    /// <summary>A local copy of a carryable on the tutorial platform: grabbed with XRI, hovers where it is let go, nothing networked.</summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class TutorialItem : MonoBehaviour
    {
        [SerializeField] private XRGrabInteractable _grab;

        /// <summary>A hand took hold of it.</summary>
        public event Action<TutorialItem> Grabbed;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private Vector3 _homeScale;
        private bool _homeKnown;

        public bool IsHeld => _grab != null && _grab.isSelected;

        /// <summary>What a 2D player's prompt says to take this ("Grab", "Buy", "Harvest"); the platform sets it.</summary>
        public string Verb { get; set; } = "Grab";

        private void OnValidate()
        {
            if (_grab == null) _grab = GetComponent<XRGrabInteractable>();
        }

        private void Awake()
        {
            if (_grab == null) _grab = GetComponent<XRGrabInteractable>();
            RememberHome();
        }

        private void OnEnable()
        {
            if (_grab != null) _grab.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            if (_grab != null) _grab.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void OnSelectEntered(SelectEnterEventArgs args) => Grabbed?.Invoke(this);

        private void RememberHome()
        {
            if (_homeKnown) return;
            _homePosition = transform.localPosition;
            _homeRotation = transform.localRotation;
            _homeScale = transform.localScale;
            _homeKnown = true;
        }

        /// <summary>Lets go of it if held, puts it back where it started and hides it.</summary>
        public void PutBack()
        {
            RememberHome();
            Release();
            transform.localPosition = _homePosition;
            transform.localRotation = _homeRotation;
            transform.localScale = _homeScale;
            gameObject.SetActive(false);
        }

        /// <summary>Whether a hand can take it.</summary>
        public void SetGrabbable(bool grabbable)
        {
            if (_grab == null) return;
            if (!grabbable) Release();
            _grab.enabled = grabbable;
        }

        /// <summary>Takes it out of the hand holding it.</summary>
        public void Release()
        {
            if (_grab == null || !_grab.isSelected || _grab.interactionManager == null) return;
            _grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)_grab);
        }
    }
}
