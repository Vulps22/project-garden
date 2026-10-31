using UnityEngine;

namespace Vulps.GrowAGarden.Islands
{
    /// <summary>How big an island may be; the field treats every island as its category's full size.</summary>
    public enum IslandCategory
    {
        Outcrop,
        Island,
        Landmark,
    }

    /// <summary>Marks a prefab as an island the field may place, and says which size category it belongs to.</summary>
    public class SpawnableIsland : MonoBehaviour
    {
        [Tooltip("The size category. The field reserves this category's full box for the island, whatever it actually contains.")]
        [SerializeField] private IslandCategory _category = IslandCategory.Outcrop;

        [Tooltip("Top-down picture of this island for the tablet map. Set by Grow A Garden > Bake Island Map Sprites.")]
        [SerializeField] private Sprite _mapSprite;

        [Tooltip("How many metres the map sprite spans, edge to edge, centred on this object. Set by the baker.")]
        [SerializeField] private float _mapSpan;

        /// <summary>This island's size category.</summary>
        public IslandCategory Category => _category;

        /// <summary>Top-down picture for the tablet map, or null if none has been baked.</summary>
        public Sprite MapSprite => _mapSprite;

        /// <summary>Metres the map sprite spans, edge to edge.</summary>
        public float MapSpan => _mapSpan;

        /// <summary>The largest an island of a category may be: width (X), height (Y), depth (Z), in metres.</summary>
        public static Vector3 MaxSize(IslandCategory category)
        {
            switch (category)
            {
                case IslandCategory.Island:   return new Vector3(90f, 100f, 90f);
                case IslandCategory.Landmark: return new Vector3(250f, 180f, 250f);
                default:                      return new Vector3(35f, 60f, 35f);
            }
        }

        /// <summary>Radius of the circle a category's box sweeps when spun to any yaw.</summary>
        public static float FootprintRadius(IslandCategory category)
        {
            Vector3 size = MaxSize(category);
            return 0.5f * Mathf.Sqrt(size.x * size.x + size.z * size.z);
        }

        private void OnDrawGizmos()
        {
            Vector3 size = MaxSize(_category);
            Color colour = _category == IslandCategory.Landmark ? new Color(1f, 0.55f, 0.2f)
                         : _category == IslandCategory.Island ? new Color(0.3f, 0.6f, 1f)
                         : new Color(0.3f, 0.9f, 0.4f);

            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = new Color(colour.r, colour.g, colour.b, 0.08f);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = colour;
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        private void OnValidate()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

            Vector3 max = MaxSize(_category);
            if (bounds.size.x > max.x || bounds.size.y > max.y || bounds.size.z > max.z)
                Logger.Warn($"OnValidate() '{gameObject.name}' — contents {bounds.size:F0} exceed {_category}'s {max:F0}; it may overlap its neighbours");
        }
    }
}
