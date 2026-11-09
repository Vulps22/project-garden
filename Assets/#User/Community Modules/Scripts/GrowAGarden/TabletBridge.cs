using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Vulps.GrowAGarden
{
    /// <summary>
    /// The one place that knows the layout of Somnium's tablet outside the map (docs/tablet.md). Every lookup fails
    /// quietly, returning null, so a Somnium update that renames something leaves the tablet as Somnium made it.
    /// </summary>
    public static class TabletBridge
    {
        private const string TABLET = "Tablet.XR(Clone)";
        private const string TABS = "Canvases/Tablet.XR.Tabs";
        private const string HOME = TABS + "/Tablet.XR.HomeTab";
        private const string HOME_GRID = HOME + "/ContentView";
        private const string TEMPLATE_BUTTON = HOME_GRID + "/Tablet.XR.SwitchTabBtn - Map";

        /// <summary>The local player's tablet under their rig, or null when it is not there yet.</summary>
        public static Transform FindTablet(Transform rigRoot)
        {
            if (rigRoot == null) return null;
            Transform tablet = rigRoot.Find(TABLET);
            if (tablet == null && rigRoot.parent != null) tablet = rigRoot.parent.Find("XR.Body/" + TABLET);
            return tablet;
        }

        /// <summary>Adds a button to the home grid, cloned from Somnium's Map button but opening nothing of theirs. Null if the grid has moved.</summary>
        public static Button AddHomeButton(Transform tablet, string name, Sprite icon, string label)
        {
            Transform template = tablet == null ? null : tablet.Find(TEMPLATE_BUTTON);
            if (template == null) return null;

            Transform existing = template.parent.Find(name);
            if (existing != null) Object.Destroy(existing.gameObject);

            // Cloned under an inactive holder so none of Somnium's scripts on it wake before they are stripped.
            var holder = new GameObject("TabletBridge_Holder");
            holder.SetActive(false);
            GameObject clone = Object.Instantiate(template.gameObject, holder.transform, false);
            clone.name = name;

            // Somnium's own behaviour comes off: its tab switch would open the Map, its localisation would reset the
            // label, its tooltip would say "Map". The click sound stays.
            foreach (MonoBehaviour mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string type = mb.GetType().Name;
                if (type == "SwitchTabButton" || type == "LocalizeStringEvent" || type == "TabletTooltipForUIObjects") Object.DestroyImmediate(mb);
            }

            clone.transform.SetParent(template.parent, false);
            clone.transform.SetAsLastSibling();
            Object.Destroy(holder);

            Button button = clone.GetComponent<Button>();
            if (button == null) { Object.Destroy(clone); return null; }
            button.onClick = new Button.ButtonClickedEvent();

            Image iconImage = clone.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImage != null && icon != null) iconImage.sprite = icon;
            TMP_Text text = clone.transform.Find("Text")?.GetComponent<TMP_Text>();
            if (text != null) text.text = label;
            return button;
        }

        /// <summary>
        /// Adds a page of our own beside Somnium's apps: a canvas set up like the home screen's, so the hands' rays
        /// can point at it. Starts hidden. Null if the home screen has moved.
        /// </summary>
        public static RectTransform AddPage(Transform tablet, string name)
        {
            Transform home = tablet == null ? null : tablet.Find(HOME);
            Canvas homeCanvas = home == null ? null : home.GetComponent<Canvas>();
            if (homeCanvas == null) return null;

            Transform existing = home.parent.Find(name);
            if (existing != null) Object.Destroy(existing.gameObject);

            var page = new GameObject(name, typeof(RectTransform));
            page.layer = home.gameObject.layer;
            var rect = (RectTransform)page.transform;
            rect.SetParent(home.parent, false);
            var homeRect = (RectTransform)home;
            rect.anchorMin = homeRect.anchorMin;
            rect.anchorMax = homeRect.anchorMax;
            rect.pivot = homeRect.pivot;
            rect.sizeDelta = homeRect.sizeDelta;
            rect.anchoredPosition3D = homeRect.anchoredPosition3D;
            rect.localRotation = homeRect.localRotation;
            rect.localScale = homeRect.localScale;

            Canvas canvas = page.AddComponent<Canvas>();
            canvas.renderMode = homeCanvas.renderMode;
            canvas.worldCamera = homeCanvas.worldCamera;
            canvas.overrideSorting = homeCanvas.overrideSorting;
            canvas.sortingLayerID = homeCanvas.sortingLayerID;
            canvas.sortingOrder = homeCanvas.sortingOrder + 1;

            CanvasScaler homeScaler = home.GetComponent<CanvasScaler>();
            CanvasScaler scaler = page.AddComponent<CanvasScaler>();
            if (homeScaler != null) scaler.dynamicPixelsPerUnit = homeScaler.dynamicPixelsPerUnit;

            page.AddComponent<TrackedDeviceGraphicRaycaster>();
            page.SetActive(false);
            return rect;
        }

        /// <summary>Shows or hides the home screen's grid, which is how Somnium itself switches to and from an app.</summary>
        public static void ShowHome(Transform tablet, bool show)
        {
            Transform grid = tablet == null ? null : tablet.Find(HOME_GRID);
            if (grid != null && grid.gameObject.activeSelf != show) grid.gameObject.SetActive(show);
        }

        /// <summary>Whether Somnium is showing its home screen or any app of its own: our page should step aside.</summary>
        public static bool SomniumPageOpen(Transform tablet, Transform ours)
        {
            Transform tabs = tablet == null ? null : tablet.Find(TABS);
            if (tabs == null) return false;
            foreach (Transform tab in tabs)
            {
                if (tab == ours) continue;
                Transform view = tab.Find("ContentView");
                if (view != null && view.gameObject.activeInHierarchy) return true;
            }
            return false;
        }

        // ── The sidebar ───────────────────────────────────────────────────────────

        private const string SIDEBAR = "Canvases/Tablet.XR.SideBar";
        private const string QUICK_TEMPLATE = "Tablet.XR.TimerQuickActionBtn - Go To";

        /// <summary>
        /// Adds a quick-action button to the sidebar, cloned from World Spawn and placed under it, doing nothing of
        /// Somnium's. Null if the sidebar has moved.
        /// </summary>
        public static Button AddQuickAction(Transform tablet, string name, Sprite icon, string label)
        {
            Transform sidebar = tablet == null ? null : tablet.Find(SIDEBAR);
            Transform template = sidebar == null ? null : FindDeep(sidebar, QUICK_TEMPLATE);
            if (template == null) return null;
            Transform panel = template.parent;
            Transform existing = panel.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject clone = CloneStripped(template.gameObject, name,
                "QuickActionButtonWithTimer", "QuickActionButton", "LocalizeStringEvent", "TabletTooltipForUIObjects");
            Button button = clone.GetComponent<Button>();
            if (button == null) { Object.Destroy(clone); return null; }
            button.onClick = new Button.ButtonClickedEvent();

            Transform timer = clone.transform.Find("Timer");
            if (timer != null) timer.gameObject.SetActive(false);
            Image iconImage = clone.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImage != null && icon != null) iconImage.sprite = icon;
            TMP_Text text = clone.transform.Find("Text")?.GetComponent<TMP_Text>();
            if (text != null) text.text = label;

            clone.transform.SetParent(panel, false);
            clone.transform.SetSiblingIndex(template.GetSiblingIndex() + 1);

            // The panel is a stated height, not fitted: grow it by one button so nothing below is pushed off.
            var panelRect = (RectTransform)panel;
            if (panel.GetComponent<ContentSizeFitter>() == null)
            {
                var layout = panel.GetComponent<VerticalLayoutGroup>();
                float grow = ((RectTransform)template).rect.height + (layout == null ? 0f : layout.spacing);
                panelRect.sizeDelta += new Vector2(0f, grow);
                LayoutElement element = panel.GetComponent<LayoutElement>();
                if (element != null && element.preferredHeight > 0f) element.preferredHeight += grow;
            }
            LayoutRebuilder.MarkLayoutForRebuild(panelRect);
            if (panel.parent is RectTransform content) LayoutRebuilder.MarkLayoutForRebuild(content);
            return button;
        }

        /// <summary>Sets a quick-action button's label.</summary>
        public static void SetQuickActionLabel(Button button, string label)
        {
            TMP_Text text = button == null ? null : button.transform.Find("Text")?.GetComponent<TMP_Text>();
            if (text != null && text.text != label) text.text = label;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // ── Settings (OptionsTab) ─────────────────────────────────────────────────

        private const string OPTIONS = TABS + "/Tablet.XR.OptionsTab/ContentView";
        private const string SELECTOR = OPTIONS + "/SelectorPanel";
        private const string SELECTOR_TEMPLATE = SELECTOR + "/Audio";
        private const string TOGGLE_TEMPLATE = OPTIONS + "/OptionsTab.General/Left/Section - Tablet/Buttons/Option_FolowPlayer";
        private const string SECTION_TEMPLATE = OPTIONS + "/OptionsTab.General/Left/Section - General";

        /// <summary>Somnium's section buttons down the left of Settings (General, Controls, Audio …), or empty.</summary>
        public static Button[] SettingsSections(Transform tablet)
        {
            Transform selector = tablet == null ? null : tablet.Find(SELECTOR);
            return selector == null ? new Button[0] : selector.GetComponentsInChildren<Button>(true);
        }

        /// <summary>Adds a section button to Settings, cloned from Audio's with Somnium's switching stripped. Null if Settings has moved.</summary>
        public static Button AddSettingsSection(Transform tablet, string name, string label, Texture icon)
        {
            Transform template = tablet == null ? null : tablet.Find(SELECTOR_TEMPLATE);
            if (template == null) return null;
            Transform existing = template.parent.Find(name);
            if (existing != null) Object.Destroy(existing.gameObject);

            GameObject clone = CloneStripped(template.gameObject, name, "LocalizeStringEvent");
            Button button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            TMP_Text text = clone.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;
            RawImage iconImage = clone.GetComponentInChildren<RawImage>(true);
            if (iconImage != null && icon != null) iconImage.texture = icon;
            SetSelectorHighlight(button, false);

            clone.transform.SetParent(template.parent, false);
            clone.transform.SetAsLastSibling();

            // Seven buttons overflow Somnium's spacing of 40 in 1080; close them up just enough.
            var layout = template.parent.GetComponent<VerticalLayoutGroup>();
            int count = template.parent.childCount;
            if (layout != null && count > 1)
            {
                float room = ((RectTransform)template.parent).rect.height - count * ((RectTransform)template).rect.height;
                layout.spacing = Mathf.Min(layout.spacing, Mathf.Max(8f, room / (count + 1)));
            }
            return button;
        }

        /// <summary>Shows or hides a section button's highlight, the child Somnium names "…_Highlight".</summary>
        public static void SetSelectorHighlight(Button section, bool on)
        {
            if (section == null) return;
            foreach (Transform child in section.transform)
                if (child.name.EndsWith("_Highlight") && child.gameObject.activeSelf != on) child.gameObject.SetActive(on);
        }

        /// <summary>
        /// Adds a section panel of our own to Settings, a sibling of Somnium's OptionsTab.General, .Audio and the rest,
        /// filling the content area with no background of its own. Starts hidden. Null if Settings has moved.
        /// </summary>
        public static RectTransform AddSettingsPage(Transform tablet, string name)
        {
            Transform options = tablet == null ? null : tablet.Find(OPTIONS);
            if (options == null) return null;
            Transform existing = options.Find(name);
            if (existing != null) Object.Destroy(existing.gameObject);

            var page = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)page.transform;
            rect.SetParent(options, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            page.SetActive(false);
            return rect;
        }

        /// <summary>
        /// Shows one section of Settings the way Somnium's own switching does, with SetActive on the panels and the
        /// highlights: ours (pass its panel and button), or Somnium's by button. Every other section goes off.
        /// </summary>
        public static void ShowSettingsSection(Transform tablet, Transform ourPanel, Button lit)
        {
            Transform options = tablet == null ? null : tablet.Find(OPTIONS);
            if (options == null) return;
            string theirs = ourPanel == null && lit != null ? "OptionsTab." + lit.name : null;
            foreach (Transform section in options)
            {
                if (!section.name.StartsWith("OptionsTab.")) continue;
                bool on = section == ourPanel || section.name == theirs;
                if (section.gameObject.activeSelf != on) section.gameObject.SetActive(on);
            }
            foreach (Button button in SettingsSections(tablet)) SetSelectorHighlight(button, button == lit);
        }

        /// <summary>Whether Somnium's own panel for this section button is showing.</summary>
        public static bool SettingsSectionShowing(Transform tablet, Button section)
        {
            Transform panel = tablet == null || section == null ? null : tablet.Find(OPTIONS + "/OptionsTab." + section.name);
            return panel != null && panel.gameObject.activeSelf;
        }

        /// <summary>A section panel like Settings' own (dark rounded panel and its sprite), or null.</summary>
        public static Sprite SettingsSectionSprite(Transform tablet)
        {
            Image image = tablet == null ? null : tablet.Find(SECTION_TEMPLATE)?.GetComponent<Image>();
            return image == null ? null : image.sprite;
        }

        /// <summary>
        /// Adds one of Settings' on/off rows, cloned from Somnium's with its own option logic stripped: the caller drives
        /// it through the returned Toggle and <see cref="ShowToggle"/>. Null if Settings has moved.
        /// </summary>
        public static Toggle AddSettingsToggle(Transform tablet, Transform parent, string name, string label)
        {
            Transform template = tablet == null ? null : tablet.Find(TOGGLE_TEMPLATE);
            if (template == null || parent == null) return null;

            GameObject clone = CloneStripped(template.gameObject, name, "OptionViewBase", "On_OffToggle", "LocalizeStringEvent");
            Toggle toggle = clone.GetComponentInChildren<Toggle>(true);
            if (toggle == null) { Object.Destroy(clone); return null; }
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.group = null;

            Transform title = clone.transform.Find("Text (TMP)");
            if (title != null && title.TryGetComponent(out TMP_Text t)) t.text = label;
            SetText(toggle.transform.Find("OffText"), "Off");
            SetText(toggle.transform.Find("OnText"), "On");

            clone.transform.SetParent(parent, false);
            clone.SetActive(true);
            return toggle;
        }

        private const string SLIDER_TEMPLATE = "Option_MainVolume";

        /// <summary>
        /// Adds one of Settings' slider rows (828 × 112, full width), cloned from Audio's Main volume with Somnium's option
        /// logic stripped. The caller drives the returned Slider and its Previous/Next arrows (<paramref name="less"/>,
        /// <paramref name="more"/>) and writes the value with <see cref="SetSliderValueText"/>. Null if Settings has moved.
        /// </summary>
        public static Slider AddSettingsSlider(Transform tablet, Transform parent, string name, string label, out Button less, out Button more)
        {
            less = more = null;
            Transform options = tablet == null ? null : tablet.Find(OPTIONS);
            Transform template = options == null ? null : FindDeep(options, SLIDER_TEMPLATE);
            if (template == null || parent == null) return null;

            GameObject clone = CloneStripped(template.gameObject, name, "OptionViewBase", "FloatSlider", "LocalizeStringEvent");
            Slider slider = clone.GetComponentInChildren<Slider>(true);
            if (slider == null) { Object.Destroy(clone); return null; }
            slider.onValueChanged = new Slider.SliderEvent();

            less = clone.transform.Find("Previous")?.GetComponent<Button>();
            more = clone.transform.Find("Next")?.GetComponent<Button>();
            if (less != null) less.onClick = new Button.ButtonClickedEvent();
            if (more != null) more.onClick = new Button.ButtonClickedEvent();
            SetText(clone.transform.Find("Name Label"), label);

            clone.transform.SetParent(parent, false);
            clone.SetActive(true);
            return slider;
        }

        /// <summary>Writes a slider row's value, top right, where Settings shows "100%".</summary>
        public static void SetSliderValueText(Slider slider, string text)
        {
            Transform row = slider == null ? null : slider.transform.parent;
            SetText(row == null ? null : row.Find("Value Label"), text);
        }

        /// <summary>Draws an on/off row the way Somnium's own does: the handle slides, the fill lights.</summary>
        public static void ShowToggle(Toggle toggle, bool on, bool usable)
        {
            if (toggle == null) return;
            Transform handle = toggle.transform.Find("Handle");
            if (handle != null) handle.localPosition = new Vector3(on ? 21f : -21f, handle.localPosition.y, handle.localPosition.z);
            Image fill = toggle.transform.Find("Fill")?.GetComponent<Image>();
            if (fill != null) { Color c = fill.color; c.a = on ? 1f : 0f; fill.color = c; }
            toggle.interactable = usable;
            CanvasGroup group = toggle.transform.parent.GetComponent<CanvasGroup>();
            if (group == null) group = toggle.transform.parent.gameObject.AddComponent<CanvasGroup>();
            group.alpha = usable ? 1f : 0.4f;
        }

        private static void SetText(Transform t, string text)
        {
            if (t != null && t.TryGetComponent(out TMP_Text tmp)) tmp.text = text;
        }

        /// <summary>Clones under an inactive holder and strips the named scripts, so none of them ever wakes. Returned unparented and active.</summary>
        private static GameObject CloneStripped(GameObject template, string name, params string[] strip)
        {
            var holder = new GameObject("TabletBridge_Holder");
            holder.SetActive(false);
            GameObject clone = Object.Instantiate(template, holder.transform, false);
            clone.name = name;
            foreach (MonoBehaviour mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (System.Array.IndexOf(strip, mb.GetType().Name) >= 0) Object.DestroyImmediate(mb);
            clone.transform.SetParent(null, false);
            Object.Destroy(holder);
            return clone;
        }

        /// <summary>The tablet's font, so our text matches and is sure to render in the client.</summary>
        public static TMP_FontAsset Font(Transform tablet)
        {
            TMP_Text text = tablet == null ? null : tablet.Find(TEMPLATE_BUTTON + "/Text")?.GetComponent<TMP_Text>();
            return text == null ? null : text.font;
        }

        /// <summary>The sprite behind every home button, rounded and white, for tinting our own panels.</summary>
        public static Sprite PanelSprite(Transform tablet)
        {
            Image image = tablet == null ? null : tablet.Find(TEMPLATE_BUTTON)?.GetComponent<Image>();
            return image == null ? null : image.sprite;
        }
    }
}
