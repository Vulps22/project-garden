using UnityEditor;
using UnityEngine;

namespace GrowAGarden.EditorTools
{
    /// <summary>
    /// Drives every CloudGenerator's edit-mode preview from outside the class, so CloudGenerator
    /// itself never references UnityEditor — see that class's note on why an #if UNITY_EDITOR
    /// guard inside it isn't enough to keep it off Somnium's editor-only-code scan.
    ///
    /// One global tick rather than a per-instance EditorApplication.update subscription: simpler
    /// bookkeeping (no subscribe/unsubscribe to get wrong across OnEnable/OnDisable/OnValidate),
    /// and one shared wall-clock delta is all any instance needs.
    /// </summary>
    [InitializeOnLoad]
    internal static class CloudGeneratorEditorPreview
    {
        private static double _lastEditorTime;

        static CloudGeneratorEditorPreview()
        {
            _lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            // Clamped so refocusing after a long idle doesn't age every cloud through its whole
            // life in one jump — see CloudGenerator's own retry-timer notes for the same trap.
            float dt = Mathf.Clamp((float)(now - _lastEditorTime), 0f, 0.1f);
            _lastEditorTime = now;

            if (Application.isPlaying) return;

            bool anyTicked = false;
            foreach (CloudGenerator generator in Object.FindObjectsByType<CloudGenerator>(FindObjectsSortMode.None))
            {
                if (generator == null || !generator.isActiveAndEnabled || !generator.PreviewInEditor) continue;
                generator.EditorPreviewTick(dt);
                anyTicked = true;
            }

            if (anyTicked) SceneView.RepaintAll();
        }
    }
}
