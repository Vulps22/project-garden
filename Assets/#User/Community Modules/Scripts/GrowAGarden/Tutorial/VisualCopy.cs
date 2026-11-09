using UnityEngine;

namespace Vulps.GrowAGarden.Tutorial
{
    /// <summary>Copies only what draws a prefab (its meshes and their materials), so nothing of the original's behaviour or networking comes with it.</summary>
    public static class VisualCopy
    {
        /// <summary>A new object under <paramref name="parent"/> drawing <paramref name="source"/>'s meshes, centred on the source's root.</summary>
        public static GameObject Of(GameObject source, Transform parent, string name)
        {
            var root = new GameObject(string.IsNullOrEmpty(name) ? source.name : name);
            root.transform.SetParent(parent, false);
            foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null) continue;
                var piece = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer));
                piece.transform.SetParent(root.transform, false);
                Matrix4x4 local = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                piece.transform.localPosition = local.GetColumn(3);
                piece.transform.localRotation = local.rotation;
                piece.transform.localScale = local.lossyScale;
                piece.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                piece.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            return root;
        }
    }
}
