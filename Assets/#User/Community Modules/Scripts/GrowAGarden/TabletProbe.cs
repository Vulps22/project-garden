using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Vulps.GrowAGarden.DevTools;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden
{
    /// <summary>
    /// Temporary: logs everything Unity can see of Somnium's tablet, so the whole structure can be
    /// documented. Runs only for the developer, and only while they are the master client. A full snapshot when the tablet first exists, then, whenever something opens or
    /// closes, the parts that switched on in full and the parts that switched off by path.
    /// </summary>
    public static class TabletProbe
    {
        private const int MAX_DUMPS = 300;
        private const int MAX_CHILDREN = 40;
        private const int LINES_PER_ENTRY = 120;

        private static Transform _tablet;
        private static HashSet<Transform> _active = new HashSet<Transform>();
        private static int _dumps;
        private static int _snapshot;

        private static readonly HashSet<Transform> _found = new HashSet<Transform>();

        /// <summary>
        /// Call every few seconds while the map has not been found: every object anywhere named like a tablet, and a
        /// snapshot of each new one. For the 2D tablet, whose name and place are not known. Developer only.
        /// </summary>
        public static void Search()
        {
            if (_dumps >= MAX_DUMPS || !Developer.IsLocal) return;

            var now = new List<Transform>();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.IndexOf("tablet", System.StringComparison.OrdinalIgnoreCase) >= 0 && !t.name.StartsWith("GAG_") && !UnderTablet(t))
                    now.Add(t);

            foreach (Transform t in now)
            {
                if (!_found.Add(t)) continue;
                _tablet = t;
                _snapshot++;
                _dumps++;
                var chain = new StringBuilder();
                for (Transform up = t.parent; up != null; up = up.parent) chain.Insert(0, up.name + "/");
                var lines = new List<string> { $"search #{_snapshot}: '{t.name}' under '{chain}' active={t.gameObject.activeInHierarchy} scene='{t.gameObject.scene.name}' world pos={t.position:F3} layer={LayerMask.LayerToName(t.gameObject.layer)} (2D={!PlayerManager.LocalIsVR})" };
                Describe(t, 0, lines);
                Emit(lines);
            }
        }

        /// <summary>Whether a parent is itself named like a tablet, so only the topmost of a tablet's parts is reported.</summary>
        private static bool UnderTablet(Transform t)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
                if (p.name.IndexOf("tablet", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>Call about once a second with the map's content panel, which lives inside the tablet.</summary>
        public static void Probe(RectTransform content)
        {
            if (_dumps >= MAX_DUMPS || content == null) return;
            if (!Developer.IsLocalMaster) return;   // only Vulps, only as master: nobody else's log fills with it

            // The topmost ancestor named Tablet.*: Tablet.XR(Clone), above Canvases. Stopping at the first
            // gap in the names missed everything beside the tabs, the sidebar included.
            Transform tablet = null;
            for (Transform t = content; t != null; t = t.parent)
                if (t.name.StartsWith("Tablet.")) tablet = t;
            if (tablet == null) return;

            if (tablet != _tablet)
            {
                _tablet = tablet;
                _active = ActiveSet(tablet);
                Snapshot(tablet);
                return;
            }

            HashSet<Transform> now = ActiveSet(tablet);
            if (now.SetEquals(_active)) return;

            var on = new List<Transform>();
            var off = new List<string>();
            foreach (Transform t in now)
                if (!_active.Contains(t) && (t.parent == null || !now.Contains(t.parent) || _active.Contains(t.parent))) on.Add(t);
            foreach (Transform t in _active)
                if (t != null && !now.Contains(t) && (t.parent == null || now.Contains(t.parent))) off.Add(PathOf(t));
            _active = now;

            _snapshot++;
            _dumps++;
            var lines = new List<string> { $"change #{_snapshot}: {on.Count} part(s) on, {off.Count} off" };
            foreach (string p in off) lines.Add($"OFF {p}");
            foreach (Transform t in on)
            {
                lines.Add($"ON {PathOf(t)}");
                Describe(t, 1, lines);
            }
            Emit(lines);
        }

        private static void Snapshot(Transform tablet)
        {
            _snapshot++;
            _dumps++;
            var lines = new List<string>();
            Transform up = tablet.parent;
            var chain = new StringBuilder();
            while (up != null) { chain.Insert(0, up.name + "/"); up = up.parent; }
            lines.Add($"snapshot #{_snapshot} of '{tablet.name}' under '{chain}' world pos={tablet.position:F3} rot={tablet.eulerAngles:F1} lossyScale={tablet.lossyScale:F4} layer={LayerMask.LayerToName(tablet.gameObject.layer)}");
            Describe(tablet, 0, lines);
            Emit(lines);
        }

        /// <summary>Everything switched on, leaving out the map's own scrolling content, which changes as the map pans.</summary>
        private static HashSet<Transform> ActiveSet(Transform tablet)
        {
            var set = new HashSet<Transform>();
            foreach (Transform t in tablet.GetComponentsInChildren<Transform>(false))
                if (!InsideMapContent(t)) set.Add(t);
            return set;
        }

        private static bool InsideMapContent(Transform t)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
                if (p.name == "Content" && p.parent != null && p.parent.name == "Viewport" && p.parent.parent != null && p.parent.parent.name == "MapView") return true;
            return false;
        }

        private static void Describe(Transform t, int depth, List<string> lines)
        {
            if (t.name.StartsWith("GAG_")) { lines.Add($"{Indent(depth)}{t.name} (ours, skipped)"); return; }
            lines.Add(Indent(depth) + Node(t));

            int shown = 0;
            foreach (Transform child in t)
            {
                if (shown++ >= MAX_CHILDREN) { lines.Add($"{Indent(depth + 1)}… {t.childCount - MAX_CHILDREN} more children"); break; }
                Describe(child, depth + 1, lines);
            }
        }

        private static string Node(Transform t)
        {
            var sb = new StringBuilder();
            sb.Append(t.name);
            sb.Append(t.gameObject.activeSelf ? "" : " (off)");
            sb.Append($" layer={LayerMask.LayerToName(t.gameObject.layer)}");

            if (t is RectTransform r)
                sb.Append($" rect[anchors={r.anchorMin:F2}-{r.anchorMax:F2} pivot={r.pivot:F2} pos={r.anchoredPosition:F1} size={r.sizeDelta:F1} actual={r.rect.size:F1}]");
            else
                sb.Append($" pos={t.localPosition:F3}");
            if (t.localScale != Vector3.one) sb.Append($" scale={t.localScale:F3}");
            if (t.localRotation != Quaternion.identity) sb.Append($" rot={t.localEulerAngles:F1}");

            var types = new List<string>();
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null) { types.Add("MISSING"); continue; }
                if (c is Transform) continue;
                types.Add(c.GetType().FullName);
                Detail(c, sb);
            }
            if (types.Count > 0) sb.Append(" {").Append(string.Join(", ", types)).Append('}');
            return sb.ToString();
        }

        private static void Detail(Component c, StringBuilder sb)
        {
            switch (c)
            {
                case TMP_Text text:
                    sb.Append($" text=\"{Clip(text.text)}\" font={(text.font ? text.font.name : "-")} size={text.fontSize:F1} colour=#{ColorUtility.ToHtmlStringRGBA(text.color)} align={text.alignment}");
                    break;
                case Text legacy:
                    sb.Append($" uitext=\"{Clip(legacy.text)}\" size={legacy.fontSize}");
                    break;
                case Image image:
                    sb.Append($" image[sprite={(image.sprite ? image.sprite.name : "-")} colour=#{ColorUtility.ToHtmlStringRGBA(image.color)} type={image.type} raycast={image.raycastTarget}{(image.material && image.material != image.defaultMaterial ? " mat=" + image.material.name : "")}]");
                    break;
                case RawImage raw:
                    sb.Append($" rawimage[tex={(raw.texture ? raw.texture.name : "-")}]");
                    break;
                case Button button:
                    sb.Append($" button[interactable={button.interactable}{Listeners(button.onClick)}]");
                    break;
                case Toggle toggle:
                    sb.Append($" toggle[on={toggle.isOn} group={(toggle.group ? toggle.group.name : "-")}{Listeners(toggle.onValueChanged)}]");
                    break;
                case Slider slider:
                    sb.Append($" slider[{slider.minValue}..{slider.maxValue} value={slider.value}]");
                    break;
                case TMP_InputField input:
                    sb.Append($" input[\"{Clip(input.text)}\" type={input.contentType}]");
                    break;
                case ScrollRect scroll:
                    sb.Append($" scroll[h={scroll.horizontal} v={scroll.vertical} content={(scroll.content ? scroll.content.name : "-")} movement={scroll.movementType}]");
                    break;
                case HorizontalOrVerticalLayoutGroup layout:
                    sb.Append($" layout[spacing={layout.spacing} padding={layout.padding.left},{layout.padding.right},{layout.padding.top},{layout.padding.bottom} align={layout.childAlignment} expand={layout.childForceExpandWidth}/{layout.childForceExpandHeight} control={layout.childControlWidth}/{layout.childControlHeight}]");
                    break;
                case GridLayoutGroup grid:
                    sb.Append($" grid[cell={grid.cellSize} spacing={grid.spacing} constraint={grid.constraint}:{grid.constraintCount}]");
                    break;
                case LayoutElement element:
                    sb.Append($" element[min={element.minWidth}x{element.minHeight} pref={element.preferredWidth}x{element.preferredHeight} flex={element.flexibleWidth}x{element.flexibleHeight} ignore={element.ignoreLayout}]");
                    break;
                case ContentSizeFitter fitter:
                    sb.Append($" fitter[{fitter.horizontalFit}/{fitter.verticalFit}]");
                    break;
                case Canvas canvas:
                    sb.Append($" canvas[mode={canvas.renderMode} sort={canvas.sortingOrder} override={canvas.overrideSorting} camera={(canvas.worldCamera ? canvas.worldCamera.name : "-")} scale={canvas.scaleFactor}]");
                    break;
                case CanvasGroup group:
                    sb.Append($" group[alpha={group.alpha} interactable={group.interactable} blocks={group.blocksRaycasts}]");
                    break;
                case Mask _:
                    sb.Append(" mask");
                    break;
                case Collider collider:
                    sb.Append($" collider[{collider.bounds.size:F3} trigger={collider.isTrigger}]");
                    break;
            }
        }

        /// <summary>The handlers wired in the inspector; ones added in code are invisible to Unity's API.</summary>
        private static string Listeners(UnityEventBase e)
        {
            int n = e.GetPersistentEventCount();
            if (n == 0) return " onClick=none-persistent";
            var sb = new StringBuilder(" calls=");
            for (int i = 0; i < n; i++)
            {
                Object target = e.GetPersistentTarget(i);
                sb.Append(i > 0 ? "; " : "").Append(target ? $"{target.GetType().Name}({target.name})" : "null").Append('.').Append(e.GetPersistentMethodName(i));
            }
            return sb.ToString();
        }

        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null && p != _tablet.parent; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        private static string Clip(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", "\\n").Replace("\r", "");
            return s.Length > 80 ? s.Substring(0, 80) + "…" : s;
        }

        private static string Indent(int depth) => new string(' ', depth * 2);

        /// <summary>Writes in chunks so one log entry stays readable.</summary>
        private static void Emit(List<string> lines)
        {
            for (int i = 0; i < lines.Count; i += LINES_PER_ENTRY)
            {
                int n = Mathf.Min(LINES_PER_ENTRY, lines.Count - i);
                Logger.Info($"Probe() 'TabletProbe' — #{_snapshot} part {i / LINES_PER_ENTRY + 1}/{(lines.Count + LINES_PER_ENTRY - 1) / LINES_PER_ENTRY}\n{string.Join("\n", lines.GetRange(i, n))}");
            }
        }
    }
}
