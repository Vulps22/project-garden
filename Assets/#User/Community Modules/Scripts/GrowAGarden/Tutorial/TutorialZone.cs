using System;
using UnityEngine;
using Vulps.GrowAGarden.Interaction;

namespace Vulps.GrowAGarden.Tutorial
{
    /// <summary>A trigger on the tutorial platform that says which tutorial item came into it.</summary>
    [RequireComponent(typeof(Collider))]
    public class TutorialZone : MonoBehaviour, IItemReceiver
    {
        /// <summary>A tutorial item entered the zone.</summary>
        public event Action<TutorialItem> Entered;

        /// <summary>What a 2D player's prompt says here ("Claim", "Plant", "Sell"); the platform sets it.</summary>
        public string Verb { get; set; } = "Put";

        private void OnTriggerEnter(Collider other)
        {
            TutorialItem item = other.GetComponentInParent<TutorialItem>();
            if (item != null) Entered?.Invoke(item);
        }

        public string VerbFor(GameObject item) => item != null && item.GetComponentInParent<TutorialItem>() != null ? Verb : null;

        public void Receive(GameObject item)
        {
            TutorialItem tutorialItem = item == null ? null : item.GetComponentInParent<TutorialItem>();
            if (tutorialItem != null) Entered?.Invoke(tutorialItem);
        }

        public Vector3 ReceivePoint => TryGetComponent(out Collider zone) ? zone.bounds.center : transform.position;
    }
}
