using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Vulps.GrowAGarden.Ownership;
using Vulps.GrowAGarden.Settings;

namespace Vulps.GrowAGarden.Notifications
{
    /// <summary>
    /// Toasts just below the middle of the player's view: <c>Notifier.Push("…")</c> shows a box for two seconds with a
    /// chime, silent if the last one came under five seconds before. Local only; off in World Settings. See docs/notifier.md.
    /// </summary>
    public class Notifier : MonoBehaviour
    {
        private const float DEFAULT_SECONDS = 3f;   // long enough to read (Vulps)
        private const float HARVEST_REPEAT_SECONDS = 10f;   // a plant bearing nine blooms is one notice, not nine
        private const float FADE_SECONDS = 0.25f;
        private const float QUIET_SECONDS = 5f;     // a push this soon after the last one makes no sound (Vulps)
        private const float SETTLE_SECONDS = 10f;   // joins this soon after our own are the room arriving, not news
        private const int MAX_SHOWN = 4;

        [Tooltip("Plays the chime. In the scene from load, so Somnium routes it through the player's volume settings.")]
        [SerializeField] private AudioSource _audio;
        [Tooltip("The chime (sounds/Notified 2.wav).")]
        [SerializeField] private AudioClip _sound;
        [Tooltip("The toast's font.")]
        [SerializeField] private TMP_FontAsset _font;
        [Tooltip("How far in front of the eyes the toasts sit, in metres.")]
        [SerializeField] private float _distance = 0.6f;
        [Tooltip("How far below the middle of the view the first toast sits, in metres at that distance.")]
        [SerializeField] private float _below = 0.06f;

        private static Notifier _instance;

        private RectTransform _column;
        private Camera _camera;
        private Material _boxMaterial;
        private float _lastPush = float.NegativeInfinity;
        private float _settledAt = float.PositiveInfinity;
        private readonly List<(CanvasGroup group, float shownAt, float seconds)> _shown = new List<(CanvasGroup, float, float)>();
        private readonly Dictionary<PlotLeaseManager, float> _lastHarvestNotice = new Dictionary<PlotLeaseManager, float>();
        private bool _wasDeveloper;

        /// <summary>Shows a toast to this player for <paramref name="seconds"/>, unless they have turned notifications off.</summary>
        public static void Push(string text, float seconds = DEFAULT_SECONDS)
        {
            if (_instance == null) { Logger.Warn($"Push() — no Notifier in the scene; dropped '{text}'"); return; }
            _instance.Show(text, Mathf.Max(0.5f, seconds));
        }

        private void Awake()
        {
            _instance = this;
            enabled = false;   // the fades run only while a toast is up
            PlayerManager.LocalPlayerJoined += OnLocalPlayerJoined;
            PlayerManager.PlayerJoined += OnPlayerJoined;
            Crops.Produce.AnyRipened += OnProduceRipened;
            DevTools.DeveloperAccess.Changed += OnDeveloperAccessChanged;
        }

        private void OnDestroy()
        {
            PlayerManager.LocalPlayerJoined -= OnLocalPlayerJoined;
            PlayerManager.PlayerJoined -= OnPlayerJoined;
            Crops.Produce.AnyRipened -= OnProduceRipened;
            DevTools.DeveloperAccess.Changed -= OnDeveloperAccessChanged;
            if (_instance == this) _instance = null;
            if (_column != null) Destroy(_column.gameObject);
            if (_boxMaterial != null) Destroy(_boxMaterial);
        }

        private void OnLocalPlayerJoined(string id, string name)
        {
            _settledAt = Time.time + SETTLE_SECONDS;
            _wasDeveloper = DevTools.Developer.IsLocal;
        }

        /// <summary>Something ripened on a Plot this player owns or is a teammate on: one notice per Plot every 10 s.</summary>
        private void OnProduceRipened(Crops.Produce produce)
        {
            string me = PlayerManager.LocalPlayerId;
            if (produce == null || string.IsNullOrEmpty(me)) return;
            PlotLeaseManager plot = PlotAt(produce.transform.position);
            if (plot == null || !(plot.OwnerId == me || Contains(plot.TeammateIds, me))) return;
            if (_lastHarvestNotice.TryGetValue(plot, out float last) && Time.time - last < HARVEST_REPEAT_SECONDS) return;
            _lastHarvestNotice[plot] = Time.time;
            Push("Something is ready to harvest");
        }

        /// <summary>The Plot a point is in: the nearest of its Garden's four, if it is close enough to be on it.</summary>
        private static PlotLeaseManager PlotAt(Vector3 point)
        {
            Islands.Garden garden = Islands.Garden.Nearest(point);
            if (garden == null) return null;
            PlotLeaseManager best = null;
            float bestSqr = 15f * 15f;
            foreach (PlotLeaseManager plot in garden.Plots)
            {
                if (plot == null) continue;
                float sqr = Islands.Garden.FlatSqr(plot.transform.position, point);
                if (sqr < bestSqr) { bestSqr = sqr; best = plot; }
            }
            return best;
        }

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            foreach (string each in ids) if (each == id) return true;
            return false;
        }

