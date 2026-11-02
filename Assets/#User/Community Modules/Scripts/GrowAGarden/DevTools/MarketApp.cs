using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.DevTools
{
    /// <summary>
    /// The Developer Market, an app on the developer's tablet and nobody else's: a searchable grid of every seed,
    /// fruit and the splif. Point a hand at one and pull the trigger to bring it out of the tablet into that hand, free.
    /// Built entirely in code onto Somnium's tablet through <see cref="TabletManager"/>. See docs/dev-tools.md.
    /// </summary>
    public class MarketApp : MonoBehaviour
    {
        private const string BUTTON_NAME = "Tablet.XR.AppBtn - Developer Market";
        private const string PAGE_NAME = "Tablet.XR.DeveloperMarketTab";
        private static readonly string[] KEY_ROWS = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };

        [Tooltip("Home-screen icon for the app. A texture, made a sprite at runtime: sprite references arrive empty in-world.")]
        [SerializeField] private Texture2D _icon;

        [Tooltip("How far out of the tablet, towards the head, an item appears, in metres.")]
        [SerializeField] private float _pullOut = 0.12f;

        private static readonly Color PANEL = new Color(0.07f, 0.09f, 0.13f, 0.97f);
        private static readonly Color FIELD = new Color(0.14f, 0.17f, 0.23f, 1f);
        private static readonly Color KEY = new Color(0.2f, 0.24f, 0.31f, 1f);
        private static readonly Color CELL = new Color(0.12f, 0.15f, 0.21f, 1f);
        private static readonly Color CELL_LIT = new Color(0.2f, 0.42f, 0.55f, 1f);
        private static readonly Color ACCENT = new Color(0.4f, 1f, 0.8f, 1f);

        private Transform _tablet;
        private RectTransform _page;
        private TMP_FontAsset _font;
        private Sprite _sprite;
        private TMP_Text _query;
        private readonly List<MarketCell> _cells = new List<MarketCell>();
        private string _search = string.Empty;
        private float _nextLookup;
        private bool _built;

        private readonly MarketCell[] _hover = new MarketCell[2];   // 0 left, 1 right
        private readonly bool[] _triggerWas = new bool[2];
        private readonly XRBaseInteractor[] _holding = new XRBaseInteractor[2];   // a hand holding a delivered item by script
        private readonly List<InputDevice> _devices = new List<InputDevice>();

        private void Awake() => DeveloperMarket.Delivered += OnDelivered;

        private void OnDestroy()
        {
            DeveloperMarket.Delivered -= OnDelivered;
            if (_page != null) Destroy(_page.gameObject);
        }

        private void Update()
        {
            LetGo(false);
            LetGo(true);
            if (!_built) { TryBuild(); return; }
            if (_tablet == null || _page == null) { _built = false; return; }
            if (!_page.gameObject.activeSelf) return;

            // Somnium opened something of its own (the frame's buttons, a notification): step aside.
            if (TabletManager.SomniumPageOpen(_tablet, _page)) { _page.gameObject.SetActive(false); return; }

            Pull(false);
            Pull(true);
        }

        // ── Building ──────────────────────────────────────────────────────────────

        private void TryBuild()
        {
            if (Time.time < _nextLookup) return;
            _nextLookup = Time.time + 2f;
            if (!Developer.IsLocal) return;

            _tablet = TabletManager.FindTablet(PlayerManager.LocalPlayerRig.Root);
            if (_tablet == null) return;

            Sprite icon = _icon == null ? null : Sprite.Create(_icon, new Rect(0, 0, _icon.width, _icon.height), new Vector2(0.5f, 0.5f));
            Button open = TabletManager.AddHomeButton(_tablet, BUTTON_NAME, icon, "Dev Market");
            _page = TabletManager.AddPage(_tablet, PAGE_NAME);
            if (open == null || _page == null)
            {
                Logger.Warn($"TryBuild() '{gameObject.name}' — the tablet's home screen is not where docs/tablet.md says; no Developer Market");
                enabled = false;
                return;
            }

            _font = TabletManager.Font(_tablet);
            _sprite = TabletManager.PanelSprite(_tablet);
            open.onClick.AddListener(Open);
            BuildPage();
            _built = true;
            Logger.Info($"TryBuild() '{gameObject.name}' — Developer Market on the tablet, {DeveloperMarket.Count} item(s)");
        }

        private void BuildPage()
        {
            Image bg = Panel("Background", _page, PANEL);
            Stretch(bg.rectTransform, 0, 0, 0, 0);

            Button back = MakeButton("Back", _page, "‹ Back", 28, KEY);
            Place(back.transform, 20, -14, 130, 60);
            back.onClick.AddListener(Close);

            TMP_Text title = Text("Title", _page, "Developer Market", 40, ACCENT, TextAlignmentOptions.Center);
            Place(title.transform, 160, -14, 640, 60);

            Image field = Panel("Search", _page, FIELD);
            Place(field.transform, 30, -92, 900, 64);
            _query = Text("Query", field.transform, string.Empty, 30, Color.white, TextAlignmentOptions.MidlineLeft);
            Stretch(_query.rectTransform, 20, 0, 20, 0);

            for (int r = 0; r < KEY_ROWS.Length; r++)
            {
                string row = KEY_ROWS[r];
                int extra = r == 2 ? 2 : 0;
                float width = (row.Length + extra) * 86f + (r == 2 ? 80f : 0f);
                float x = (960f - width) / 2f;
                for (int k = 0; k < row.Length; k++)
                {
                    string letter = row[k].ToString();
                    Button key = MakeButton(letter, _page, letter, 30, KEY);
                    Place(key.transform, x + k * 86f, -170 - r * 68, 80, 60);
                    key.onClick.AddListener(() => Type(letter));
                }
                if (r == 2)
                {
                    Button del = MakeButton("Delete", _page, "Del", 26, KEY);
                    Place(del.transform, x + row.Length * 86f, -170 - r * 68, 120, 60);
                    del.onClick.AddListener(() => Type(null));
                    Button clear = MakeButton("Clear", _page, "Clear", 26, KEY);
                    Place(clear.transform, x + row.Length * 86f + 126f, -170 - r * 68, 120, 60);
                    clear.onClick.AddListener(() => { _search = string.Empty; Filter(); });
                }
            }

            // The grid: a scrolling viewport of item cells.
            var scrollGo = new GameObject("Items", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.SetParent(_page, false);
            Stretch(scrollRect, 20, 380, 20, 20);
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
            grid.cellSize = new Vector2(205, 235);
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

            for (int i = 0; i < DeveloperMarket.Count; i++) _cells.Add(MakeCell(i, contentRect));
            Filter();
        }

        private MarketCell MakeCell(int index, Transform parent)
        {
            Image bg = Panel(DeveloperMarket.NameOf(index), parent, CELL);
            var cell = bg.gameObject.AddComponent<MarketCell>();
            cell.Index = index;
            cell.App = this;
            cell.Background = bg;
            cell.Normal = CELL;
            cell.Lit = CELL_LIT;

            var pic = new GameObject("Preview", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            pic.transform.SetParent(bg.transform, false);
            pic.texture = DeveloperMarket.PreviewOf(index);
            pic.raycastTarget = false;
            Place(pic.transform, 17, -10, 170, 170);

            TMP_Text label = Text("Name", bg.transform, DeveloperMarket.NameOf(index), 24, Color.white, TextAlignmentOptions.Center);
            label.raycastTarget = false;
            Place(label.transform, 5, -185, 195, 44);
            return cell;
        }

        // ── Using it ──────────────────────────────────────────────────────────────

        private void Open()
        {
            if (_page == null) return;
            TabletManager.ShowHome(_tablet, false);
            _page.gameObject.SetActive(true);
        }

        private void Close()
        {
            if (_page == null) return;
            _page.gameObject.SetActive(false);
            TabletManager.ShowHome(_tablet, true);
        }

        private void Type(string letter)
        {
            if (letter == null) { if (_search.Length > 0) _search = _search.Substring(0, _search.Length - 1); }
            else _search += letter;
            Filter();
        }

        private void Filter()
        {
            if (_query != null) _query.text = _search.Length == 0 ? "<color=#8899AA>Search…</color>" : _search;
            foreach (MarketCell cell in _cells)
            {
                bool show = _search.Length == 0 || DeveloperMarket.NameOf(cell.Index).ToUpperInvariant().Contains(_search);
                if (cell.gameObject.activeSelf != show) cell.gameObject.SetActive(show);
            }
        }

        public void Hover(bool right, MarketCell cell) => _hover[right ? 1 : 0] = cell;

        public void Unhover(bool right, MarketCell cell)
        {
            if (_hover[right ? 1 : 0] == cell) _hover[right ? 1 : 0] = null;
        }

        /// <summary>Trigger pulled while that hand's ray is on an item: ask for it, just out of the tablet towards the head. Not grip, which grabs the tablet.</summary>
        private void Pull(bool right)
        {
            int h = right ? 1 : 0;
            bool down = TriggerDown(right);
            bool pressed = down && !_triggerWas[h];
            _triggerWas[h] = down;

            MarketCell cell = _hover[h];
            if (!pressed || cell == null || !cell.isActiveAndEnabled) return;

            Vector3 from = cell.transform.position;
            Transform head = PlayerManager.LocalPlayerRig.Head;
            Vector3 toward = head != null ? (head.position - from).normalized : -cell.transform.forward;
            DeveloperMarket.Request(cell.Index, from + toward * _pullOut, Quaternion.identity, right);
        }

        /// <summary>The master spawned what this hand asked for: put it in the hand once it exists here and can be held.</summary>
        private void OnDelivered(uint id, bool right) => StartCoroutine(PutInHand(id, right));

        private IEnumerator PutInHand(uint id, bool right)
        {
            float until = Time.time + 3f;
            XRGrabInteractable grab = null;
            while (Time.time < until)
            {
                var obj = WorldManager.Find(id);
                grab = obj == null ? null : obj.GetComponentInChildren<XRGrabInteractable>();
                if (grab != null && grab.isActiveAndEnabled) break;
                grab = null;
                yield return null;
            }
            if (grab == null) { Logger.Warn($"PutInHand() '{gameObject.name}' — item {id} never became grabbable here; left where it appeared"); yield break; }

            XRBaseInteractor hand = HandInteractor(right);
            if (hand == null || hand.interactionManager == null) { Logger.Warn($"PutInHand() '{gameObject.name}' — no {(right ? "right" : "left")} hand interactor; left where it appeared"); yield break; }
            if (hand.hasSelection) yield break;   // the hand took something else meanwhile
            if (!TriggerDown(right)) yield break;  // let go before it arrived: it stays where it appeared

            // Held by script while the trigger is: XRI would otherwise drop it at once, since grip is what holds.
            hand.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grab);
            _holding[right ? 1 : 0] = hand;
        }

        /// <summary>The trigger came up on a hand holding a delivered item: hand it back to XRI, which keeps it if grip is held and drops it if not.</summary>
        private void LetGo(bool right)
        {
            int h = right ? 1 : 0;
            if (_holding[h] == null || TriggerDown(right)) return;
            if (_holding[h].isPerformingManualInteraction) _holding[h].EndManualInteraction();
            _holding[h] = null;
        }

        private bool TriggerDown(bool right)
        {
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller |
                (right ? InputDeviceCharacteristics.Right : InputDeviceCharacteristics.Left), _devices);
            return _devices.Count > 0 && _devices[0].TryGetFeatureValue(CommonUsages.triggerButton, out bool t) && t;
        }

        /// <summary>The hand's near-far interactor: the one that grabs, near or by ray.</summary>
        private static XRBaseInteractor HandInteractor(bool right)
        {
            Transform root = PlayerManager.LocalPlayerRig.Root;
            if (root == null) return null;
            Transform search = root.parent != null ? root.parent : root;
            XRBaseInteractor fallback = null;
            foreach (XRBaseInteractor i in search.GetComponentsInChildren<XRBaseInteractor>(true))
            {
                bool isRight = i.handedness == InteractorHandedness.Right || (i.handedness == InteractorHandedness.None && i.name.Contains("Right"));
                bool isLeft = i.handedness == InteractorHandedness.Left || (i.handedness == InteractorHandedness.None && i.name.Contains("Left"));
                if (right ? !isRight : !isLeft) continue;
                if (i.name.Contains("Near-Far")) return i;
                if (fallback == null && i.name.Contains("Grab")) fallback = i;
            }
            return fallback;
        }

        // ── UI helpers ────────────────────────────────────────────────────────────

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
