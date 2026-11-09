using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vulps.GrowAGarden.Tutorial;

namespace Vulps.GrowAGarden.EditorTools
{
    /// <summary>Builds the tutorial platform into the open scene from the game's own prefabs, stripped to what draws. Rerun to rebuild. See docs/tutorial.md.</summary>
    public static class TutorialPlatformBuilder
    {
        private const string ROOT_NAME = "TutorialPlatform";
        private const string ART = "Assets/#User/GrowAGarden/";
        private const string MATERIALS = ART + "Materials/Tutorial";
        private static readonly Vector3 POSITION = new Vector3(0f, 5000f, 0f);
        private const float WALL_RADIUS = 14f;
        private const float SKY_RADIUS = 80f;
        // The Garden's ground is 0.5 m above its pieces' roots: plots sink by it, and the barrel's root sits 0.35 m above it.
        private const float GROUND = 0.5f;
        private const float BARREL_HEIGHT = 0.35f;

        // What survives on a copy: drawing, text, fire, and (on carryables) grabbing.
        private static readonly HashSet<string> KEEP = new HashSet<string>
        {
            "Transform", "RectTransform", "MeshFilter", "MeshRenderer", "TextMeshPro", "ParticleSystem", "ParticleSystemRenderer",
            "Light", "UniversalAdditionalLightData", "FireLightFlicker", "AudioSource",
        };

        [MenuItem("Grow A Garden/Build Tutorial Platform")]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject existing in scene.GetRootGameObjects())
                if (existing.name == ROOT_NAME) Object.DestroyImmediate(existing);

            var root = new GameObject(ROOT_NAME);
            root.transform.position = POSITION;
            TutorialPlatform platform = root.AddComponent<TutorialPlatform>();
            var wiring = new SerializedObject(platform);

            Materials m = LoadMaterials();

            // The void: a white sky sphere, an invisible floor and a ring of invisible walls.
            Mesh sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            Mesh cylinder = Resources.GetBuiltinResource<Mesh>("New-Cylinder.fbx");
            Mesh plane = Resources.GetBuiltinResource<Mesh>("New-Plane.fbx");
            GameObject sky = Drawn("Sky", root.transform, sphere, m.Sky);
            sky.transform.localScale = Vector3.one * SKY_RADIUS * 2f;
            Floor(root.transform);
            Walls(root.transform);

            SmokePuff smoke = Smoke(root.transform, m.Smoke);

            GameObject voidFloor = Drawn("VoidFloor", root.transform, plane, m.Floor);
            voidFloor.transform.localScale = new Vector3(WALL_RADIUS * 0.2f + 0.2f, 1f, WALL_RADIUS * 0.2f + 0.2f);
            voidFloor.transform.localPosition = Vector3.up * 0.002f;
            Stage(voidFloor, smoke);

            GameObject disc = Drawn("Disc", root.transform, cylinder, m.Disc);
            disc.transform.localScale = new Vector3(8f, 0.05f, 8f);
            disc.transform.localPosition = Vector3.up * -0.04f;
            Stage(disc, smoke);

            var arrival = new GameObject("Arrival").transform;
            arrival.SetParent(root.transform, false);
            arrival.localPosition = new Vector3(0f, 0.05f, -4f);

            // Helpers.
            var helpers = new GameObject("Helpers");
            helpers.transform.SetParent(root.transform, false);
            InstructionPanel panel = helpers.AddComponent<InstructionPanel>();
            var notifier = Object.FindFirstObjectByType<Notifications.Notifier>();
            if (notifier != null) Set(panel, "_font", new SerializedObject(notifier).FindProperty("_font").objectReferenceValue);
            TutorialGlow glow = helpers.AddComponent<TutorialGlow>();
            AudioSource voice = helpers.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            Set(glow, "_material", m.Glow);

            // The plot, with a dirt pile on its middle-right row.
            GameObject plot = Copy("prefabs_world:BorderedPlot", "Plot", root.transform, new Vector3(0f, 0.01f - GROUND, 1.5f), 180f,
                remove: new[] { "Deed", "Plot/DeedAnchor", "Plot/ApplicationDropZone" });
            Stage(plot, smoke);
            Transform dirtRow = plot.transform.Find("Plot/Dirt (2)");
            GameObject dirtPile = Copy("prefabs_world:Plant_Slot", "DirtPile", dirtRow != null ? dirtRow : plot.transform, new Vector3(0f, 0.51f, 0f), 0f);
            var plantZone = dirtPile.AddComponent<BoxCollider>();
            plantZone.isTrigger = true;
            plantZone.center = Vector3.up * 0.1f;
            plantZone.size = new Vector3(0.4f, 0.35f, 0.4f);
            TutorialZone plantZoneComponent = dirtPile.AddComponent<TutorialZone>();

