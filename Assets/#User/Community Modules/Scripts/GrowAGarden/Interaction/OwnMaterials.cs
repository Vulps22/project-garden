using UnityEngine;

namespace Vulps.GrowAGarden.Interaction
{
    /// <summary>
    /// Gives this renderer its own copies of its materials at spawn. For anything whose material is
    /// driven per object, such as an SPS mesh: Community Modules' SPSTrigger writes to the shared
    /// material, so without this every copy in the world would bend at once.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class OwnMaterials : MonoBehaviour
    {
        private Material[] _copies;

        private void Awake()
        {
            Renderer r = GetComponent<Renderer>();
            _copies = r.materials;   // reading .materials makes this renderer's own copies
        }

        private void OnDestroy()
        {
            if (_copies == null) return;
            foreach (Material m in _copies) if (m != null) Destroy(m);
        }
    }
}
