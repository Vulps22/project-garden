using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GrowAGarden.EditorTools
{
    /// <summary>
    /// Draws a wireframe box at the origin while a prefab whose name starts with "Island" is open
    /// in Prefab Mode, so a new island can be scaled against the one that already works.
    ///
    /// Nothing is added to any prefab. A procgen island is going to be authored and re-authored a
    /// lot, and a reference object living inside the asset is a thing to forget to delete, to
    /// accidentally parent something to, or to ship. This hangs off SceneView.duringSceneGui and
    /// touches no asset at all, which also means it cannot possibly reach an exported world — on
    /// top of already being in the Editor-only assembly.
    ///
    /// The sizes come from GardenIsland.prefab, the island the game is currently balanced around:
    /// renderer bounds 42.5 x 57.1 x 38.4, spanning y -49.0 to +8.1. Rounded to 40 and 60 because
    /// this is a sense-of-scale reference rather than a spec, and a box you can eyeball as "forty
    /// across, sixty tall" is worth more than one that is exact. Note the real island's height is
    /// mostly the rocky underside — its colliders are only 20.6 x 22.6 x 16.7 — so the footprint
    /// is the number worth matching and the height is context.
    /// </summary>
    [InitializeOnLoad]
    internal static class IslandScaleReference
    {
        /// <summary>Prefab name prefix that turns the box on. Matches IslandFragment_* too.</summary>
        private const string Prefix = "Island";

        /// <summary>
        /// Every combination of the two rounded dimensions. Which proportions read as "an island"
        /// rather than "a pillar" or "a plate" is not something to be worked out on paper, so the
        /// menu offers all eight and lets the answer be found by flipping between them.
        /// Ordered X-major so the menu reads like a counter and the shape you want is findable.
        /// </summary>
        private static readonly Vector3[] Sizes =
        {
            new Vector3(40f, 40f, 40f),
            new Vector3(40f, 40f, 60f),
            new Vector3(40f, 60f, 40f),
            new Vector3(40f, 60f, 60f),
            new Vector3(60f, 40f, 40f),
            new Vector3(60f, 40f, 60f),
            new Vector3(60f, 60f, 40f),
            new Vector3(60f, 60f, 60f),
        };

        /// <summary>40 x 60 x 40 — the closest of the eight to the measured island.</summary>
        private const int DefaultIndex = 2;

        private const string Root = "Grow A Garden/Island Scale Box/";
        private const string ShownKey = "GrowAGarden.IslandScaleReference.Shown";
        private const string SizeKey = "GrowAGarden.IslandScaleReference.SizeIndex";

        private static bool Shown
        {
            get => EditorPrefs.GetBool(ShownKey, true);
            set { EditorPrefs.SetBool(ShownKey, value); SceneView.RepaintAll(); }
        }

        private static int SizeIndex
        {
            get => Mathf.Clamp(EditorPrefs.GetInt(SizeKey, DefaultIndex), 0, Sizes.Length - 1);
            set { EditorPrefs.SetInt(SizeKey, value); SceneView.RepaintAll(); }
        }

        private static Vector3 Size => Sizes[SizeIndex];

        static IslandScaleReference()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
        }

        // ── Menu ──────────────────────────────────────────────────────────────────
        //
        // One pair of methods per size, because a MenuItem path has to be a compile-time
        // constant — there is no way to build these in a loop. The priority gap between Show and
        // the sizes is what draws the separator.

        [MenuItem(Root + "Show", false, 0)] private static void ToggleShown() => Shown = !Shown;
        [MenuItem(Root + "Show", true, 0)] private static bool ToggleShownValidate() => Check(Root + "Show", Shown);

        [MenuItem(Root + "40 x 40 x 40", false, 20)] private static void Set0() => SizeIndex = 0;
        [MenuItem(Root + "40 x 40 x 40", true, 20)] private static bool Set0V() => Check(Root + "40 x 40 x 40", SizeIndex == 0);

        [MenuItem(Root + "40 x 40 x 60", false, 21)] private static void Set1() => SizeIndex = 1;
        [MenuItem(Root + "40 x 40 x 60", true, 21)] private static bool Set1V() => Check(Root + "40 x 40 x 60", SizeIndex == 1);

        [MenuItem(Root + "40 x 60 x 40", false, 22)] private static void Set2() => SizeIndex = 2;
        [MenuItem(Root + "40 x 60 x 40", true, 22)] private static bool Set2V() => Check(Root + "40 x 60 x 40", SizeIndex == 2);

        [MenuItem(Root + "40 x 60 x 60", false, 23)] private static void Set3() => SizeIndex = 3;
        [MenuItem(Root + "40 x 60 x 60", true, 23)] private static bool Set3V() => Check(Root + "40 x 60 x 60", SizeIndex == 3);

        [MenuItem(Root + "60 x 40 x 40", false, 24)] private static void Set4() => SizeIndex = 4;
        [MenuItem(Root + "60 x 40 x 40", true, 24)] private static bool Set4V() => Check(Root + "60 x 40 x 40", SizeIndex == 4);

        [MenuItem(Root + "60 x 40 x 60", false, 25)] private static void Set5() => SizeIndex = 5;
        [MenuItem(Root + "60 x 40 x 60", true, 25)] private static bool Set5V() => Check(Root + "60 x 40 x 60", SizeIndex == 5);

        [MenuItem(Root + "60 x 60 x 40", false, 26)] private static void Set6() => SizeIndex = 6;
        [MenuItem(Root + "60 x 60 x 40", true, 26)] private static bool Set6V() => Check(Root + "60 x 60 x 40", SizeIndex == 6);

        [MenuItem(Root + "60 x 60 x 60", false, 27)] private static void Set7() => SizeIndex = 7;
        [MenuItem(Root + "60 x 60 x 60", true, 27)] private static bool Set7V() => Check(Root + "60 x 60 x 60", SizeIndex == 7);

        private static bool Check(string path, bool on)
        {
            Menu.SetChecked(path, on);
            return true;
        }

        // ── Drawing ───────────────────────────────────────────────────────────────

        private static void OnSceneGui(SceneView view)
        {
            if (!Shown) return;
            if (Event.current.type != EventType.Repaint) return;

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) return;

            // The asset's filename, not the root GameObject's name — the root can be renamed in
            // the scene view, and the file is what actually identifies the prefab.
            string name = System.IO.Path.GetFileNameWithoutExtension(stage.assetPath);
            if (!name.StartsWith(Prefix, System.StringComparison.OrdinalIgnoreCase)) return;

            Vector3 size = Size;
            Color previous = Handles.color;

            // Drawn twice: solid where it is in front of geometry, faint where it is behind, so
            // the box stays readable once an island is big enough to swallow it.
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.color = new Color(0.25f, 0.6f, 1f, 1f);
            Handles.DrawWireCube(Vector3.zero, size);

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Greater;
            Handles.color = new Color(0.25f, 0.6f, 1f, 0.2f);
            Handles.DrawWireCube(Vector3.zero, size);

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.color = new Color(0.25f, 0.6f, 1f, 0.9f);
            Handles.Label(new Vector3(0f, size.y * 0.5f, 0f),
                          $"island scale  {size.x:0.#} x {size.y:0.#} x {size.z:0.#}");

            Handles.color = previous;
        }
    }
}