            // The hut, door towards the player; its claim zone kept, the fire off until claimed.
            GameObject hut = Copy("prefabs_world:DeedHut", "Hut", root.transform, new Vector3(0f, 0f, 6f), 0f,
                remove: new[] { "ItemDispenser", "BeatRings", "GuestDeed (0)", "GuestDeed (1)", "GuestDeed (2)", "GuestDeed (3)", "GuestDeed (4)", "GuestDeed (5)" },
                keepColliders: new[] { "Log house", "ClaimZone" });
            Transform door = hut.transform.Find("Log house/Door");
            FaceTowardsPlayer(hut.transform, door, hut.transform.Find("ClaimZone")?.GetComponent<BoxCollider>());
            Stage(hut, smoke);
            Transform claim = hut.transform.Find("ClaimZone");
            TutorialZone claimZone = claim.gameObject.AddComponent<TutorialZone>();
            Transform namedDeed = hut.transform.Find("Named Deed");
            Transform fireplace = hut.transform.Find("Fireplace");

            // The buy point: the barrel at its real height, its own labels towards the player, the seed in its socket.
            GameObject barrel = Copy("prefab_buyables:BuyPoint", "Barrel", root.transform, new Vector3(0f, BARREL_HEIGHT, 1.5f), BARREL_YAW, remove: new[] { "GrooveGlow" });
            Stage(barrel, smoke);
            Transform labels = barrel.transform.Find("SalePoint/data");
            Transform socket = barrel.transform.Find("Socket");

            // The scoreboard: the wooden board, with our own line where the balance board was.
            GameObject board = Copy("prefabs_world:Scoreboard", "Scoreboard", root.transform, new Vector3(0f, 0f, 3f), 0f, remove: new[] { "BalanceBoard" });
            Stage(board, smoke);
            Transform boardPose = AssetDatabase.LoadAssetAtPath<GameObject>(ART + "Prefabs_world/Scoreboard.prefab").transform.Find("BalanceBoard");
            TMP_Text scoreboardText = Label("Balance", board.transform, boardPose.localPosition + Vector3.back * 0.12f, 2.2f, "Player    0", Color.white);
            scoreboardText.transform.localRotation = Quaternion.identity;

            // The sell point.
            GameObject sell = Copy("prefabs_sellables:SellPoint", "SellPoint", root.transform, new Vector3(0f, 0f, 3f), 270f, keepColliders: new[] { "" });
            Stage(sell, smoke);
            TutorialZone sellZone = sell.AddComponent<TutorialZone>();

            // Seed types: two rows that fill themselves in at runtime.
            var types = new GameObject("SeedTypes");
            types.transform.SetParent(root.transform, false);
            types.transform.localPosition = new Vector3(0f, 1.3f, 2.5f);
            SeedTypesShow show = types.AddComponent<SeedTypesShow>();
            Transform buffRow = Empty("BuffingRow", types.transform, new Vector3(-1.4f, 0f, 0f));
            Transform upgradeRow = Empty("UpgradeRow", types.transform, new Vector3(1.4f, 0f, 0f));
            TMP_Text buffLabel = Label("BuffingLabel", types.transform, new Vector3(-1.4f, 0.55f, 0f), 1.2f, "Buffing seeds", new Color(0.1f, 0.25f, 0.45f));
            TMP_Text upgradeLabel = Label("UpgradeLabel", types.transform, new Vector3(1.4f, 0.55f, 0f), 1.2f, "Upgrade seeds", new Color(0.1f, 0.25f, 0.45f));
            Set(show, "_buffingRow", buffRow);
            Set(show, "_upgradeRow", upgradeRow);
            Set(show, "_buffingLabel", buffLabel.gameObject);
            Set(show, "_upgradeLabel", upgradeLabel.gameObject);
            Set(show, "_smoke", smoke);
            types.SetActive(false);

            // The carryables, apart from every stage so hiding a stage never takes one out of a hand.
            Transform items = Empty("Items", root.transform, Vector3.zero);
            Transform path = plot.transform.Find("Plot/Path");
            TutorialItem scroll = Item("prefab_items:Collectible_BlankDeedScroll", "Scroll", items, (path != null ? path.position : plot.transform.position) + Vector3.up * (GROUND + 1.1f), smoke);
            TutorialItem seed = Item("prefabs_crops:Seed_Carrot", "Seed", items, socket.position, smoke, socket.rotation);
            TutorialItem produce = Item("prefabs_crops:Produce_Carrot", "Carrot", items, dirtPile.transform.position, smoke);

