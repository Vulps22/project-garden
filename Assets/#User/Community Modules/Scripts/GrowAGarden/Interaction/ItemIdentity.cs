using UnityEngine;

namespace Vulps.GrowAGarden.Interaction
{
    /// <summary>
    /// What kind of thing this is, as a stable string ("seed.tomato", "crop.tomato", "item.deed"): typed on each carryable
    /// prefab, the same on every client, and what persistence stores in a shelf slot. See docs/persistence.md.
    /// </summary>
    public class ItemIdentity : MonoBehaviour
    {
        [Tooltip("Stable kind id: seed.<crop>, crop.<crop> or item.<name>. Never change one once players have stored it.")]
        [SerializeField] private string _id;

        public string Id => _id;

        /// <summary>The kind id of an object, or empty if it has none.</summary>
        public static string Of(Component thing)
        {
            if (thing == null) return string.Empty;
            ItemIdentity identity = thing.GetComponent<ItemIdentity>();
            return identity == null || identity._id == null ? string.Empty : identity._id;
        }
    }
}
