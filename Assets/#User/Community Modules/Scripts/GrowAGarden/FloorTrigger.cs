using System;
using UnityEngine;

namespace Vulps.GrowAGarden
{
    /// <summary>A trigger volume that says what fell into it. Physics raises the event; see <see cref="WorldFloor"/>.</summary>
    public class FloorTrigger : MonoBehaviour
    {
        /// <summary>A collider entered the volume.</summary>
        public event Action<Collider> Entered;

        private void OnTriggerEnter(Collider other) => Entered?.Invoke(other);
    }
}
