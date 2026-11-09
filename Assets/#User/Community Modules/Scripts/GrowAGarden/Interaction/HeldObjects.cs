using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Vulps.GrowAGarden.Interaction
{
    /// <summary>Lets go of everything of ours the local player is holding, so it stays behind when they are moved.</summary>
    public static class HeldObjects
    {
        /// <summary>Releases every held IHeldObject (disabling its grab for a frame forces the deselect); returns how many.</summary>
        public static int ReleaseAll(MonoBehaviour runner)
        {
            var grabbables = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
            int dropped = 0;
            foreach (XRGrabInteractable grab in grabbables)
            {
                if (grab == null || !grab.isSelected) continue;
                if (grab.GetComponent<IHeldObject>() == null) continue;   // only ours; never the player's own tools
                runner.StartCoroutine(ReleaseThenRearm(grab));
                dropped++;
            }
            return dropped;
        }

        private static IEnumerator ReleaseThenRearm(XRGrabInteractable grab)
        {
            grab.enabled = false;
            yield return null;
            if (grab != null) grab.enabled = true;
        }
    }
}
