using System.Collections.Generic;
using UnityEngine;

namespace Vulps.GrowAGarden.Tutorial
{
    /// <summary>Highlights the next thing to touch on the tutorial platform: a pulsing shell on a copy of each of its meshes.</summary>
    public class TutorialGlow : MonoBehaviour
    {
        [Tooltip("GrowAGarden/TutorialGlow.")]
        [SerializeField] private Material _material;

        private readonly List<GameObject> _shells = new List<GameObject>();

        /// <summary>Highlights <paramref name="target"/> and only it.</summary>
        public void Show(GameObject target)
        {
            Clear();
            if (target == null || _material == null) return;
            foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue;
                var shell = new GameObject("Glow", typeof(MeshFilter), typeof(MeshRenderer));
                shell.layer = filter.gameObject.layer;
                shell.transform.SetParent(filter.transform, false);
                shell.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = shell.GetComponent<MeshRenderer>();
                Material[] materials = new Material[filter.sharedMesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = _material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _shells.Add(shell);
            }
        }

        /// <summary>Highlights nothing.</summary>
        public void Clear()
        {
            foreach (GameObject shell in _shells) if (shell != null) Destroy(shell);
            _shells.Clear();
        }
    }
}
