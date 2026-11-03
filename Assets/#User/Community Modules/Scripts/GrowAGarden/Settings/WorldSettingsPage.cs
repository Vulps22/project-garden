using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vulps.GrowAGarden.DevTools;
using Vulps.GrowAGarden.Ownership;
using Vulps.GrowAGarden.Tutorial;

namespace Vulps.GrowAGarden.Settings
{
    /// <summary>
    /// "World Settings", our section in Somnium's own Settings app: a World panel for every player (stoned effects,
    /// force feeding, resetting the voice tutorial) and, beneath it, a Developer panel for the developer alone
    /// (Somnium's fly mode). Built in code onto the tablet through <see cref="TabletManager"/>, stored by
    /// <see cref="GameSettings"/>. Local only. See docs/tablet.md → World Settings.
    /// </summary>
    public class WorldSettingsPage : MonoBehaviour
    {
        [Tooltip("Icon on the World section button.")]
        [SerializeField] private Texture _icon;

        private Transform _tablet;
        private TMP_FontAsset _font;
        private readonly List<Button> _sections = new List<Button>();
        private readonly List<RectTransform> _pages = new List<RectTransform>();
        private Toggle _stoned;
        private Toggle _forceFeeding;
        private Toggle _notifications;
        private Toggle _nativeFlight;
        private GameObject _developerPanel;   // shown while this player has developer access, which can change mid-session
        private float _nextLookup;
        private bool _built;

        private void OnEnable() => GameSettings.Changed += Refresh;
        private void OnDisable() => GameSettings.Changed -= Refresh;

        private void OnDestroy()
        {
            foreach (RectTransform page in _pages) if (page != null) Destroy(page.gameObject);
            foreach (Button section in _sections) if (section != null) Destroy(section.gameObject);
        }

        private void Update()
        {
            if (_built)
            {
                if (_tablet == null || _pages.Count == 0 || _pages[0] == null) { _built = false; return; }
                if (_developerPanel != null && _developerPanel.activeSelf != Developer.IsLocal) _developerPanel.SetActive(Developer.IsLocal);
                return;
            }
            if (Time.time < _nextLookup) return;
            _nextLookup = Time.time + 2f;

            _tablet = TabletManager.FindTablet(PlayerManager.LocalPlayerRig.Root);
            if (_tablet == null) return;
            _font = TabletManager.Font(_tablet);
            _sections.Clear();
            _pages.Clear();

            RectTransform column = AddSection("World", "World", _icon);
            Transform world = column == null ? null : AddPanel(column, "World", "World Settings");
            if (world == null)
            {
                Logger.Warn($"Update() '{gameObject.name}' — Settings is not where docs/tablet.md says; no World Settings");
                enabled = false;
                return;
            }
            _stoned = TabletManager.AddSettingsToggle(_tablet, world, "Option_AllowStonedEffects", "Allow Stoned Effects");
            _forceFeeding = TabletManager.AddSettingsToggle(_tablet, world, "Option_AllowForceFeeding", "Allow Force Feeding (soon)");
            _notifications = TabletManager.AddSettingsToggle(_tablet, world, "Option_ShowNotifications", "Show Notifications");
            ActionRow(world, "Option_ResetVoiceTutorial", "Voice Tutorial", "Reset", ResetTutorial);
            if (_stoned != null) _stoned.onValueChanged.AddListener(on => GameSettings.GetInstance()?.SetAllowStonedEffects(on));
            if (_notifications != null) _notifications.onValueChanged.AddListener(on => GameSettings.GetInstance()?.SetShowNotifications(on));
            FitRows(world);

            Transform dev = AddPanel(column, "Developer", "Developer");
            _nativeFlight = TabletManager.AddSettingsToggle(_tablet, dev, "Option_NativeFlight", "Enable Native Flight");
            if (_nativeFlight != null) _nativeFlight.onValueChanged.AddListener(SetNativeFlight);
            FitRows(dev);
            _developerPanel = dev.parent.gameObject;
            _developerPanel.SetActive(Developer.IsLocal);

            // Somnium's own sections hide ours; listeners added once, after ours exist so they are skipped.
            foreach (Button theirs in TabletManager.SettingsSections(_tablet))
            {
                if (_sections.Contains(theirs)) continue;
                Button clicked = theirs;
                theirs.onClick.AddListener(() => HideAll(clicked));
            }

            Refresh();
            _built = true;
            Logger.Info($"Update() '{gameObject.name}' — {_sections.Count} section(s) added to the tablet's Settings");
        }

        /// <summary>A section button and its page, a column for panels like Settings' own left column; null if Settings has moved.</summary>
        private RectTransform AddSection(string name, string label, Texture icon)
        {
            Button section = TabletManager.AddSettingsSection(_tablet, name, label, icon);
            RectTransform page = TabletManager.AddSettingsPage(_tablet, $"OptionsTab.{name} (Grow A Garden)");
            if (section == null || page == null) return null;

            int index = _sections.Count;
            _sections.Add(section);
            _pages.Add(page);
            section.onClick.AddListener(() => Show(index));

            var column = new GameObject("Left", typeof(RectTransform), typeof(VerticalLayoutGroup));
            var columnRect = (RectTransform)column.transform;
            columnRect.SetParent(page, false);
            columnRect.anchorMin = columnRect.anchorMax = new Vector2(0, 1);
            columnRect.pivot = new Vector2(0, 1);
            columnRect.anchoredPosition = new Vector2(10, -10);
            columnRect.sizeDelta = new Vector2(872, 1060);
            var columnLayout = column.GetComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 22;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandHeight = false;
            return columnRect;
        }

