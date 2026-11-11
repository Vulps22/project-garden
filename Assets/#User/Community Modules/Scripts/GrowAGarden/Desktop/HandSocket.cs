using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Vulps.GrowAGarden.Desktop
{
    /// <summary>
    /// One of a 2D player's two hands: an XRI interactor with no reach of its own, holding what DesktopInteraction hands
    /// it at its attach point above the head. Being a real XRI select, everything that listens for a grab (holder,
    /// purchase, harvest, authority) works for a 2D player unchanged.
    /// </summary>
    public class HandSocket : XRBaseInteractor
    {
        protected override void Awake()
        {
            base.Awake();
            allowHover = false;   // it only ever holds what it is handed
        }
    }
}
