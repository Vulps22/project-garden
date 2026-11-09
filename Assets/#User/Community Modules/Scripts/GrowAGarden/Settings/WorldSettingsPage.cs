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
    /// Our sections in Somnium's own Settings app: World, for every player (stoned effects, force feeding, notifications,
    /// skipping or repeating the tutorial, draw distance), and beside it a Developer section, shown to anyone with developer
    /// access (Somnium's fly mode; a notification to everyone, typed on its own keyboard). Built in code onto the tablet through <see cref="TabletManager"/>, stored by
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
        private TMP_Text _notifyField;
        private TMP_Text _tutorialButton;
        private string _notifyText = string.Empty;
        private bool _caps = true;
        private GameObject _developerSection;   // the right-hand column, shown while this player has developer access
        private Slider _drawDistance;
        private float _nextLookup;
        private float _nextTabletCheck;
        private bool _built;

        private void OnEnable()
        {
            GameSettings.Changed += Refresh;
            TutorialManager.Started += ShowTutorialButton;
            TutorialManager.Ended += ShowTutorialButton;
        }

        private void OnDisable()
        {
            GameSettings.Changed -= Refresh;
            TutorialManager.Started -= ShowTutorialButton;
            TutorialManager.Ended -= ShowTutorialButton;
        }

        private void OnDestroy()
        {
            foreach (RectTransform page in _pages) if (page != null) Destroy(page.gameObject);
            foreach (Button section in _sections) if (section != null) Destroy(section.gameObject);
        }

        private void Update()
        {
            if (_built)
            {
                if (_tablet == null || _pages.Count == 0 || _pages[0] == null || TabletReplaced()) { _built = false; return; }
                // Developer access comes and goes mid-session: so does the Developer section.
                bool allowed = Developer.IsLocal;
                if (_developerSection != null && _developerSection.activeSelf != allowed) _developerSection.SetActive(allowed);
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
                Logger.Warn($"Update() '{gameObject.name}' — Settings is not where docs/tablet.md says (or not built yet); trying again");
                _nextLookup = Time.time + 5f;
                return;
            }
            _stoned = TabletManager.AddSettingsToggle(_tablet, world, "Option_AllowStonedEffects", "Allow Stoned Effects");
            _forceFeeding = TabletManager.AddSettingsToggle(_tablet, world, "Option_AllowForceFeeding", "Allow Force Feeding (soon)");
            _notifications = TabletManager.AddSettingsToggle(_tablet, world, "Option_ShowNotifications", "Show Notifications");
            if (TutorialManager.Available)
            {
                _tutorialButton = ActionRow(world, "Option_Tutorial", "Tutorial", "Repeat", SkipOrRepeatTutorial);
                ShowTutorialButton();
            }
            if (_stoned != null) _stoned.onValueChanged.AddListener(on => GameSettings.GetInstance()?.SetAllowStonedEffects(on));
            if (_notifications != null) _notifications.onValueChanged.AddListener(on => GameSettings.GetInstance()?.SetShowNotifications(on));
            FitRows(world);
            DrawDistanceRow(world.parent);

            // The Developer section sits in the page's right-hand column, as Somnium's own pages have one.
            RectTransform devColumn = AddColumn((RectTransform)column.parent, "Right", 890f);
            Transform dev = AddPanel(devColumn, "Developer", "Developer");
            _nativeFlight = TabletManager.AddSettingsToggle(_tablet, dev, "Option_NativeFlight", "Enable Native Flight");
            if (_nativeFlight != null) _nativeFlight.onValueChanged.AddListener(SetNativeFlight);
            FitRows(dev);
            NotifyBlock(dev.parent);
            _developerSection = devColumn.gameObject;
            _developerSection.SetActive(Developer.IsLocal);

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

            return AddColumn(page, "Left", 10f);
        }

        /// <summary>A column of panels like Settings' own Left and Right, 872 × 1060.</summary>
        private static RectTransform AddColumn(RectTransform page, string name, float x)
        {
            var column = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            var columnRect = (RectTransform)column.transform;
            columnRect.SetParent(page, false);
            columnRect.anchorMin = columnRect.anchorMax = new Vector2(0, 1);
            columnRect.pivot = new Vector2(0, 1);
            columnRect.anchoredPosition = new Vector2(x, -10);
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
        private TMP_Text ActionRow(Transform parent, string name, string label, string buttonLabel, UnityEngine.Events.UnityAction act)
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
            return text;
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

        /// <summary>Draw distance: Somnium's own slider row, 300 m to 2 km in 100 m steps, arrows a step each.</summary>
        private void DrawDistanceRow(Transform panel)
        {
            var holder = new GameObject("Draw Distance", typeof(RectTransform), typeof(LayoutElement));
            holder.transform.SetParent(panel, false);
            holder.GetComponent<LayoutElement>().preferredHeight = 112f;
            _drawDistance = TabletManager.AddSettingsSlider(_tablet, holder.transform, "Option_DrawDistance", "Draw Distance", out Button less, out Button more);
            if (_drawDistance == null) { Destroy(holder); Logger.Warn($"DrawDistanceRow() '{gameObject.name}' — no slider to clone; no draw distance setting"); return; }
            var row = (RectTransform)_drawDistance.transform.parent;
            row.anchorMin = row.anchorMax = new Vector2(0, 1);
            row.pivot = new Vector2(0, 1);
            row.anchoredPosition = Vector2.zero;

            _drawDistance.minValue = GameSettings.MIN_DRAW_DISTANCE / 100f;
            _drawDistance.maxValue = GameSettings.MAX_DRAW_DISTANCE / 100f;
            _drawDistance.wholeNumbers = true;
            _drawDistance.onValueChanged.AddListener(v => GameSettings.GetInstance()?.SetDrawDistance(v * 100f));
            if (less != null) less.onClick.AddListener(() => StepDrawDistance(-100f));
            if (more != null) more.onClick.AddListener(() => StepDrawDistance(100f));
        }

        private static void StepDrawDistance(float metres)
        {
            GameSettings settings = GameSettings.GetInstance();
            if (settings != null) settings.SetDrawDistance(settings.DrawDistance + metres);
        }

        private static readonly string[] NOTIFY_ROWS = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
        private static readonly Color KEY = new Color(0.2f, 0.24f, 0.31f, 1f);
        private static readonly Color SEND = new Color(0.25f, 0.55f, 0.7f, 1f);
        private const float KEY_W = 74f, KEY_H = 52f, KEY_STEP = 80f, ROW_STEP = 60f;

        /// <summary>Notify Everyone: a text field, a keyboard of our own (Settings has none) and Send.</summary>
        private void NotifyBlock(Transform panel)
        {
            var block = new GameObject("Notify Everyone", typeof(RectTransform), typeof(LayoutElement));
            var blockRect = (RectTransform)block.transform;
            blockRect.SetParent(panel, false);
            block.GetComponent<LayoutElement>().preferredHeight = 120f + 5 * ROW_STEP;

            TMP_Text title = Label("Text (TMP)", blockRect, "Notify Everyone (you too)", 24.1f, TextAlignmentOptions.TopLeft);
            Pin(title.transform, 0, 0, 828, 32);

            var field = new GameObject("Field", typeof(RectTransform), typeof(Image));
            field.transform.SetParent(blockRect, false);
            Pin(field.transform, 0, -40, 828, 64);
            Image fieldImage = field.GetComponent<Image>();
            fieldImage.sprite = TabletManager.SettingsSectionSprite(_tablet);
            fieldImage.type = Image.Type.Sliced;
            fieldImage.color = new Color(0.14f, 0.17f, 0.23f, 1f);
            _notifyField = Label("Text", field.transform, string.Empty, 24f, TextAlignmentOptions.MidlineLeft);
            _notifyField.textWrappingMode = TextWrappingModes.Normal;
            _notifyField.overflowMode = TextOverflowModes.Ellipsis;
            var textRect = (RectTransform)_notifyField.transform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16, 4); textRect.offsetMax = new Vector2(-16, -4);

            float top = -120f;
            for (int r = 0; r < NOTIFY_ROWS.Length; r++)
            {
                string row = NOTIFY_ROWS[r];
                float x = (828f - (row.Length + (r == 3 ? 3 : 0)) * KEY_STEP) / 2f;
                for (int k = 0; k < row.Length; k++)
                {
                    string ch = row[k].ToString();
                    Key(blockRect, ch, ch, x + k * KEY_STEP, top - r * ROW_STEP, KEY_W, KEY, () => TypeNotify(ch));
                }
                if (r == 3) Key(blockRect, "Del", "Del", x + row.Length * KEY_STEP, top - r * ROW_STEP, KEY_STEP * 3 - 6, KEY, () => TypeNotify(null));
            }

            float y = top - 4 * ROW_STEP, at = 0f;
            Key(blockRect, "Caps", "Aa", at, y, KEY_W, KEY, () => { _caps = !_caps; ShowNotifyText(); }); at += KEY_STEP;
            foreach (string p in new[] { ",", ".", "!", "?", "'" })
            {
                string ch = p;
                Key(blockRect, ch, ch, at, y, 54f, KEY, () => TypeNotify(ch));
                at += 60f;
            }
            Key(blockRect, "Space", "Space", at, y, 230f, KEY, () => TypeNotify(" ")); at += 236f;
            Key(blockRect, "Clear", "Clear", at, y, 100f, KEY, () => { _notifyText = string.Empty; ShowNotifyText(); }); at += 106f;
            Key(blockRect, "Send", "Send", at, y, 828f - at, SEND, SendNotify);
            ShowNotifyText();
        }

        private void Key(Transform parent, string name, string label, float x, float y, float width, Color colour, UnityEngine.Events.UnityAction act)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Pin(go.transform, x, y, width, KEY_H);
            Image image = go.GetComponent<Image>();
            image.sprite = TabletManager.SettingsSectionSprite(_tablet);
            image.type = Image.Type.Sliced;
            image.color = colour;
            go.GetComponent<Button>().onClick.AddListener(act);
            TMP_Text text = Label("Label", go.transform, label, 22f, TextAlignmentOptions.Center);
            text.raycastTarget = false;
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Pin(Transform t, float x, float y, float width, float height)
        {
            var rect = (RectTransform)t;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void TypeNotify(string ch)
        {
            if (ch == null) { if (_notifyText.Length > 0) _notifyText = _notifyText.Substring(0, _notifyText.Length - 1); }
            else if (_notifyText.Length < DeveloperBroadcast.MAX_LENGTH)
            {
                bool upper = _caps || _notifyText.Length == 0 || _notifyText.EndsWith(". ") || _notifyText.EndsWith("! ") || _notifyText.EndsWith("? ");
                _notifyText += upper ? ch.ToUpperInvariant() : ch.ToLowerInvariant();
            }
            ShowNotifyText();
        }

        private void ShowNotifyText(string placeholder = "Type a notification…")
        {
            if (_notifyField == null) return;
            string caret = _caps ? " <color=#8899AA>[Aa]</color>" : string.Empty;
            _notifyField.text = _notifyText.Length == 0 ? $"<color=#8899AA>{placeholder}</color>{caret}" : _notifyText + caret;
        }

        private void SendNotify()
        {
            if (!DeveloperBroadcast.Send(_notifyText)) { ShowNotifyText("Nothing sent: no text, or not connected"); return; }
            _notifyText = string.Empty;
            ShowNotifyText("Sent.");
        }

        // ── Behaviour ─────────────────────────────────────────────────────────────

        /// <summary>Skip while the tutorial runs; Repeat once it is over.</summary>
        private void SkipOrRepeatTutorial()
        {
            TutorialManager tutorial = TutorialManager.GetInstance();
            if (tutorial == null) { Logger.Warn($"SkipOrRepeatTutorial() '{gameObject.name}' — no TutorialManager"); return; }
            if (tutorial.IsRunning) tutorial.Skip();
            else tutorial.Repeat();
        }

        private void ShowTutorialButton()
        {
            if (_tutorialButton == null) return;
            TutorialManager tutorial = TutorialManager.GetInstance();
            _tutorialButton.text = tutorial != null && tutorial.IsRunning ? "Skip" : "Repeat";
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
            if (_drawDistance != null)
            {
                float metres = settings != null ? settings.DrawDistance : GameSettings.DEFAULT_DRAW_DISTANCE;
                _drawDistance.SetValueWithoutNotify(metres / 100f);
                TabletManager.SetSliderValueText(_drawDistance, metres >= 1000f ? $"{metres / 1000f:0.0} km" : $"{metres:F0} m");
            }
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

        /// <summary>Somnium made a new tablet (an instance change can): the old one's build is no longer what the player holds.</summary>
        private bool TabletReplaced()
        {
            if (Time.time < _nextTabletCheck) return false;
            _nextTabletCheck = Time.time + 2f;
            Transform now = TabletManager.FindTablet(PlayerManager.LocalPlayerRig.Root);
            if (now == null || now == _tablet) return false;
            Logger.Info($"TabletReplaced() '{gameObject.name}' — a new tablet; building again");
            return true;
        }
    }
}
