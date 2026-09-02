using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    /// <summary>
    /// Lets a trait switch an object's grab off without depending on XRI being there.
    ///
    /// SellableEntity has to suppress the grab while a sale is pending, but it is meant to work on
    /// anything — including things that are not grabbable at all. Rather than making every such
    /// component null-check an XRGrabInteractable it may not have, the lookup lives here once.
    /// </summary>
    public readonly struct XRGrabInteractableRef
    {
        private readonly XRGrabInteractable _grab;

        public XRGrabInteractableRef(GameObject go)
        {
            _grab = go.GetComponent<XRGrabInteractable>();
        }

        public void SetEnabled(bool enabled)
        {
            if (_grab != null) _grab.enabled = enabled;
        }

        /// <summary>
        /// True only on the machine whose hand is actually on it. isSelected is not replicated,
        /// which is exactly why it is the right question here: the holder is the one client
        /// entitled to say "I am putting this down".
        /// </summary>
        public bool IsHeld => _grab != null && _grab.isSelected;
    }
}
