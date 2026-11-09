using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Vulps.GrowAGarden.Video
{
    /// <summary>
    /// Temporary (Vulps, 2026-11-03, for one session): every Garden's screen but world spawn's is hidden and replaced by a
    /// notice. The players are left running and synced; only what they draw is hidden. Remove once multi-screen works.
    /// See docs/popcorn.md.
    /// </summary>
    public class ScreenStandIn : MonoBehaviour
    {
        [Tooltip("The Garden at world spawn, whose screen stays.")]
        [SerializeField] private Transform _spawnGarden;
        [Tooltip("The notice's font.")]
        [SerializeField] private TMP_FontAsset _font;
        [TextArea]
        [SerializeField] private string _text = "Multi-screen support coming soon\nin the mean time join the party at world spawn\n(you will need another popcorn seed)";

        private readonly List<ScreenManager> _hidden = new List<ScreenManager>();
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Canvas> _canvases = new List<Canvas>();
        private float _nextCollect;

        private void Start()
        {
            ScreenManager keep = _spawnGarden == null ? null : ScreenManager.For(_spawnGarden.position);
            foreach (ScreenManager screen in FindObjectsByType<ScreenManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (screen == keep) continue;
                _hidden.Add(screen);
                Notice(screen.transform, 0f);
                Notice(screen.transform, 180f);
            }
            Logger.Info($"Start() '{gameObject.name}' — {_hidden.Count} screen(s) replaced by the notice; world spawn's kept");
        }

        /// <summary>The notice where the screen stands, one face each way, in a holder of its own so the player's stretch does not skew it.</summary>
        private void Notice(Transform screen, float turn)
        {
            var holder = new GameObject("Screen Notice (temporary)");
            holder.transform.SetPositionAndRotation(screen.position, screen.rotation * Quaternion.Euler(0f, turn, 0f));
            holder.transform.SetParent(screen.parent, true);

            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshPro>();
            text.transform.SetParent(holder.transform, false);
            text.rectTransform.sizeDelta = new Vector2(4.4f, 2.4f);
            if (_font != null) text.font = _font;
            text.text = _text;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.5f;
            text.fontSizeMax = 3f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 255);
            // TextMeshPro draws both sides, so each face would show the other's text mirrored through it: front only.
            text.fontMaterial.SetFloat(ShaderUtilities.ShaderTag_CullMode, (float)UnityEngine.Rendering.CullMode.Back);
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextCollect)
            {
                // Somnium builds the player's screen and controls at runtime: look again now and then.
                _nextCollect = Time.unscaledTime + 1f;
                _renderers.Clear();
                _canvases.Clear();
                foreach (ScreenManager screen in _hidden)
                {
                    if (screen == null) continue;
                    _renderers.AddRange(screen.GetComponentsInChildren<Renderer>(true));
                    _canvases.AddRange(screen.GetComponentsInChildren<Canvas>(true));
                }
            }
            foreach (Renderer r in _renderers) if (r != null && !r.forceRenderingOff) r.forceRenderingOff = true;
            foreach (Canvas c in _canvases) if (c != null && c.enabled) c.enabled = false;
        }
    }
}
