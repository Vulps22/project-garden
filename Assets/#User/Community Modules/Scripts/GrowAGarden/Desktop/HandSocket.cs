using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Vulps.GrowAGarden.Desktop
{
    /// <summary>
    /// One of a 2D player's two hands: an XRI interactor with no reach of its own, holding what DesktopInteraction hands
    /// it at its attach point above the head. Being a real XRI select, everything that listens for a grab (holder,
    /// purchase, harvest, authority) works for a 2D player unchanged. What it holds has its colliders off until let go.
    /// </summary>
    public class HandSocket : XRBaseInteractor
    {
        private readonly List<Collider> _off = new List<Collider>();

        protected override void Awake()
        {
            base.Awake();
            allowHover = false;   // it only ever holds what it is handed
        }

        // Held at the mouth or overhead, the item's colliders would shove the player's own body about
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            Transform item = args.interactableObject.transform;
            foreach (Collider c in item.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled) continue;
                c.enabled = false;
                _off.Add(c);
            }
            Logger.Info($"OnSelectEntered() '{name}' — '{item.name}' held, {_off.Count} colliders off");
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            foreach (Collider c in _off) if (c != null) c.enabled = true;
            Logger.Info($"OnSelectExited() '{name}' — '{(args.interactableObject as Object != null ? args.interactableObject.transform.name : "gone")}' let go, {_off.Count} colliders on");
            _off.Clear();
            base.OnSelectExited(args);
        }
    }
}