            // Wiring.
            Set(wiring, "_arrival", arrival);
            var floorCatch = Object.FindFirstObjectByType<WorldFloor>();
            if (floorCatch != null) Set(wiring, "_worldReturn", new SerializedObject(floorCatch).FindProperty("_returnTo").objectReferenceValue);
            Set(wiring, "_panel", panel);
            Set(wiring, "_glow", glow);
            Set(wiring, "_flight", Object.FindFirstObjectByType<Flight.FlightController>());
            Set(wiring, "_carrot", AssetDatabase.LoadAssetAtPath<GameObject>(ART + "prefabs_crops/Seed_Carrot.prefab").GetComponent<SeedDefinition>());
            Set(wiring, "_voidFloor", voidFloor.GetComponent<TutorialStage>());
            Set(wiring, "_plot", plot.GetComponent<TutorialStage>());
            Set(wiring, "_hut", hut.GetComponent<TutorialStage>());
            Set(wiring, "_barrel", barrel.GetComponent<TutorialStage>());
            Set(wiring, "_scoreboard", board.GetComponent<TutorialStage>());
            Set(wiring, "_sellPoint", sell.GetComponent<TutorialStage>());
            Set(wiring, "_seedTypes", show);
            Set(wiring, "_disc", disc.GetComponent<TutorialStage>());
            Set(wiring, "_scroll", scroll);
            Set(wiring, "_seed", seed);
            Set(wiring, "_produce", produce);
            Set(wiring, "_claimZone", claimZone);
            Set(wiring, "_plantZone", plantZoneComponent);
            Set(wiring, "_sellZone", sellZone);
            Set(wiring, "_hutDoor", door != null ? door.gameObject : null);
            Set(wiring, "_namedDeed", namedDeed != null ? namedDeed.gameObject : null);
            Set(wiring, "_namedDeedText", namedDeed != null ? namedDeed.GetComponentInChildren<TMP_Text>(true) : null);
            SerializedProperty fire = wiring.FindProperty("_fire");
            fire.arraySize = fireplace != null ? 1 : 0;
            if (fireplace != null) fire.GetArrayElementAtIndex(0).objectReferenceValue = fireplace.gameObject;
            Set(wiring, "_barrelName", labels?.Find("Name")?.GetComponent<TMP_Text>());
            Set(wiring, "_barrelTagline", labels?.Find("Tagline")?.GetComponent<TMP_Text>());
            Set(wiring, "_barrelBuy", labels?.Find("PriceBuy")?.GetComponent<TMP_Text>());
            Set(wiring, "_barrelSell", labels?.Find("PriceSell")?.GetComponent<TMP_Text>());
            Set(wiring, "_voice", voice);
            SerializedProperty clips = wiring.FindProperty("_voiceClips");
            List<AudioClip> lines = VoiceLines();
            clips.arraySize = lines.Count;
            for (int i = 0; i < lines.Count; i++) clips.GetArrayElementAtIndex(i).objectReferenceValue = lines[i];
            Set(wiring, "_scoreboardText", scoreboardText);
            Set(wiring, "_dirtPile", dirtPile);
            wiring.ApplyModifiedPropertiesWithoutUndo();

