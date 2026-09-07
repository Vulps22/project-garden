using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GrowAGarden.EditorTools
{
    /// <summary>
    /// Grow A Garden ▸ New Seed. Turns the cost of a crop into one dialogue.
    ///
    /// It clones an existing crop rather than building three prefabs from scratch, and that is the
    /// whole design. A Seed_ prefab is sixteen components, several carrying tuning that only VR
    /// reveals — hover height, rise rate, fall gravity scale, spin. Reconstructing that in code
    /// means the tool becomes a second, unverified definition of what a seed is, and the first
    /// time the prefabs change it is silently wrong. Cloning starts from the artifact that has
    /// actually been played.
    ///
    /// It is also why the shape is chosen by picking a *template crop* rather than a plant class:
    /// the shapes are real and few — rooted, single-harvest, multi-harvest — and an existing crop
    /// is the honest way to name one.
    ///
    /// Two passes, because a generated SeedDefinition subclass does not exist as a Type until the
    /// domain has reloaded. The job is parked in SessionState and resumed by DidReloadScripts.
    /// </summary>
    public class NewSeedWindow : EditorWindow
    {
        private const string PENDING_KEY = "GrowAGarden.NewSeed.Pending";

        private const string SEEDS_DIR   = "Assets/#User/GrowAGarden/scripts/Seeds";
        private const string PREFABS_DIR = "Assets/#User/GrowAGarden/prefabs_crops";

        private string _cropName = "Beetroot";
        private string _displayName = "Beetroot";
        private SeedDefinition _template;
        private Mesh _mesh;

        private int _buyPrice = 20;
        private int _sellValue = 30;
        private float _spawnWeight = 0.6f;
        private float _growthDuration = 12f;
        private float _ripenDuration = 1f;
        private float _witherDuration = 0f;

        [MenuItem("Grow A Garden/New Seed", priority = 0)]
        private static void Open() =>
            GetWindow<NewSeedWindow>(true, "New Seed", true).minSize = new Vector2(420, 460);

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Clones an existing crop's three prefabs, generates a SeedDefinition, wires the " +
                "chain, and registers it. Pick the template whose shape you want — rooted, " +
                "single-harvest, multi-harvest.", MessageType.None);

            EditorGUILayout.Space();
            _cropName = EditorGUILayout.TextField(new GUIContent("Crop name",
                "Used for the class and the prefabs: Beetroot -> BeetrootSeed, Seed_Beetroot."), _cropName);
            _displayName = EditorGUILayout.TextField("Display name", _displayName);
            _template = (SeedDefinition)EditorGUILayout.ObjectField(new GUIContent("Template crop",
                "Any existing crop's definition. Its shape is the shape you get."),
                _template, typeof(SeedDefinition), true);
            _mesh = (Mesh)EditorGUILayout.ObjectField(new GUIContent("Mesh (optional)",
                "Applied to the first MeshFilter in each new prefab. Leave empty to keep the template's."),
                _mesh, typeof(Mesh), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Economy", EditorStyles.boldLabel);
            _buyPrice = EditorGUILayout.IntField("Buy price", _buyPrice);
            _sellValue = EditorGUILayout.IntField("Sell value", _sellValue);
            _spawnWeight = EditorGUILayout.FloatField(new GUIContent("Spawn weight",
                "Relative chance of a stall rolling this. 1 is a staple; lower is rarer."), _spawnWeight);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Timing (seconds)", EditorStyles.boldLabel);
            _growthDuration = EditorGUILayout.FloatField("Growth", _growthDuration);
            _ripenDuration = EditorGUILayout.FloatField("Ripen", _ripenDuration);
            _witherDuration = EditorGUILayout.FloatField(new GUIContent("Wither",
                "Ignored by rooted plants, which are replaced by their produce the moment they grow."),
                _witherDuration);

            EditorGUILayout.Space();
            string problem = Validate();
            if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);

            using (new EditorGUI.DisabledScope(problem != null))
                if (GUILayout.Button("Create crop", GUILayout.Height(30)))
                    Create();
        }

        private string Validate()
        {
            if (string.IsNullOrWhiteSpace(_cropName)) return "Give the crop a name.";
            if (!char.IsLetter(_cropName[0]) || _cropName.Any(c => !char.IsLetterOrDigit(c)))
                return "The name has to be letters and digits, starting with a letter — it becomes a class name.";
            if (_template == null) return "Pick a template crop to take the shape from.";
            if (File.Exists($"{SEEDS_DIR}/{_cropName}Seed.cs")) return $"{_cropName}Seed.cs already exists.";
            if (File.Exists($"{PREFABS_DIR}/Seed_{_cropName}.prefab")) return $"Seed_{_cropName}.prefab already exists.";
            return null;
        }

        // ── Pass one: the definition class ────────────────────────────────────────

        private void Create()
        {
            NetworkChain chain = NetworkChain.Resolve(_template);
            if (chain == null)
            {
                EditorUtility.DisplayDialog("New Seed",
                    $"Could not follow '{_template.seedId}' from seed to plant to produce. The template " +
                    "needs a Seed_ prefab whose _plantPrefab and _producePrefab are wired.", "OK");
                return;
            }

            Directory.CreateDirectory(SEEDS_DIR);
            File.WriteAllText($"{SEEDS_DIR}/{_cropName}Seed.cs", DefinitionSource());

            var job = new PendingJob
            {
                cropName = _cropName,
                meshGuid = _mesh == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_mesh)),
                seedPath = chain.SeedPath,
                plantPath = chain.PlantPath,
                producePath = chain.ProducePath,
                templateTypeName = _template.GetType().Name,
            };
            SessionState.SetString(PENDING_KEY, JsonUtility.ToJson(job));

            Debug.Log($"[New Seed] Wrote {_cropName}Seed.cs — waiting for the domain reload to build the prefabs.");
            AssetDatabase.Refresh();
            Close();
        }

        private string DefinitionSource()
        {
            var sb = new StringBuilder();
            sb.AppendLine("namespace GrowAGarden");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// {_displayName}. Generated by Grow A Garden ▸ New Seed from the {_template.seedId} template.");
            sb.AppendLine("    ///");
            sb.AppendLine("    /// Numbers are hardcoded so balance changes are code changes rather than Inspector");
            sb.AppendLine("    /// edits. This one definition is referenced by all three of the crop's prefabs.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public class {_cropName}Seed : SeedDefinition");
            sb.AppendLine("    {");
            sb.AppendLine("        protected override void Init()");
            sb.AppendLine("        {");
            sb.AppendLine($"            seedId = \"{_cropName.ToLowerInvariant()}\";");
            sb.AppendLine($"            displayName = \"{_displayName}\";");
            sb.AppendLine($"            buyPrice = {_buyPrice};");
            sb.AppendLine($"            sellValue = {_sellValue};");
            sb.AppendLine($"            growthDuration = {_growthDuration}f;");
            sb.AppendLine($"            ripenDuration = {_ripenDuration}f;");
            sb.AppendLine($"            witherDuration = {_witherDuration}f;");
            sb.AppendLine($"            spawnWeight = {_spawnWeight}f;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        [Serializable]
        private class PendingJob
        {
            public string cropName;
            public string meshGuid;
            public string seedPath;
            public string plantPath;
            public string producePath;
            public string templateTypeName;
        }

        // ── Pass two: the prefabs, once the class exists ──────────────────────────

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void ResumeAfterReload()
        {
            string raw = SessionState.GetString(PENDING_KEY, "");
            if (string.IsNullOrEmpty(raw)) return;
            SessionState.EraseString(PENDING_KEY);

            var job = JsonUtility.FromJson<PendingJob>(raw);
            Type definitionType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .FirstOrDefault(t => t.Name == job.cropName + "Seed" && typeof(SeedDefinition).IsAssignableFrom(t));

            if (definitionType == null)
            {
                Debug.LogError($"[New Seed] {job.cropName}Seed compiled but could not be found; prefabs not built. " +
                               "Fix any compile error and run the tool again.");
                return;
            }

            try
            {
                BuildPrefabs(job, definitionType);
            }
            catch (Exception e)
            {
                Debug.LogError($"[New Seed] Failed building '{job.cropName}': {e}");
            }
        }

        private static Type[] SafeTypes(System.Reflection.Assembly a)
        {
            try { return a.GetTypes(); } catch { return Type.EmptyTypes; }
        }

        private static void BuildPrefabs(PendingJob job, Type definitionType)
        {
            string seedPath    = $"{PREFABS_DIR}/Seed_{job.cropName}.prefab";
            string plantPath   = $"{PREFABS_DIR}/Plant_{job.cropName}.prefab";
            string producePath = $"{PREFABS_DIR}/Produce_{job.cropName}.prefab";

            Clone(job.seedPath, seedPath);
            Clone(job.plantPath, plantPath);
            Clone(job.producePath, producePath);

            Mesh mesh = string.IsNullOrEmpty(job.meshGuid)
                ? null
                : AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(job.meshGuid));

            SeedDefinition seedDef    = Retype(seedPath, job.templateTypeName, definitionType, mesh);
            SeedDefinition plantDef   = Retype(plantPath, job.templateTypeName, definitionType, mesh);
            SeedDefinition produceDef = Retype(producePath, job.templateTypeName, definitionType, mesh);

            // Rewire the chain. Each stage names only the next one — that is the lifecycle, and
            // it is why a crop needs no central description of itself.
            var seedGo    = PrefabUtility.LoadPrefabContents(seedPath);
            var plantGo   = PrefabUtility.LoadPrefabContents(plantPath);
            var produceGo = AssetDatabase.LoadAssetAtPath<GameObject>(producePath);
            var plantAsset = AssetDatabase.LoadAssetAtPath<GameObject>(plantPath);

            SetObjectField(seedGo.GetComponent<Seed>(), "_plantPrefab", plantAsset.GetComponent<Fusion.NetworkObject>());
            PrefabUtility.SaveAsPrefabAsset(seedGo, seedPath);
            PrefabUtility.UnloadPrefabContents(seedGo);

            foreach (var plant in plantGo.GetComponents<Plant>())
                SetObjectField(plant, "_producePrefab", produceGo.GetComponent<Fusion.NetworkObject>());
            PrefabUtility.SaveAsPrefabAsset(plantGo, plantPath);
            PrefabUtility.UnloadPrefabContents(plantGo);

            // The definition's own seedPrefab closes the loop for the catalogue.
            var seedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(seedPath);
            foreach (var def in new[] { seedDef, plantDef, produceDef })
            {
                if (def == null) continue;
                var so = new SerializedObject(def);
                so.FindProperty("seedPrefab").objectReferenceValue = seedAsset.GetComponent<Fusion.NetworkObject>();
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SavePrefabAsset(def.gameObject.transform.root.gameObject);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Register(job.cropName, seedAsset, plantAsset, produceGo, definitionType);

            Selection.activeObject = seedAsset;
            EditorGUIUtility.PingObject(seedAsset);
            Debug.Log($"[New Seed] Built Seed_/Plant_/Produce_{job.cropName} and chained them.");
        }

        private static void Clone(string from, string to)
        {
            if (!AssetDatabase.CopyAsset(from, to))
                throw new Exception($"could not copy {from} -> {to}");
        }

        /// <summary>
        /// Swaps the template's definition component for the new crop's, and re-points everything
        /// that referenced the old one. Done by scanning serialized object references rather than
        /// by naming fields, so a component that gains a definition reference later needs no
        /// change here.
        /// </summary>
        private static SeedDefinition Retype(string prefabPath, string oldTypeName, Type newType, Mesh mesh)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SeedDefinition old = root.GetComponentsInChildren<SeedDefinition>(true)
                    .FirstOrDefault(d => d.GetType().Name == oldTypeName);

                SeedDefinition created = null;
                if (old != null)
                {
                    GameObject owner = old.gameObject;
                    UnityEngine.Object.DestroyImmediate(old, true);
                    created = (SeedDefinition)owner.AddComponent(newType);

                    foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb == null) continue;
                        var so = new SerializedObject(mb);
                        var it = so.GetIterator();
                        bool touched = false;
                        while (it.NextVisible(true))
                        {
                            if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                            // The old component is destroyed, so any field that held it now reads
                            // null while still declaring a SeedDefinition — that is the one we want.
                            if (it.objectReferenceValue != null) continue;
                            if (!it.type.Contains("SeedDefinition") && !it.type.Contains(oldTypeName)) continue;
                            it.objectReferenceValue = created;
                            touched = true;
                        }
                        if (touched) so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                if (mesh != null)
                {
                    var filter = root.GetComponentInChildren<MeshFilter>(true);
                    if (filter != null) filter.sharedMesh = mesh;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return saved == null ? null : saved.GetComponentInChildren<SeedDefinition>(true);
        }

        private static void SetObjectField(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── The two lists that would otherwise drift ──────────────────────────────

        /// <summary>
        /// Adds the three prefabs to SceneNetworking and the definition to WorldManager.
        ///
        /// These are the real cost of a crop at scale — two hand-maintained lists that have to
        /// agree, in a scene rather than in code, where nothing checks them. The tool exists as
        /// much for this as for the prefabs.
        /// </summary>
        private static void Register(string cropName, GameObject seed, GameObject plant, GameObject produce, Type definitionType)
        {
            var net = UnityEngine.Object.FindFirstObjectByType<SceneNetworking>();
            if (net == null)
            {
                Debug.LogWarning($"[New Seed] No SceneNetworking in the open scene — add Seed_/Plant_/Produce_{cropName} " +
                                 "to its Network Prefabs list by hand, or open the scene and run the tool again.");
            }
            else
            {
                var so = new SerializedObject(net);
                var arr = so.FindProperty("_networkPrefabs");
                foreach (var go in new[] { seed, plant, produce })
                {
                    var no = go.GetComponent<Fusion.NetworkObject>();
                    if (no == null) continue;
                    bool already = Enumerable.Range(0, arr.arraySize)
                        .Any(i => arr.GetArrayElementAtIndex(i).objectReferenceValue == no);
                    if (already) continue;
                    arr.InsertArrayElementAtIndex(arr.arraySize);
                    arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = no;
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(net);
            }

            var world = UnityEngine.Object.FindFirstObjectByType<WorldManager>();
            if (world == null)
            {
                Debug.LogWarning($"[New Seed] No WorldManager in the open scene — add a {definitionType.Name} " +
                                 "to its Buyables by hand.");
                return;
            }

            // The catalogue holds a component, so the crop needs somewhere to live in the scene.
            var holder = new GameObject(cropName);
            holder.transform.SetParent(world.transform, false);
            var entry = (SeedDefinition)holder.AddComponent(definitionType);

            var eso = new SerializedObject(entry);
            eso.FindProperty("seedPrefab").objectReferenceValue = seed.GetComponent<Fusion.NetworkObject>();
            eso.ApplyModifiedPropertiesWithoutUndo();

            var wso = new SerializedObject(world);
            var buyables = wso.FindProperty("_buyables");
            buyables.InsertArrayElementAtIndex(buyables.arraySize);
            buyables.GetArrayElementAtIndex(buyables.arraySize - 1).objectReferenceValue = entry;
            wso.ApplyModifiedProperties();
            EditorUtility.SetDirty(world);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            Debug.Log($"[New Seed] Registered '{cropName}' on SceneNetworking and WorldManager. Save the scene to keep it.");
        }
    }
}
