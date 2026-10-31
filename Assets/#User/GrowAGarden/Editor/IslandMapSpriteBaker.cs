using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vulps.GrowAGarden.Islands;

namespace Vulps.GrowAGarden.EditorTools
{
    /// <summary>Renders each island prefab, and the Garden, from directly above into a transparent sprite for the tablet map.</summary>
    public static class IslandMapSpriteBaker
    {
        private const string SEARCH = "Assets/#User/GrowAGarden";
        private const string OUTPUT = "Assets/#User/GrowAGarden/Islands/MapSprites";
        private const string GARDEN = "GardenIsland";
        private const int RESOLUTION = 256;
        private static readonly Color KEY = new Color(1f, 0f, 1f, 1f);

        /// <summary>Bakes every prefab under the Grow A Garden folder that carries SpawnableIsland, plus the Garden.</summary>
        [MenuItem("Grow A Garden/Bake Island Map Sprites")]
        public static void BakeAll()
        {
            if (!AssetDatabase.IsValidFolder(OUTPUT)) AssetDatabase.CreateFolder("Assets/#User/GrowAGarden/Islands", "MapSprites");

            int baked = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { SEARCH }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                if (prefab.GetComponent<SpawnableIsland>() == null && prefab.name != GARDEN) continue;
                if (Bake(path)) baked++;
            }
            AssetDatabase.SaveAssets();
            Logger.Info($"BakeAll() 'IslandMapSpriteBaker' — baked {baked} map sprites into {OUTPUT}");
        }

        /// <summary>Bakes one prefab; returns false when it has nothing to draw.</summary>
        public static bool Bake(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Scene scene = EditorSceneManager.NewPreviewScene();
            float span;
            string pngPath = $"{OUTPUT}/{prefab.name}.png";
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return false;
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

                float half = Mathf.Max(Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)),
                                       Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)));
                span = half * 2f;

                var light = new GameObject("Bake Light", typeof(Light)).GetComponent<Light>();
                SceneManager.MoveGameObjectToScene(light.gameObject, scene);
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(60f, 30f, 0f);

                var camera = new GameObject("Bake Camera", typeof(Camera)).GetComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = half;
                camera.transform.SetPositionAndRotation(new Vector3(0f, bounds.max.y + 10f, 0f), Quaternion.Euler(90f, 0f, 0f));
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = bounds.size.y + 20f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = KEY;

                var target = new RenderTexture(RESOLUTION, RESOLUTION, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                var texture = new Texture2D(RESOLUTION, RESOLUTION, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, RESOLUTION, RESOLUTION), 0, 0);
                RenderTexture.active = null;
                KeyOut(texture);

                File.WriteAllBytes(pngPath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);

            if (prefab.GetComponent<SpawnableIsland>() != null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
                var so = new SerializedObject(contents.GetComponent<SpawnableIsland>());
                so.FindProperty("_mapSprite").objectReferenceValue = sprite;
                so.FindProperty("_mapSpan").floatValue = span;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Logger.Info($"Bake() 'IslandMapSpriteBaker' — '{prefab.name}' → {pngPath}, spans {span:F0} m");
            return true;
        }

        /// <summary>Makes the background colour transparent, whether or not the render kept alpha.</summary>
        private static void KeyOut(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                bool background = p.r > 240 && p.g < 20 && p.b > 240;
                pixels[i] = background ? new Color32(0, 0, 0, 0) : new Color32(p.r, p.g, p.b, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
        }
    }
}
