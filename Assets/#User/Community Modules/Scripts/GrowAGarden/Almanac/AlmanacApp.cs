using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Almanac
{
    /// <summary>
    /// The Almanac, an app on every player's tablet: a numbered grid of every crop, and a page for each with what it is,
    /// what it does, how rare it is, how many stalls stock it now and the player's own record. Every page can be read
    /// from the start; a crop's picture stays a silhouette until the player has bought its seed from a stall. Crops not
    /// yet in the stalls say "coming soon". Built in code onto Somnium's tablet through <see cref="TabletManager"/>, like
    /// the Developer Market. See docs/almanac.md.
    /// </summary>
    public class AlmanacApp : MonoBehaviour
    {
        private const string BUTTON_NAME = "Tablet.XR.AppBtn - Almanac";
        private const string PAGE_NAME = "Tablet.XR.AlmanacTab";

        [Tooltip("Home-screen icon for the app. A texture, made a sprite at runtime: sprite references arrive empty in-world.")]
        [SerializeField] private Texture2D _icon;

        [Tooltip("Seconds between refreshes of an open page's stall count.")]
        [SerializeField] private float _refreshSeconds = 2f;

        private static readonly Color PANEL = new Color(0.1f, 0.09f, 0.07f, 0.97f);
        private static readonly Color CELL = new Color(0.17f, 0.15f, 0.11f, 1f);
        private static readonly Color KEY = new Color(0.27f, 0.23f, 0.17f, 1f);
        private static readonly Color ACCENT = new Color(0.95f, 0.82f, 0.45f, 1f);
        private static readonly Color SOFT = new Color(0.78f, 0.74f, 0.66f, 1f);
        private static readonly Color SOON = new Color(0.55f, 0.75f, 0.95f, 1f);

        private Transform _tablet;
        private RectTransform _page;
        private RectTransform _grid;
        private RectTransform _detail;
        private TMP_FontAsset _font;
        private Sprite _sprite;
        private readonly List<(AlmanacBook.Entry entry, RawImage picture, TMP_Text name)> _cells = new List<(AlmanacBook.Entry, RawImage, TMP_Text)>();
        private AlmanacBook.Entry _showing;
        private RawImage _detailPicture;
        private TMP_Text _detailTitle, _detailKind, _detailText, _detailRecord;
        private float _nextLookup;
        private float _nextRefresh;
        private bool _built;
        private bool _dirty;

        private void Awake() => AlmanacBook.Changed += OnChanged;

        private void OnDestroy()
        {
            AlmanacBook.Changed -= OnChanged;
            if (_page != null) Destroy(_page.gameObject);
        }

        private void OnChanged() => _dirty = true;

        private void Update()
        {
            if (!_built) { TryBuild(); return; }
            if (_tablet == null || _page == null) { _built = false; return; }
            if (!_page.gameObject.activeSelf) return;

            // Somnium opened something of its own (the frame's buttons, a notification): step aside.
            if (TabletManager.SomniumPageOpen(_tablet, _page)) { _page.gameObject.SetActive(false); return; }
            if (_dirty || Time.time >= _nextRefresh) Refresh();
        }

        // ── Building ──────────────────────────────────────────────────────────────

        private void TryBuild()
        {
            if (Time.time < _nextLookup) return;
            _nextLookup = Time.time + 2f;
            if (PlayerManager.LocalPlayerRig.Root == null) return;

            _tablet = TabletManager.FindTablet(PlayerManager.LocalPlayerRig.Root);
            if (_tablet == null) return;

            Sprite icon = _icon == null ? null : Sprite.Create(_icon, new Rect(0, 0, _icon.width, _icon.height), new Vector2(0.5f, 0.5f));
            Button open = TabletManager.AddHomeButton(_tablet, BUTTON_NAME, icon, "Almanac");
            _page = TabletManager.AddPage(_tablet, PAGE_NAME);
            if (open == null || _page == null)
            {
                Logger.Warn($"TryBuild() '{gameObject.name}' — the tablet's home screen is not where docs/tablet.md says; no Almanac");
                enabled = false;
                return;
            }

            _font = TabletManager.Font(_tablet);
            _sprite = TabletManager.PanelSprite(_tablet);
            open.onClick.AddListener(Open);
            BuildGrid();
            BuildDetail();
            _built = true;
            Logger.Info($"TryBuild() '{gameObject.name}' — Almanac on the tablet, {AlmanacBook.Entries.Count} crop(s)");
        }

        private void BuildGrid()
        {
            Image bg = Panel("Background", _page, PANEL);
            Stretch(bg.rectTransform, 0, 0, 0, 0);
            _grid = bg.rectTransform;

            Button back = MakeButton("Back", _grid, "‹ Back", 28, KEY);
            Place(back.transform, 20, -14, 130, 60);
            back.onClick.AddListener(Close);

            TMP_Text title = Text("Title", _grid, "Almanac", 44, ACCENT, TextAlignmentOptions.Center);
            Place(title.transform, 160, -14, 640, 60);
            TMP_Text hint = Text("Hint", _grid, "Every crop there is. A shadow is one you have not bought yet.", 22, SOFT, TextAlignmentOptions.Center);
            Place(hint.transform, 20, -80, 920, 36);

            var scrollGo = new GameObject("Crops", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.SetParent(_grid, false);
            Stretch(scrollRect, 20, 126, 20, 20);
            scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);   // something for the ray to drag

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewRect = (RectTransform)viewport.transform;
            viewRect.SetParent(scrollRect, false);
            Stretch(viewRect, 0, 0, 0, 0);

            var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            var contentRect = (RectTransform)content.transform;
            contentRect.SetParent(viewRect, false);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = Vector2.zero;
            var grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(205, 260);
            grid.spacing = new Vector2(12, 12);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            foreach (AlmanacBook.Entry entry in AlmanacBook.Entries) _cells.Add(MakeCell(entry, contentRect));
        }

        private (AlmanacBook.Entry, RawImage, TMP_Text) MakeCell(AlmanacBook.Entry entry, Transform parent)
        {
            Button cell = MakeButton(entry.Crop.displayName, parent, string.Empty, 20, CELL);

            TMP_Text number = Text("Number", cell.transform, $"No. {entry.Number:00}", 20, SOFT, TextAlignmentOptions.TopLeft);
            number.raycastTarget = false;
            Place(number.transform, 10, -6, 120, 26);

            var pic = new GameObject("Picture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            pic.transform.SetParent(cell.transform, false);
            pic.texture = entry.Crop.picture;
            pic.raycastTarget = false;
            Place(pic.transform, 27, -30, 150, 150);

            TMP_Text name = Text("Name", cell.transform, entry.Crop.displayName, 24, Color.white, TextAlignmentOptions.Center);
            name.raycastTarget = false;
            Place(name.transform, 5, -184, 195, 34);

            if (!entry.InPool)
            {
                TMP_Text soon = Text("Soon", cell.transform, "coming soon", 20, SOON, TextAlignmentOptions.Center);
                soon.raycastTarget = false;
                Place(soon.transform, 5, -218, 195, 30);
            }

            AlmanacBook.Entry shown = entry;
            cell.onClick.AddListener(() => Show(shown));
            return (entry, pic, name);
        }

        private void BuildDetail()
        {
            Image bg = Panel("Page", _page, PANEL);
            _detail = bg.rectTransform;
            Stretch(_detail, 0, 0, 0, 0);

            Button back = MakeButton("Back", _detail, "‹ Almanac", 28, KEY);
            Place(back.transform, 20, -14, 180, 60);
            back.onClick.AddListener(() => Show(null));

            _detailPicture = new GameObject("Picture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _detailPicture.transform.SetParent(_detail, false);
            _detailPicture.raycastTarget = false;
            Place(_detailPicture.transform, 30, -100, 300, 300);

            _detailTitle = Text("Title", _detail, string.Empty, 42, ACCENT, TextAlignmentOptions.TopLeft);
            Place(_detailTitle.transform, 360, -96, 580, 56);
            _detailKind = Text("Kind", _detail, string.Empty, 26, SOFT, TextAlignmentOptions.TopLeft);
            Place(_detailKind.transform, 360, -152, 580, 36);

            _detailText = Text("About", _detail, string.Empty, 26, Color.white, TextAlignmentOptions.TopLeft);
            _detailText.enableWordWrapping = true;
            _detailText.overflowMode = TextOverflowModes.Overflow;
            Place(_detailText.transform, 360, -196, 580, 420);

            _detailRecord = Text("Record", _detail, string.Empty, 26, Color.white, TextAlignmentOptions.TopLeft);
            _detailRecord.enableWordWrapping = true;
            _detailRecord.overflowMode = TextOverflowModes.Overflow;
            Place(_detailRecord.transform, 30, -630, 900, 300);

            _detail.gameObject.SetActive(false);
        }

        // ── Using it ──────────────────────────────────────────────────────────────

        private void Open()
        {
            if (_page == null) return;
            TabletManager.ShowHome(_tablet, false);
            _page.gameObject.SetActive(true);
            Show(null);
        }

        private void Close()
        {
            if (_page == null) return;
            _page.gameObject.SetActive(false);
            TabletManager.ShowHome(_tablet, true);
        }

        /// <summary>A crop's page, or the grid with null.</summary>
        private void Show(AlmanacBook.Entry entry)
        {
            _showing = entry;
            _grid.gameObject.SetActive(entry == null);
            _detail.gameObject.SetActive(entry != null);
            Refresh();
        }

        private void Refresh()
        {
            _dirty = false;
            _nextRefresh = Time.time + _refreshSeconds;
            foreach ((AlmanacBook.Entry entry, RawImage picture, TMP_Text _) in _cells)
                picture.color = AlmanacBook.Collected(entry.Crop.seedId) ? Color.white : Color.black;
            if (_showing != null) FillDetail(_showing);
        }

        private void FillDetail(AlmanacBook.Entry entry)
        {
            SeedDefinition crop = entry.Crop;
            bool collected = AlmanacBook.Collected(crop.seedId);
            _detailPicture.texture = crop.picture;
            _detailPicture.color = collected ? Color.white : Color.black;
            _detailTitle.text = $"No. {entry.Number:00}  {crop.displayName}";
            _detailKind.text = entry.Kind;

            var about = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(crop.tagline)) about.Append($"<i>\"{crop.tagline}\"</i>\n\n");
            if (!string.IsNullOrEmpty(crop.description)) about.Append(crop.description).Append("\n\n");
            if (entry.InPool) about.Append($"<color=#F2D173>{AlmanacBook.Tier(entry.Share)}</color> · {entry.Share * 100f:0.#}% of stall rolls\n");
            else about.Append("<color=#8CBFF2>Coming soon</color>: not in the stalls yet\n");
            about.Append($"Seed {crop.buyPrice} Thatch");
            about.Append(crop.sellValue > 0 ? $" · sells for {crop.sellValue} each\n" : " · nothing to sell\n");
            about.Append($"Grows in {Seconds(crop.growthDuration)}");
            if (crop.ripenDuration > 1f) about.Append($" · ripens in {Seconds(crop.ripenDuration)}");
            about.Append('\n');
            if (entry.InPool)
            {
                int stalls = ShopKeeper.Stocked(crop.seedId);
                about.Append(stalls == 0 ? "In no stall right now" : stalls == 1 ? "In 1 stall right now" : $"In {stalls} stalls right now");
            }
            _detailText.text = about.ToString();

            (int planted, int harvested, int sold, int best) = AlmanacBook.Record(crop.seedId);
            _detailRecord.text = !collected && planted == 0
                ? "<color=#F2D173>Your record</color>\nNot collected yet: buy one from a stall."
                : $"<color=#F2D173>Your record</color>\nPlanted {planted} · Harvested {harvested} · Sold {sold}" + (best > 0 ? $" · Best sale {best} Thatch" : string.Empty);
        }

        private static string Seconds(float s) => s >= 60f ? $"{s / 60f:0.#} min" : $"{s:0} s";

        // ── UI helpers, as the Developer Market's ─────────────────────────────────

        private Image Panel(string name, Transform parent, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = _sprite;
            image.type = Image.Type.Sliced;
            image.color = colour;
            return image;
        }

        private TMP_Text Text(string name, Transform parent, string text, float size, Color colour, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.color = colour;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        private Button MakeButton(string name, Transform parent, string label, float size, Color colour)
        {
            Image bg = Panel(name, parent, colour);
            var button = bg.gameObject.AddComponent<Button>();
            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
            colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colours;
            if (string.IsNullOrEmpty(label)) return button;
            TMP_Text text = Text("Label", bg.transform, label, size, Color.white, TextAlignmentOptions.Center);
            text.raycastTarget = false;
            Stretch(text.rectTransform, 0, 0, 0, 0);
            return button;
        }

        /// <summary>Top-left anchored at (x, y) from the parent's top-left, w by h.</summary>
        private static void Place(Transform t, float x, float y, float w, float h)
        {
            var r = (RectTransform)t;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        /// <summary>Fills the parent, inset by left, top, right, bottom.</summary>
        private static void Stretch(RectTransform r, float left, float top, float right, float bottom)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(left, bottom);
            r.offsetMax = new Vector2(-right, -top);
        }
    }
}
