using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vulps.GrowAGarden.Islands;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Map
{
    /// <summary>Draws the seed's islands and the Garden onto Somnium's tablet map in world coordinates, for whatever part of the map is in view.</summary>
    public class MapOverlay : MonoBehaviour
    {
        [Tooltip("The field whose seed decides the islands. Its position is the map's origin.")]
        [SerializeField] private IslandField _field;

        [Tooltip("Map units per world metre.")]
        [SerializeField] private float _unitsPerMetre = 2f;

        [Tooltip("How far from the origin the map may be scrolled, in metres.")]
        [SerializeField] private float _scrollExtent = 50000f;

        [Tooltip("Most islands drawn at once; zoomed far out, the furthest from the view's centre are left off.")]
        [SerializeField] private int _maxDrawn = 1500;

        [Tooltip("Largest radius, in metres, the view is searched for islands; beyond it the map shows only what is within this of its centre.")]
        [SerializeField] private float _maxSearchRadius = 4000f;

        [Tooltip("Seconds between checks for whether the view has changed while the map is open.")]
        [SerializeField] private float _redrawInterval = 0.25f;

        [Tooltip("When the view spans more than this many metres from its centre, sectors are drawn instead of islands.")]
        [SerializeField] private float _islandViewLimit = 900f;

        [Tooltip("Most sectors drawn at once.")]
        [SerializeField] private int _maxSectors = 400;

        [Tooltip("Sector tile colour.")]
        [SerializeField] private Color _sectorColour = new Color(0.2f, 0.35f, 0.5f, 0.35f);

        [Tooltip("Sector label colour.")]
        [SerializeField] private Color _sectorLabelColour = new Color(1f, 1f, 1f, 0.85f);

        [Tooltip("Top-down picture of the Garden. Set by Grow A Garden > Bake Island Map Sprites (GardenIsland.png).")]
        [SerializeField] private Sprite _gardenSprite;

        [Tooltip("Metres the Garden sprite spans, edge to edge.")]
        [SerializeField] private float _gardenSpan = 60f;

        [Tooltip("Size of the player's arrow on screen, in map units at zoom 1. It does not change with zoom.")]
        [SerializeField] private float _dotSize = 48f;

        [Tooltip("Colour of the sector the player is in, when zoomed out.")]
        [SerializeField] private Color _currentSectorColour = new Color(1f, 0.85f, 0.2f, 0.55f);

        private readonly List<IslandField.Placement> _found = new List<IslandField.Placement>();
        private readonly List<Image> _islandMarks = new List<Image>();
        private readonly List<Image> _sectorTiles = new List<Image>();
        private readonly List<TextMeshProUGUI> _sectorLabels = new List<TextMeshProUGUI>();
        private TMP_FontAsset _font;
        private Vector4 _lastView;
        private float _lastZoom;
        private Vector3 _viewAnchor;
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _content;
        private RectTransform _layer;
        private readonly List<Image> _gardens = new List<Image>();
        private Image _you;
        private float _nextLookup;
        private float _nextDraw;
        private float _nextProbe;
        private float _nextReport;
        private bool _wasOpen;
        private bool _reportedMissing;
        private string _lastReport;

        private void Update()
        {
            if (_content == null)
            {
                if (Time.time >= _nextProbe)
                {
                    _nextProbe = Time.time + 3f;
                    MapManager.SearchForTablet();
                }
                FindMap();
                return;
            }

            if (Time.time >= _nextProbe)
            {
                _nextProbe = Time.time + 1f;
                MapManager.ProbeTablet(_content);
            }

            if (!_content.gameObject.activeInHierarchy)
            {
                _wasOpen = false;
                return;
            }

            MapManager.HideSomniumContents(_content);
            MapManager.SetContentSize(_content, Vector2.one * (2f * _scrollExtent * _unitsPerMetre));
            HoldCentreThroughZoom();
            KeepArrowSteady();

            if (!_wasOpen)
            {
                _wasOpen = true;
                _lastView = Vector4.positiveInfinity;
                _lastZoom = 0f;
                Draw();
                CentreOnPlayer();
                Logger.Info($"Update() '{gameObject.name}' — map opened; players list: {MapManager.DescribePlayersContainer(_content)}");
            }

            Report();

            if (Time.time < _nextDraw) return;
            _nextDraw = Time.time + _redrawInterval;
            if (ViewChanged()) Draw();
        }

        /// <summary>When the zoom changes, puts the map point that was at the centre of the view back there, so zooming never pans.</summary>
        private void HoldCentreThroughZoom()
        {
            RectTransform viewport = MapManager.ViewportOf(_content);
            if (viewport == null) return;

            viewport.GetWorldCorners(_corners);
            Vector3 viewCentre = (_corners[0] + _corners[2]) * 0.5f;
            float zoom = _content.localScale.x;

            if (_lastZoom > 0f && !Mathf.Approximately(zoom, _lastZoom))
                _content.position += viewCentre - _layer.TransformPoint(_viewAnchor);

            _viewAnchor = _layer.InverseTransformPoint(viewCentre);
            _lastZoom = zoom;
        }

        /// <summary>Keeps the player's arrow the same size on screen at any zoom, pointing where the player faces.</summary>
        private void KeepArrowSteady()
        {
            float zoom = _content.localScale.x;
            if (zoom > 0f) _you.rectTransform.localScale = Vector3.one / zoom;

            Transform me = PlayerAnchor();
            if (me != null) _you.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -me.eulerAngles.y);
        }

        /// <summary>Whether the view, the zoom or the player has moved enough since the last draw to be worth drawing again.</summary>
        private bool ViewChanged()
        {
            ViewInWorld(_field.transform.position, out Vector3 centre, out float radius);
            Transform me = PlayerAnchor();
            Vector3 player = me != null ? me.position : Vector3.zero;
            var view = new Vector4(Mathf.Round(centre.x / 5f), Mathf.Round(centre.z / 5f), Mathf.Round(radius / 5f),
                                   Mathf.Round(player.x) * 7919f + Mathf.Round(player.z));
            if (view == _lastView) return false;
            _lastView = view;
            return true;
        }

        private void OnDestroy()
        {
            MapManager.RestoreSomniumContents();
            if (_layer != null) Destroy(_layer.gameObject);
        }

        /// <summary>Looks for the tablet's map under the local player's rig, every couple of seconds until it is there.</summary>
        private void FindMap()
        {
            if (Time.time < _nextLookup) return;
            _nextLookup = Time.time + 2f;

            Transform root = PlayerManager.LocalPlayerRig.Root;
            if (root == null) return;

            _content = MapManager.FindMapContent(root);
            if (_content == null)
            {
                if (!_reportedMissing) Logger.Warn($"FindMap() '{gameObject.name}' — no tablet map under '{root.name}'; Somnium's map is left alone");
                _reportedMissing = true;
                return;
            }

            BuildLayer();
            Logger.Info($"FindMap() '{gameObject.name}' — found the tablet map; drawing at {_unitsPerMetre} units per metre");
        }

        /// <summary>Creates our layer at the centre of Somnium's panel, behind Somnium's own markers.</summary>
        private void BuildLayer()
        {
            var layerGo = new GameObject("GAG_MapLayer", typeof(RectTransform));
            _layer = (RectTransform)layerGo.transform;
            _layer.SetParent(_content, false);
            _layer.anchorMin = _layer.anchorMax = new Vector2(0.5f, 0.5f);
            _layer.anchoredPosition = Vector2.zero;
            _layer.sizeDelta = Vector2.zero;
            _layer.SetAsFirstSibling();

            _font = MapManager.MapFont(_content);
            _you = NewMark("GAG_You");
            _you.sprite = MapManager.PlayerDotSprite(_content);
            _you.rectTransform.sizeDelta = new Vector2(_dotSize, _dotSize);
        }

        /// <summary>Draws the islands in view, the Garden and the player, in map units from the field's origin.</summary>
        private void Draw()
        {
            Vector3 origin = _field.transform.position;

            ViewInWorld(origin, out Vector3 viewCentre, out float viewRadius);
            bool sectors = viewRadius > _islandViewLimit;
            DrawSectors(sectors, origin, viewCentre, viewRadius);
            if (sectors) _found.Clear();
            else _field.PlacementsNear(viewCentre, Mathf.Min(viewRadius, _maxSearchRadius), _found);
            if (_found.Count > _maxDrawn)
            {
                _found.Sort((a, b) => FlatDistance(a.Position, viewCentre).CompareTo(FlatDistance(b.Position, viewCentre)));
                _found.RemoveRange(_maxDrawn, _found.Count - _maxDrawn);
            }

            while (_islandMarks.Count < _found.Count) _islandMarks.Add(NewMark("GAG_Island"));
            for (int i = 0; i < _islandMarks.Count; i++)
            {
                Image mark = _islandMarks[i];
                bool used = i < _found.Count;
                mark.gameObject.SetActive(used);
                if (!used) continue;

                IslandField.Placement p = _found[i];
                SpawnableIsland island = p.Prefab;
                bool hasSprite = island.MapSprite != null && island.MapSpan > 0f;
                mark.sprite = hasSprite ? island.MapSprite : null;
                mark.color = hasSprite ? Color.white : new Color(0.5f, 0.7f, 0.5f, 0.8f);
                Vector3 box = SpawnableIsland.MaxSize(island.Category);
                Vector2 metres = hasSprite ? Vector2.one * island.MapSpan : new Vector2(box.x, box.z);
                Place(mark.rectTransform, p.Position - origin, metres, p.Rotation.eulerAngles.y);
            }

            // Every Garden in the scene, every town's.
            IReadOnlyList<Garden> gardens = Garden.All;
            while (_gardens.Count < gardens.Count) { Image g = NewMark("GAG_Garden"); g.sprite = _gardenSprite; _gardens.Add(g); }
            for (int i = 0; i < _gardens.Count; i++)
            {
                Image mark = _gardens[i];
                bool used = i < gardens.Count && gardens[i] != null;
                mark.gameObject.SetActive(used);
                if (!used) continue;
                Place(mark.rectTransform, gardens[i].transform.position - origin, Vector2.one * _gardenSpan, 0f);
                mark.color = _gardenSprite != null ? Color.white : new Color(0.95f, 0.85f, 0.35f);
                mark.transform.SetAsLastSibling();
            }

            Transform me = PlayerAnchor();
            if (me != null) _you.rectTransform.anchoredPosition = ToMap(me.position - origin);
            _you.transform.SetAsLastSibling();
        }

        /// <summary>Shows a labelled tile for each sector in view, or hides them all when zoomed in to islands.</summary>
        private void DrawSectors(bool show, Vector3 origin, Vector3 viewCentre, float viewRadius)
        {
            int used = 0;
            if (show)
            {
                float size = SectorManager.SectorSize;
                Vector2Int mine = SectorManager.HasLocalSector ? SectorManager.LocalSector : new Vector2Int(int.MinValue, int.MinValue);
                int reach = Mathf.CeilToInt(viewRadius / size) + 1;
                Vector2Int view = SectorManager.SectorOf(viewCentre);
                int cx = view.x, cz = view.y;
                for (int dz = -reach; dz <= reach && used < _maxSectors; dz++)
                for (int dx = -reach; dx <= reach && used < _maxSectors; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (used == _sectorTiles.Count) AddSectorTile();
                    Image tile = _sectorTiles[used];
                    TextMeshProUGUI label = _sectorLabels[used];
                    tile.gameObject.SetActive(true);
                    tile.color = x == mine.x && z == mine.y ? _currentSectorColour : _sectorColour;
                    Place(tile.rectTransform, new Vector3(x * size, 0f, z * size), Vector2.one * (size * 0.97f), 0f);
                    label.text = x == 0 && z == 0 ? "Sector 0" : $"Sector {x},{z}";
                    used++;
                }
            }
            for (int i = used; i < _sectorTiles.Count; i++) _sectorTiles[i].gameObject.SetActive(false);
        }

        private void AddSectorTile()
        {
            Image tile = NewMark("GAG_Sector");
            tile.color = _sectorColour;
            tile.preserveAspect = false;
            tile.transform.SetAsFirstSibling();

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(tile.transform, false);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            if (_font != null) label.font = _font;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = SectorManager.SectorSize * _unitsPerMetre * 0.12f;
            label.color = _sectorLabelColour;
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            _sectorTiles.Add(tile);
            _sectorLabels.Add(label);
        }

        /// <summary>Scrolls Somnium's panel so the player's dot sits in the middle of the view.</summary>
        private void CentreOnPlayer()
        {
            RectTransform viewport = MapManager.ViewportOf(_content);
            if (viewport == null || PlayerAnchor() == null) return;

            viewport.GetWorldCorners(_corners);
            Vector3 viewCentre = (_corners[0] + _corners[2]) * 0.5f;
            _content.position += viewCentre - _you.rectTransform.position;
        }

        /// <summary>The part of the world the map's viewport is showing, as a centre and a radius.</summary>
        private void ViewInWorld(Vector3 origin, out Vector3 centre, out float radius)
        {
            RectTransform viewport = MapManager.ViewportOf(_content);
            if (viewport == null)
            {
                Transform me = PlayerAnchor();
                centre = me != null ? me.position : origin;
                radius = 1500f;
                return;
            }

            viewport.GetWorldCorners(_corners);
            Vector3 a = _layer.InverseTransformPoint(_corners[0]);
            Vector3 b = _layer.InverseTransformPoint(_corners[2]);
            Vector2 mid = (a + b) * 0.5f / _unitsPerMetre;
            centre = origin + new Vector3(mid.x, 0f, mid.y);
            radius = 0.5f * Vector2.Distance(a, b) / _unitsPerMetre;
        }

        private static Transform PlayerAnchor()
        {
            Transform head = PlayerManager.LocalPlayerHead;
            return head != null ? head : PlayerManager.LocalPlayerRig.Root;
        }

        private Vector2 ToMap(Vector3 offset) => new Vector2(offset.x, offset.z) * _unitsPerMetre;

        private void Place(RectTransform rect, Vector3 offset, Vector2 metres, float yaw)
        {
            rect.anchoredPosition = ToMap(offset);
            rect.sizeDelta = metres * _unitsPerMetre;
            rect.localRotation = Quaternion.Euler(0f, 0f, -yaw);
        }

        private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).sqrMagnitude;

        private Image NewMark(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_layer, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            return image;
        }

        /// <summary>Logs the panel's size, zoom and position whenever they change, every two seconds at most.</summary>
        private void Report()
        {
            if (Time.time < _nextReport) return;
            _nextReport = Time.time + 2f;

            string report = $"content size={_content.rect.size:F0} scale={_content.localScale.x:F3} pos={_content.anchoredPosition:F0} pivot={_content.pivot:F2} islandsDrawn={_found.Count}";
            if (report == _lastReport) return;
            _lastReport = report;
            Logger.Info($"Report() '{gameObject.name}' — {report}");
        }

        private void OnValidate()
        {
            if (_field == null) _field = GetComponent<IslandField>();
            if (_unitsPerMetre <= 0f) _unitsPerMetre = 0.01f;
            if (_maxDrawn < 1) _maxDrawn = 1;
            if (_maxSectors < 1) _maxSectors = 1;
        }
    }
}
