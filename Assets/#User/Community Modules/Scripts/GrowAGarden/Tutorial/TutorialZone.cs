using System;
using UnityEngine;

namespace Vulps.GrowAGarden.Tutorial
{
    /// <summary>A trigger on the tutorial platform that says which tutorial item came into it.</summary>
    [RequireComponent(typeof(Collider))]
    public class TutorialZone : MonoBehaviour
    {
        /// <summary>A tutorial item entered the zone.</summary>
        public event Action<TutorialItem> Entered;

        private void OnTriggerEnter(Collider other)
        {
            TutorialItem item = other.GetComponentInParent<TutorialItem>();
            if (item != null) Entered?.Invoke(item);
        }
    }
}
