using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Vulps.GrowAGarden.Crops;
using Vulps.GrowAGarden.DevTools;
using Vulps.GrowAGarden.Interaction;

namespace Vulps.GrowAGarden.Desktop
{
    /// <summary>
    /// What a 2D player sees of their hands and of K: two translucent white squares in the bottom left, each showing
    /// its item with the Almanac's and the Developer Market's tile, the selected one brighter; the "Press K to …"
    /// prompt; and the blue highlight on what K would act on.
    /// </summary>
    public partial class DesktopInteraction
    {
        private const float SQUARE = 120f;
        private const float GAP = 14f;
        private const float MARGIN = 28f;
        private static readonly Color EMPTY = new Color(1f, 1f, 1f, 0.16f);
        private static readonly Color CHOSEN = new Color(1f, 1f, 1f, 0.42f);

        private GameObject _hud;
        private readonly Image[] _squares = new Image[2];
        private readonly RawImage[] _tiles = new RawImage[2];
        private readonly TMP_Text[] _names = new TMP_Text[2];
        private readonly XRGrabInteractable[] _shownItem = new XRGrabInteractable[2];
        private int _shownSelected = -1;
        private GameObject _promptBox;
        private TMP_Text _prompt;
        private string _shownVerb;
        private GameObject _lit;
        private readonly List<GameObject> _shells = new List<GameObject>();

        private void BuildHud()
        {
            _hud = new GameObject("GAG_DesktopHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _hud.transform.SetParent(transform, false);
            Canvas canvas = _hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = _hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            for (int i = 0; i < 2; i++)
            {
                Image square = Box($"Hand{i + 1}", _hud.transform, EMPTY);
                RectTransform rect = square.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.sizeDelta = new Vector2(SQUARE, SQUARE);
                rect.anchoredPosition = new Vector2(MARGIN + i * (SQUARE + GAP), MARGIN);
                _squares[i] = square;

                var tile = new GameObject("Tile", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                tile.transform.SetParent(rect, false);
                Fill(tile.rectTransform, 10f);
                tile.raycastTarget = false;
                tile.enabled = false;
                _tiles[i] = tile;

                TMP_Text name = Label("Name", rect, string.Empty, 18f);
                Fill(name.rectTransform, 8f);
                _names[i] = name;

                TMP_Text key = Label("Key", rect, (i + 1).ToString(), 20f);
                key.alignment = TextAlignmentOptions.TopLeft;
                Fill(key.rectTransform, 6f);
            }

            Image box = Box("Prompt", _hud.transform, new Color(0.07f, 0.1f, 0.14f, 0.75f));
            RectTransform promptRect = box.rectTransform;
            promptRect.anchorMin = promptRect.anchorMax = promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 150f);
            promptRect.sizeDelta = new Vector2(420f, 56f);
            _prompt = Label("Text", promptRect, string.Empty, 30f);
            Fill(_prompt.rectTransform, 4f);
            _promptBox = box.gameObject;
            _promptBox.SetActive(false);
            _shownVerb = null;
            _shownSelected = -1;
        }

        private void DestroyHud()
        {
            if (_hud != null) Destroy(_hud);
            _hud = null;
            for (int i = 0; i < 2; i++) _shownItem[i] = null;
        }

        private void UpdateHud()
        {
            if (_hud == null) return;
            for (int i = 0; i < 2; i++)
            {
                XRGrabInteractable item = HeldIn(i);
                if (item != _shownItem[i])
                {
                    _shownItem[i] = item;
                    Texture tile = TileOf(item);
                    _tiles[i].texture = tile;
                    _tiles[i].enabled = tile != null;
                    _names[i].text = item != null && tile == null ? NameOf(item) : string.Empty;
                }
            }
            if (_shownSelected != _selected)
            {
                _shownSelected = _selected;
                for (int i = 0; i < 2; i++) _squares[i].color = i == _selected ? CHOSEN : EMPTY;
            }
        }

        /// <summary>The prompt, the highlight and, for planting, the beam, for what K would act on.</summary>
        private void ShowTarget(Target target, Transform head)
        {
            string verb = target.IsSome ? target.Verb : null;
            if (verb != _shownVerb && _prompt != null)
            {
                _shownVerb = verb;
                _promptBox.SetActive(verb != null);
                if (verb != null) _prompt.text = $"Press <b>K</b> to {verb}";
            }

            if (target.Shown != _lit)
            {
                ClearHighlight();
                _lit = target.Shown;
                if (_lit != null) Highlight(_lit);
            }

            XRGrabInteractable held = HeldIn(_selected);
            PlantingBeam.Mark(held != null ? held.transform : null, target.Slot);
        }

        private void Highlight(GameObject target)
        {
            if (_highlight == null) return;
            foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>())
            {
                MeshRenderer source = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || source == null || !source.enabled) continue;
                var shell = new GameObject("GAG_Highlight", typeof(MeshFilter), typeof(MeshRenderer));
                shell.layer = filter.gameObject.layer;
                shell.transform.SetParent(filter.transform, false);
                shell.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = shell.GetComponent<MeshRenderer>();
                Material[] materials = new Material[filter.sharedMesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = _highlight;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _shells.Add(shell);
            }
        }

        private void ClearHighlight()
        {
            foreach (GameObject shell in _shells) if (shell != null) Destroy(shell);
            _shells.Clear();
            _lit = null;
        }

        /// <summary>The Developer Market's tile for the thing, else its crop's Almanac picture; null if neither has one.</summary>
        private static Texture TileOf(XRGrabInteractable item)
        {
            if (item == null) return null;
            int index = DeveloperMarket.IndexOf(item.gameObject);
            Texture preview = index >= 0 ? DeveloperMarket.PreviewOf(index) : null;
            if (preview != null) return preview;
            SeedDefinition crop = CropOf(item);
            return crop != null ? crop.picture : null;
        }

        private static string NameOf(XRGrabInteractable item)
        {
            if (item == null) return "empty";
            int index = DeveloperMarket.IndexOf(item.gameObject);
            if (index >= 0) return DeveloperMarket.NameOf(index);
            SeedDefinition crop = CropOf(item);
            if (crop != null && !string.IsNullOrEmpty(crop.displayName)) return crop.displayName;
            if (item.TryGetComponent(out CollectibleEntity collectible) && !string.IsNullOrEmpty(collectible.displayName)) return collectible.displayName;
            return item.name.Replace("(Clone)", string.Empty).Trim();
        }

        private static SeedDefinition CropOf(XRGrabInteractable item)
        {
            if (item.TryGetComponent(out Seed seed)) return seed.seedDefinition;
            if (item.TryGetComponent(out Produce produce)) return produce.seedDefinition;
            return null;
        }

        // ── Building blocks ───────────────────────────────────────────────────────

        private static Image Box(string name, Transform parent, Color colour)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text Label(string name, Transform parent, string text, float size)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            if (_font != null) label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        private static void Fill(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
