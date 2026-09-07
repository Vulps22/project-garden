using System.Collections.Generic;
using GrowAGarden.Generation;
using UnityEditor;
using UnityEngine;

namespace GrowAGarden.EditorTools
{
    // ⚠ Unsure if we're keeping IslandFragmentGenerator — see the note at the top of that file.

    /// <summary>
    /// Grow A Garden ▸ Generate Island Fragment. The only Editor-specific part of the island
    /// fragment pipeline — finding candidate rock prefabs via AssetDatabase and saving the result
    /// as a prefab asset. The actual shape logic lives in IslandFragmentGenerator, which knows
    /// nothing about the Editor and will be the same code a runtime spawner calls once procgen
    /// islands exist.
    /// </summary>
    internal static class IslandFragmentTool
    {
        private const string GraniteFolder = "Assets/Blinktool/Low poly rocks/Prefabs/Granite";
        private const string MossyFolder = "Assets/Blinktool/Low poly rocks/Prefabs/Mossy";
        private const string SavePath = "Assets/#User/GrowAGarden/Prefabs_world/IslandFragment_Rock.prefab";
        private const string SceneInstanceName = "IslandFragment_Rock";

        [MenuItem("Grow A Garden/Generate Island Fragment")]
        private static void Generate()
        {
            List<GameObject> granite = IslandFragmentGenerator.FilterChunky(LoadFolder(GraniteFolder));
            List<GameObject> moss = IslandFragmentGenerator.FilterChunky(LoadFolder(MossyFolder));

            if (granite.Count == 0 || moss.Count == 0)
            {
                Debug.LogError($"IslandFragmentTool.Generate() — no chunky prefabs found (granite={granite.Count}, moss={moss.Count})");
                return;
            }

            GameObject existing = GameObject.Find(SceneInstanceName);
            if (existing != null) Object.DestroyImmediate(existing);

            IslandFragmentGenerator.Settings settings = IslandFragmentGenerator.DefaultSettings;
            settings.Seed = System.Environment.TickCount;

            GameObject root = IslandFragmentGenerator.Generate(
                settings, granite, moss,
                (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent));
            root.name = SceneInstanceName;

            PrefabUtility.SaveAsPrefabAsset(root, SavePath, out bool success);
            AssetDatabase.SaveAssets();

            Debug.Log($"IslandFragmentTool.Generate() — saved {(success ? "ok" : "FAILED")} to {SavePath}, seed={settings.Seed}");
        }

        private static List<GameObject> LoadFolder(string folder)
        {
            var list = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null) list.Add(p);
            }
            return list;
        }
    }
}
