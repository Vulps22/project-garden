using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Vulps.GrowAGarden.EditorTools
{
    /// <summary>Writes the Garden island's meshes to an .obj in Blender's axes, for models fitted to it (Yggdrasil's roots).</summary>
    public static class GardenIslandObjExporter
    {
        private const string PREFAB = "Assets/#User/GrowAGarden/Prefabs_world/GardenIsland.prefab";
        private const string OUTPUT = "Library/GardenIsland.obj";

        /// <summary>Every mesh but the Dome's, in prefab space; rock objects are named rock|path so Blender can tell them apart.</summary>
        [MenuItem("Grow A Garden/Export Garden Island For Blender")]
        public static void Export()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
            var obj = new StringBuilder();
            int baseIndex = 1;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(filter.transform, prefab.transform);
                Mesh mesh = filter.sharedMesh;
                if (path.StartsWith("Dome") || mesh == null || mesh.vertexCount == 0) continue;
                string kind = path.StartsWith("RockFormation") ? "rock" : "garden";
                obj.Append("o ").Append(kind).Append('|').AppendLine(path.Replace(' ', '_'));
                // Unity (x, y, z) is Blender (-x, -z, y), as the FBX export maps it; the swap flips handedness, so winding too.
                foreach (Vector3 v in mesh.vertices)
                {
                    Vector3 w = filter.transform.TransformPoint(v);
                    obj.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:F3} {1:F3} {2:F3}", -w.x, -w.z, w.y));
                }
                int[] tris = mesh.triangles;
                for (int i = 0; i < tris.Length; i += 3)
                    obj.Append("f ").Append(tris[i] + baseIndex).Append(' ').Append(tris[i + 2] + baseIndex).Append(' ')
                        .Append(tris[i + 1] + baseIndex).AppendLine();
                baseIndex += mesh.vertexCount;
            }
            File.WriteAllText(OUTPUT, obj.ToString());
            Logger.Info($"Export() 'GardenIslandObjExporter' — wrote {baseIndex - 1} vertices to {Path.GetFullPath(OUTPUT)}");
        }
    }
}