        /// <summary>This player was just given developer access (the owner always has it, so is never told).</summary>
        private void OnDeveloperAccessChanged()
        {
            bool now = DevTools.Developer.IsLocal;
            if (now && !_wasDeveloper && !DevTools.Developer.IsLocalOwner) Push("You have been given Developer Access");
            _wasDeveloper = now;
        }

        private void OnPlayerJoined(string id, string name)
        {
            if (id == PlayerManager.LocalPlayerId || Time.time < _settledAt) return;
            Push(string.IsNullOrEmpty(name) ? "A new player has joined the world" : $"{name} has joined the world");
        }

        private void Show(string text, float seconds)
        {
            GameSettings settings = GameSettings.GetInstance();
            if (settings != null && !settings.ShowNotifications) { Logger.Info($"Show() '{gameObject.name}' — notifications off; '{text}'"); return; }
            if (!EnsureColumn()) { Logger.Warn($"Show() '{gameObject.name}' — no camera to show '{text}' on"); return; }

            bool chime = Time.time - _lastPush >= QUIET_SECONDS;
            _lastPush = Time.time;
            if (chime && _audio != null && _sound != null) _audio.PlayOneShot(_sound);
            Logger.Info($"Show() '{gameObject.name}' — '{text}'{(chime ? string.Empty : ", silent")}");

            while (_shown.Count >= MAX_SHOWN) Drop(0);
            _shown.Add((MakeToast(text), Time.time, seconds));
            enabled = true;
        }

        private void Update()
        {
            for (int i = _shown.Count - 1; i >= 0; i--)
            {
                (CanvasGroup group, float shownAt, float seconds) = _shown[i];
                if (group == null) { _shown.RemoveAt(i); continue; }
                float age = Time.time - shownAt;
                if (age >= seconds) { Drop(i); continue; }
                group.alpha = Mathf.Clamp01(Mathf.Min(age, seconds - age) / FADE_SECONDS);
            }
            if (_shown.Count == 0) enabled = false;
        }

        private void Drop(int index)
        {
            if (_shown[index].group != null) Destroy(_shown[index].group.gameObject);
            _shown.RemoveAt(index);
        }

        // ── Building ──────────────────────────────────────────────────────────────

        /// <summary>A world-space column fixed to the eyes, drawn over everything; rebuilt if the camera changes.</summary>
        private bool EnsureColumn()
        {
            Camera camera = Camera.main;
            if (camera == null) return false;
            if (_column != null && camera == _camera) return true;
            if (_column != null) Destroy(_column.gameObject);
            _shown.Clear();
            _camera = camera;

            var go = new GameObject("Notifier (Grow A Garden)", typeof(RectTransform), typeof(Canvas), typeof(VerticalLayoutGroup));
            go.layer = (camera.cullingMask & 1) != 0 ? 0 : LowestLayer(camera.cullingMask);
            _column = (RectTransform)go.transform;
            _column.SetParent(camera.transform, false);
            _column.localPosition = new Vector3(0f, -_below, _distance);
            _column.localRotation = Quaternion.identity;
            _column.localScale = Vector3.one * 0.0002f;
            _column.pivot = new Vector2(0.5f, 1f);
            _column.sizeDelta = new Vector2(900f, 600f);

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 32000;

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (_boxMaterial == null)
            {
                _boxMaterial = new Material(Canvas.GetDefaultCanvasMaterial());
                OnTop(_boxMaterial);
            }
            return true;
        }

        private CanvasGroup MakeToast(string text)
        {
            var go = new GameObject("Toast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            go.layer = _column.gameObject.layer;
            go.transform.SetParent(_column, false);
            go.transform.SetAsLastSibling();

            Image box = go.GetComponent<Image>();
            box.color = new Color(0.07f, 0.1f, 0.14f, 0.88f);
            box.material = _boxMaterial;
            box.raycastTarget = false;
            go.GetComponent<LayoutElement>().preferredWidth = 900f;
            var padding = go.GetComponent<VerticalLayoutGroup>();
            padding.padding = new RectOffset(32, 32, 20, 20);
            padding.childControlWidth = true;
            padding.childControlHeight = true;
            padding.childForceExpandHeight = false;

            var label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.gameObject.layer = go.layer;
            label.transform.SetParent(go.transform, false);
            if (_font != null) label.font = _font;
            label.text = text;
            label.fontSize = 48;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            OnTop(label.fontMaterial);

            CanvasGroup group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            return group;
        }

        /// <summary>Drawn last and through anything nearer, so a wall or the tablet never hides a toast.</summary>
        private static void OnTop(Material material)
        {
            material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            material.renderQueue = 4000;
        }

        private static int LowestLayer(int mask)
        {
            for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) return i;
            return 0;
        }
    }
}