        /// <summary>A titled panel in a section's column, in Settings' own dark panel; returns where its rows go.</summary>
        private Transform AddPanel(RectTransform column, string name, string title)
        {
            var panel = new GameObject($"Section - {name}", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            var panelRect = (RectTransform)panel.transform;
            panelRect.SetParent(column, false);
            Image bg = panel.GetComponent<Image>();
            bg.sprite = TabletManager.SettingsSectionSprite(_tablet);
            bg.type = Image.Type.Sliced;
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 22, 22);
            layout.spacing = 22;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            // Heights are stated, not fitted: fitters nested in layout groups came out short and the rows overlapped.
            TMP_Text heading = Label("Title", panelRect, title, 31.8f, TextAlignmentOptions.TopLeft);
            heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;

            var rows = new GameObject("Buttons", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            var rowsRect = (RectTransform)rows.transform;
            rowsRect.SetParent(panelRect, false);
            var grid = rows.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(405.8f, ROW_HEIGHT);
            grid.spacing = new Vector2(0, ROW_GAP);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            return rowsRect;
        }

        private const float ROW_HEIGHT = 116.5f;
        private const float ROW_GAP = 11.6f;

        /// <summary>Sizes a panel's rows to what was put in them, two to a line, as Settings' own grids are.</summary>
        private static void FitRows(Transform rows)
        {
            if (rows == null) return;
            int lines = Mathf.Max(1, (rows.childCount + 1) / 2);
            rows.GetComponent<LayoutElement>().preferredHeight = lines * ROW_HEIGHT + (lines - 1) * ROW_GAP;
        }

        /// <summary>A row like Settings' own, a label over a button instead of a switch.</summary>
        private void ActionRow(Transform parent, string name, string label, string buttonLabel, UnityEngine.Events.UnityAction act)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            TMP_Text title = Label("Text (TMP)", row.transform, label, 24.1f, TextAlignmentOptions.Top);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0, 1); titleRect.anchorMax = new Vector2(1, 1); titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.sizeDelta = new Vector2(0, 32); titleRect.anchoredPosition = Vector2.zero;

            var buttonGo = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.SetParent(row.transform, false);
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 1); buttonRect.pivot = new Vector2(0.5f, 1);
            buttonRect.anchoredPosition = new Vector2(0, -44); buttonRect.sizeDelta = new Vector2(180, 52);
            Image image = buttonGo.GetComponent<Image>();
            image.sprite = TabletManager.SettingsSectionSprite(_tablet);
            image.type = Image.Type.Sliced;
            image.color = new Color(0.25f, 0.55f, 0.7f, 1f);
            buttonGo.GetComponent<Button>().onClick.AddListener(act);
            TMP_Text text = Label("Label", buttonRect, buttonLabel, 22f, TextAlignmentOptions.Center);
            text.raycastTarget = false;
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        }

        private TMP_Text Label(string name, Transform parent, string text, float size, TextAlignmentOptions align)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (_font != null) t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = align;
            return t;
        }

        // ── Behaviour ─────────────────────────────────────────────────────────────

        private void ResetTutorial()
        {
            TutorialManager tutorial = TutorialManager.GetInstance();
            if (tutorial == null) { Logger.Warn($"ResetTutorial() '{gameObject.name}' — no TutorialManager"); return; }
            tutorial.ResetProgress();
        }

        private void SetNativeFlight(bool on)
        {
            GameSettings.GetInstance()?.SetDevNativeFlight(on);
            if (!PlayerManager.SetNativeFlyMode(on)) Logger.Warn($"SetNativeFlight() '{gameObject.name}' — no Motion feature to change");
        }

        /// <summary>Puts the switches where the stored settings say. Force feeding stays off and greyed until it exists.</summary>
        private void Refresh()
        {
            GameSettings settings = GameSettings.GetInstance();
            Show(_stoned, settings == null || settings.AllowStonedEffects, settings != null);
            Show(_forceFeeding, false, false);
            Show(_notifications, settings == null || settings.ShowNotifications, settings != null);
            Show(_nativeFlight, settings != null && settings.DevNativeFlight, settings != null);
        }

        private static void Show(Toggle toggle, bool on, bool usable)
        {
            if (toggle == null) return;
            toggle.SetIsOnWithoutNotify(on);
            TabletManager.ShowToggle(toggle, on, usable);
        }

        /// <summary>Our section, shown as Somnium shows its own: every other section panel and highlight off.</summary>
        private void Show(int index)
        {
            if (_pages[index] == null) return;
            TabletManager.ShowSettingsSection(_tablet, _pages[index], _sections[index]);
            Refresh();
        }

        /// <summary>
        /// One of Somnium's sections was picked while ours was showing. Somnium's own switch has already run; it
        /// thinks its last section is still the open one, so if it did not bring the picked panel back, we do.
        /// </summary>
        private void HideAll(Button picked)
        {
            bool ours = false;
            foreach (RectTransform page in _pages) if (page != null && page.gameObject.activeSelf) ours = true;
            if (!ours) return;
            foreach (RectTransform page in _pages) if (page != null) page.gameObject.SetActive(false);
            foreach (Button section in _sections) TabletManager.SetSelectorHighlight(section, false);
            if (!TabletManager.SettingsSectionShowing(_tablet, picked)) TabletManager.ShowSettingsSection(_tablet, null, picked);
        }
    }
}
