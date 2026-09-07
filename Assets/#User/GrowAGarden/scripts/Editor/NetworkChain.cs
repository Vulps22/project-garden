using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GrowAGarden.EditorTools
{
    /// <summary>
    /// Follows a crop from seed to plant to produce by walking the prefab references the crop
    /// already carries — Seed._plantPrefab, then Plant._producePrefab.
    ///
    /// Deliberately not "find the prefabs whose names start with Seed_/Plant_/Produce_". The chain
    /// is the lifecycle and the naming is a convention; resolving by name would work until the
    /// first crop that breaks it, and would quietly clone the wrong plant rather than failing.
    /// </summary>
    internal class NetworkChain
    {
        public string SeedPath;
        public string PlantPath;
        public string ProducePath;

        public static NetworkChain Resolve(SeedDefinition template)
        {
            if (template == null) return null;

            string typeName = template.GetType().Name;

            // The seed prefab is the one carrying this definition type *and* a Seed component —
            // the definition also sits on the plant and the produce, so the type alone is ambiguous.
            GameObject seedPrefab = AssetDatabase
                .FindAssets("t:Prefab", new[] { "Assets/#User/GrowAGarden" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(go => go != null && go.GetComponent<Seed>() != null)
                .FirstOrDefault(go => go.GetComponentsInChildren<SeedDefinition>(true)
                                        .Any(d => d.GetType().Name == typeName));

            if (seedPrefab == null) return null;

            GameObject plantPrefab = Follow(seedPrefab.GetComponent<Seed>(), "_plantPrefab");
            if (plantPrefab == null) return null;

            GameObject producePrefab = plantPrefab.GetComponents<Plant>()
                .Select(p => Follow(p, "_producePrefab"))
                .FirstOrDefault(go => go != null);
            if (producePrefab == null) return null;

            return new NetworkChain
            {
                SeedPath = AssetDatabase.GetAssetPath(seedPrefab),
                PlantPath = AssetDatabase.GetAssetPath(plantPrefab),
                ProducePath = AssetDatabase.GetAssetPath(producePrefab),
            };
        }

        private static GameObject Follow(Object from, string field)
        {
            if (from == null) return null;
            var prop = new SerializedObject(from).FindProperty(field);
            var value = prop == null ? null : prop.objectReferenceValue;
            if (value == null) return null;

            var component = value as Component;
            GameObject go = component != null ? component.gameObject : value as GameObject;
            if (go == null) return null;

            string path = AssetDatabase.GetAssetPath(go);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
}