            // Everything but the sky, the floor and the walls starts hidden; the platform shows them as it goes.
            foreach (GameObject hidden in new[] { plot, hut, barrel, board, sell, disc, scroll.gameObject, seed.gameObject, produce.gameObject })
                hidden.SetActive(false);
            if (namedDeed != null) namedDeed.gameObject.SetActive(false);
            if (fireplace != null) fireplace.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Logger.Info($"Build() — tutorial platform built at {POSITION}");
        }

        // The barrel's labels face the player at this turn (its data labels are turned half round on the prefab).
        private const float BARREL_YAW = 180f;

        /// <summary>Every recorded line: sounds/tutorial_N (or tutorial-N); the old narrator's named clips are skipped.</summary>
        private static List<AudioClip> VoiceLines()
        {
            var lines = new List<AudioClip>();
            foreach (string guid in AssetDatabase.FindAssets("tutorial t:AudioClip", new[] { ART + "sounds" }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (TutorialPlatform.LineNumber(clip, out _)) lines.Add(clip);
            }
            return lines;
        }

        // ── Pieces ────────────────────────────────────────────────────────────────

        private struct Materials { public Material Sky, Floor, Disc, Glow, Smoke; }

        private static Materials LoadMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MATERIALS)) AssetDatabase.CreateFolder(ART + "Materials", "Tutorial");
            var m = new Materials
            {
                Sky = Asset("TutorialSky", Shader.Find("GrowAGarden/TutorialVoid"), mat =>
                {
                    mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
                    mat.SetFloat("_Cell", 6f);
                    mat.SetFloat("_GridStrength", 0.18f);
                    mat.SetFloat("_Fade", 200f);
                }),
                Floor = Asset("TutorialFloor", Shader.Find("GrowAGarden/TutorialVoid"), mat =>
                {
                    mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
                    mat.SetFloat("_Cell", 1f);
                    mat.SetFloat("_GridStrength", 0.45f);
                    mat.SetFloat("_Fade", 18f);
                }),
                Disc = Asset("TutorialDisc", Shader.Find("Universal Render Pipeline/Lit"), mat =>
                {
                    mat.SetColor("_BaseColor", new Color(0.93f, 0.96f, 1f));
                    mat.SetFloat("_Smoothness", 0.8f);
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.1f, 0.3f, 0.6f));
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }),
                Glow = Asset("TutorialGlow", Shader.Find("GrowAGarden/TutorialGlow"), null),
                Smoke = SmokeMaterial(),
            };
            AssetDatabase.SaveAssets();
            return m;
        }

        private static Material Asset(string name, Shader shader, System.Action<Material> setUp)
        {
            string path = $"{MATERIALS}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            setUp?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material SmokeMaterial()
        {
            string path = $"{MATERIALS}/TutorialSmoke.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var template = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
            mat = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.8f));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static GameObject Drawn(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static void Floor(Transform root)
        {
            var floor = new GameObject("FloorCollider");
            floor.transform.SetParent(root, false);
            floor.transform.localPosition = Vector3.down * 0.5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(WALL_RADIUS * 2f + 2f, 1f, WALL_RADIUS * 2f + 2f);
        }

        private static void Walls(Transform root)
        {
            var walls = new GameObject("Walls");
            walls.transform.SetParent(root, false);
            const int count = 24;
            float width = 2f * Mathf.PI * WALL_RADIUS / count + 0.5f;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 360f / count;
                var wall = new GameObject($"Wall {i}");
                wall.transform.SetParent(walls.transform, false);
                wall.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                wall.transform.localPosition = wall.transform.localRotation * Vector3.forward * WALL_RADIUS + Vector3.up * 4f;
                wall.AddComponent<BoxCollider>().size = new Vector3(width, 8f, 0.5f);
            }
        }

        private static SmokePuff Smoke(Transform root, Material material)
        {
            var go = new GameObject("Smoke", typeof(ParticleSystem));
            go.transform.SetParent(root, false);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = 0.6f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, 0.85f);
            main.gravityModifier = -0.03f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1000;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.93f, 1f), 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.8f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.AddComponent<SmokePuff>();
        }

        private static void Stage(GameObject go, SmokePuff smoke) => Set(go.AddComponent<TutorialStage>(), "_smoke", smoke);

        private static Transform Empty(string name, Transform parent, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        private static TMP_Text Label(string name, Transform parent, Vector3 local, float size, string text, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            var label = go.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = size;
            label.color = colour;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)go.transform).sizeDelta = new Vector2(3f, 1f);
            var notifier = Object.FindFirstObjectByType<Notifications.Notifier>();
            if (notifier != null && new SerializedObject(notifier).FindProperty("_font").objectReferenceValue is TMP_FontAsset font) label.font = font;
            return label;
        }

        /// <summary>Turns a piece a whole quarter at a time so the wall holding <paramref name="door"/> faces the arrival point (towards -z).</summary>
        private static void FaceTowardsPlayer(Transform piece, Transform door, BoxCollider inside)
        {
            if (door == null) return;
            Renderer r = door.GetComponent<Renderer>();
            Vector3 local = piece.InverseTransformPoint(r != null ? r.bounds.center : door.position);
            Vector3 half = inside != null ? Vector3.Scale(inside.size, inside.transform.lossyScale) * 0.5f : Vector3.one;
            bool alongX = Mathf.Abs(local.x) / Mathf.Max(0.01f, half.x) >= Mathf.Abs(local.z) / Mathf.Max(0.01f, half.z);
            Vector3 wall = alongX ? new Vector3(Mathf.Sign(local.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(local.z));
            float turn = Vector3.SignedAngle(piece.rotation * wall, Vector3.back, Vector3.up);
            piece.Rotate(0f, Mathf.Round(turn / 90f) * 90f, 0f, Space.World);
        }

        /// <summary>A stripped copy of a prefab ("folder:name"): only what draws survives, plus colliders on the named children ("" is the root).</summary>
        private static GameObject Copy(string source, string name, Transform parent, Vector3 local, float yaw, string[] remove = null, string[] keepColliders = null)
        {
            string[] parts = source.Split(':');
            string folder = parts[0] == "prefabs_world" ? "Prefabs_world" : parts[0];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ART}{folder}/{parts[1]}.prefab");
            var go = (GameObject)Object.Instantiate(prefab);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.SetActive(true);

            if (remove != null)
                foreach (string path in remove)
                {
                    Transform child = go.transform.Find(path);
                    if (child != null) Object.DestroyImmediate(child.gameObject);
                }

            var colliderKeepers = new HashSet<Transform>();
            if (keepColliders != null)
                foreach (string path in keepColliders)
                {
                    Transform keeper = path.Length == 0 ? go.transform : go.transform.Find(path);
                    if (keeper != null) colliderKeepers.Add(keeper);
                }
            Strip(go, t => colliderKeepers.Contains(t), false);
            PlainMaterials(go);
            return go;
        }

        /// <summary>A stripped copy of a carryable that keeps its grab, as a tutorial item hovering at <paramref name="at"/>.</summary>
        private static TutorialItem Item(string source, string name, Transform parent, Vector3 at, SmokePuff smoke, Quaternion? rotation = null)
        {
            string[] parts = source.Split(':');
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ART}{parts[0]}/{parts[1]}.prefab");
            var go = (GameObject)Object.Instantiate(prefab);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = at;
            go.transform.rotation = rotation ?? Quaternion.identity;
            Strip(go, t => true, true);
            ClearPersistentEvents(go.GetComponent("XRGrabInteractable"));
            var body = go.GetComponent<Rigidbody>();
            if (body != null) { body.isKinematic = true; body.useGravity = false; }
            TutorialItem item = go.AddComponent<TutorialItem>();
            Set(go.AddComponent<TutorialStage>(), "_smoke", smoke);
            return item;
        }

        /// <summary>Removes every component not kept, over as many passes as their dependencies need.</summary>
        private static void Strip(GameObject go, System.Func<Transform, bool> keepCollidersOn, bool keepGrab)
        {
            for (int pass = 0; pass < 6; pass++)
            {
                bool removed = false;
                foreach (Component c in go.GetComponentsInChildren<Component>(true))
                {
                    if (c == null) continue;
                    string type = c.GetType().Name;
                    if (KEEP.Contains(type)) continue;
                    if (c is Collider && keepCollidersOn(c.transform)) continue;
                    if (keepGrab && (type == "XRGrabInteractable" || c is Rigidbody)) continue;
                    if (TryDestroy(c)) removed = true;
                }
                if (!removed) break;
            }
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        }

        private static bool TryDestroy(Component c)
        {
            if (!CanRemove(c)) return false;
            Object.DestroyImmediate(c);
            return true;
        }

        /// <summary>False while another component on the object still requires this one.</summary>
        private static bool CanRemove(Component c)
        {
            foreach (Component other in c.GetComponents<Component>())
            {
                if (other == null || other == c) continue;
                foreach (RequireComponent require in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                    if (Requires(require, c.GetType())) return false;
            }
            return true;
        }

        private static bool Requires(RequireComponent r, System.Type type) =>
            (r.m_Type0 != null && r.m_Type0.IsAssignableFrom(type)) || (r.m_Type1 != null && r.m_Type1.IsAssignableFrom(type)) || (r.m_Type2 != null && r.m_Type2.IsAssignableFrom(type));

        /// <summary>Light-show materials for their plain versions (X for X_Plain), so the copy never dances.</summary>
        private static void PlainMaterials(GameObject go)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    var plain = AssetDatabase.LoadAssetAtPath<Material>($"{ART}Materials/{materials[i].name}_Plain.mat");
                    if (plain != null) { materials[i] = plain; changed = true; }
                }
                if (changed) r.sharedMaterials = materials;
            }
        }

        private static void ClearPersistentEvents(Component c)
        {
            if (c == null) return;
            var so = new SerializedObject(c);
            SerializedProperty p = so.GetIterator();
            while (p.Next(true))
                if (p.name == "m_Calls" && p.isArray) p.arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            Set(so, field, value);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Logger.Warn($"Set() — no field '{field}' on {so.targetObject.GetType().Name}"); return; }
            p.objectReferenceValue = value;
        }
    }
}
