using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Vulps.GrowAGarden.Tutorial
{
    /// <summary>The tutorial's text, fixed below the middle of the player's view until the step changes: what this is, what to do, and a passing aside.</summary>
    public class InstructionPanel : MonoBehaviour
    {
        [Tooltip("The panel's font (the Notifier's).")]
        [SerializeField] private TMP_FontAsset _font;
        [Tooltip("How far in front of the eyes the panel sits, in metres.")]
        [SerializeField] private float _distance = 0.7f;
        [Tooltip("How far below the middle of the view its top edge sits, in metres at that distance.")]
        [SerializeField] private float _below = 0.12f;

        private RectTransform _root;
        private Camera _camera;
        private TextMeshProUGUI _body;
        private TextMeshProUGUI _instruction;
        private TextMeshProUGUI _aside;
        private GameObject _asideBox;
        private Material _boxMaterial;
        private float _asideUntil;

        private string _bodyText = string.Empty;
        private string _instructionText = string.Empty;
        private string _asideText = string.Empty;

        private void Awake() => enabled = false;

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
            if (_boxMaterial != null) Destroy(_boxMaterial);
        }

        /// <summary>Shows what this is (<paramref name="body"/>, may be empty) and what to do now (<paramref name="instruction"/>, may be empty).</summary>
        public void Show(string body, string instruction)
        {
            _bodyText = body ?? string.Empty;
            _instructionText = instruction ?? string.Empty;
            Logger.Info($"Show() '{gameObject.name}' — '{_bodyText}' / '{_instructionText}'");
            Apply();
        }

        /// <summary>Changes only what to do now, keeping the body.</summary>
        public void Instruct(string instruction) => Show(_bodyText, instruction);

        /// <summary>A passing line under the panel for <paramref name="seconds"/>, or until cleared if 0.</summary>
        public void Aside(string text, float seconds)
        {
            _asideText = text ?? string.Empty;
            _asideUntil = seconds > 0f ? Time.time + seconds : float.PositiveInfinity;
            Apply();
        }

        /// <summary>Takes the panel away.</summary>
        public void Hide()
        {
            _bodyText = _instructionText = _asideText = string.Empty;
            if (_root != null) _root.gameObject.SetActive(false);
            enabled = false;
        }

        private void Update()
        {
            if (_asideText.Length > 0 && Time.time >= _asideUntil) { _asideText = string.Empty; Apply(); }
            if (Camera.main != _camera) Apply();
        }

        private void Apply()
        {
            if (!Ensure()) { Logger.Warn($"Apply() '{gameObject.name}' — no camera to show the panel on"); return; }
            bool any = _bodyText.Length > 0 || _instructionText.Length > 0 || _asideText.Length > 0;
            _root.gameObject.SetActive(any);
            _body.text = _bodyText;
            _body.gameObject.SetActive(_bodyText.Length > 0);
            _instruction.text = _instructionText;
            _instruction.gameObject.SetActive(_instructionText.Length > 0);
            _aside.text = _asideText;
            _asideBox.SetActive(_asideText.Length > 0);
            enabled = any;
        }

        // ── Building ──────────────────────────────────────────────────────────────

        /// <summary>A world-space column fixed to the eyes, drawn over everything; rebuilt if the camera changes.</summary>
        private bool Ensure()
        {
            Camera camera = Camera.main;
            if (camera == null) return false;
            if (_root != null && camera == _camera) return true;
            if (_root != null) Destroy(_root.gameObject);
            _camera = camera;

            var go = new GameObject("Tutorial Panel (Grow A Garden)", typeof(RectTransform), typeof(Canvas), typeof(VerticalLayoutGroup));
            go.layer = (camera.cullingMask & 1) != 0 ? 0 : LowestLayer(camera.cullingMask);
            _root = (RectTransform)go.transform;
            _root.SetParent(camera.transform, false);
            _root.localPosition = new Vector3(0f, -_below, _distance);
            _root.localRotation = Quaternion.identity;
            _root.localScale = Vector3.one * 0.0002f;
            _root.pivot = new Vector2(0.5f, 1f);
            _root.sizeDelta = new Vector2(1300f, 900f);

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 31999;

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

            Transform main = Box("Panel", new Color(0.04f, 0.09f, 0.16f, 0.9f), 1300f);
            _body = Text(main, "Body", 44, new Color(0.92f, 0.96f, 1f), FontStyles.Normal);
            _instruction = Text(main, "Instruction", 50, new Color(0.45f, 0.85f, 1f), FontStyles.Bold);
            Transform aside = Box("Aside", new Color(0.04f, 0.09f, 0.16f, 0.75f), 1100f);
            _aside = Text(aside, "Text", 40, new Color(0.85f, 0.9f, 0.95f), FontStyles.Italic);
            _asideBox = aside.gameObject;
            return true;
        }

        private Transform Box(string name, Color colour, float width)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            go.layer = _root.gameObject.layer;
            go.transform.SetParent(_root, false);
            Image box = go.GetComponent<Image>();
            box.color = colour;
            box.material = _boxMaterial;
            box.raycastTarget = false;
            go.GetComponent<LayoutElement>().preferredWidth = width;
            var padding = go.GetComponent<VerticalLayoutGroup>();
            padding.padding = new RectOffset(36, 36, 24, 24);
            padding.spacing = 16;
            padding.childControlWidth = true;
            padding.childControlHeight = true;
            padding.childForceExpandHeight = false;
            return go.transform;
        }

        private TextMeshProUGUI Text(Transform parent, string name, float size, Color colour, FontStyles style)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.gameObject.layer = parent.gameObject.layer;
            label.transform.SetParent(parent, false);
            if (_font != null) label.font = _font;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = colour;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            OnTop(label.fontMaterial);
            return label;
        }

        /// <summary>Drawn last and through anything nearer, so the hut's walls never hide the panel.</summary>
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
