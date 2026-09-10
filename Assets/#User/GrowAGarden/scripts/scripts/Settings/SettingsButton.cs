using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    /// <summary>
    /// One pressable thing on the settings panel.
    ///
    /// XRSimpleInteractable rather than XRGrabInteractable: a button is selected and released, not
    /// carried, and a grabbable button comes off the wall in your hand the first time anyone tries
    /// it. Both run through the same XRInteractionManager that every seed and produce in this world
    /// already uses, so the interactors that can pick a carrot can press this.
    ///
    /// Deliberately dumb — it knows it was pressed and nothing else. SettingsPanel decides what a
    /// press means, so this component has no idea the game has settings.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class SettingsButton : MonoBehaviour
    {
        /// <summary>Somebody pressed this.</summary>
        public event Action Pressed;

        [Tooltip("Seconds before the same button will register again. VR hands linger inside a " +
                 "collider, and without this a single press is several.")]
        [SerializeField] private float _repeatDelay = 0.4f;

        [SerializeField] private XRSimpleInteractable _interactable;

        private float _nextPressAt;

        private void OnValidate()
        {
            if (_interactable == null) _interactable = GetComponent<XRSimpleInteractable>();
        }

        private void Awake()
        {
            if (_interactable == null) _interactable = GetComponent<XRSimpleInteractable>();
            if (_interactable == null)
            {
                Logger.Error($"Awake() '{gameObject.name}' — no XRSimpleInteractable; this button cannot be pressed");
                return;
            }
            _interactable.selectEntered.AddListener(OnSelectEntered);

            // Hover is logged as well as select because the two failures are indistinguishable
            // from the outside: a hand that is never detected and a hand that is detected but
            // never presses both read as "the button does nothing". One line here says which.
            _interactable.hoverEntered.AddListener(OnHoverEntered);
        }

        private void OnDestroy()
        {
            if (_interactable == null) return;
            _interactable.selectEntered.RemoveListener(OnSelectEntered);
            _interactable.hoverEntered.RemoveListener(OnHoverEntered);
        }

        private void OnHoverEntered(UnityEngine.XR.Interaction.Toolkit.HoverEnterEventArgs args)
        {
            if (Time.time < _nextHoverLogAt) return;
            _nextHoverLogAt = Time.time + 1f;   // a resting hand hovers every frame

            Logger.Log($"OnHoverEntered() '{gameObject.name}' — hovered by '{args.interactorObject}'");
        }

        private float _nextHoverLogAt;

        private void OnSelectEntered(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _)
        {
            if (Time.time < _nextPressAt) return;
            _nextPressAt = Time.time + Mathf.Max(0.05f, _repeatDelay);

            Logger.Log($"OnSelectEntered() '{gameObject.name}' — pressed");
            Pressed?.Invoke();
        }
    }
}
