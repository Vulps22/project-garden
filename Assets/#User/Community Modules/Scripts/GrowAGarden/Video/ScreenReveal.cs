using UnityEngine;

namespace Vulps.GrowAGarden.Video
{
    /// <summary>
    /// Builds the Garden's video player from its bottom edge up as popcorn arrives, and lowers it when no plant holds it.
    /// The player's own transform is never touched: Somnium sizes its controls from it, and squashing it once left the
    /// controls a sliver for the session. Instead the picture and its backing (`FrameParent`, `BackQuad`, unit quads at
    /// the player's origin, docs/popcorn.md) are squashed, pinned at the bottom edge, and the controls are switched off
    /// until the screen is whole. While building it reports its top edge to the ScreenManager, which is where the popcorn
    /// flies. The player stays active throughout, as Somnium requires.
    /// </summary>
    [RequireComponent(typeof(ScreenManager))]
    public class ScreenReveal : MonoBehaviour
    {
        [Tooltip("Seconds for the screen to sink when no plant holds it.")]
        [SerializeField] private float _sinkSeconds = 3f;

        [Tooltip("Height while hidden, as a share of full height. Not zero, which breaks the native controls' colliders.")]
        [SerializeField] private float _hiddenHeight = 0.001f;

        private static readonly string[] SQUASHED = { "FrameParent", "BackQuad" };

        private Transform[] _parts;
        private Vector3[] _partScale;
        private Vector3[] _partOffset;   // each part's position in the player's space, whole
        private Canvas[] _controls;
        private float _shown;
        private float _target;
        private bool _building;
        private ScreenManager _screen;

        private void Awake()
        {
            _screen = GetComponent<ScreenManager>();
            _screen.VisibilityChanged += OnVisibilityChanged;
            _screen.Assembling += OnAssembling;
        }

        private void Start()
        {
            _target = _shown = _screen.IsVisible ? 1f : 0f;
            Apply();
        }

        private void OnDestroy()
        {
            _screen.VisibilityChanged -= OnVisibilityChanged;
            _screen.Assembling -= OnAssembling;
        }

        /// <summary>Already up (a late joiner) shows at once; gone sinks.</summary>
        private void OnVisibilityChanged(bool visible)
        {
            _building = false;
            _target = visible ? 1f : 0f;
            if (visible) { _shown = 1f; Apply(); }
        }

        /// <summary>Built from nothing, even if it was up: a takeover rebuilds it from the new plant.</summary>
        private void OnAssembling(PopcornPlant from)
        {
            _shown = 0f;
            _target = 1f;
            _building = true;
            Apply();
        }

        private void Update()
        {
            if (_parts == null) Apply();
            if (Mathf.Approximately(_shown, _target))
            {
                if (_building) { _building = false; _screen.ReportBuildEdge(false, Vector3.zero, Vector3.zero); }
                return;
            }
            float seconds = _building ? _screen.BuildSeconds : _sinkSeconds;
            _shown = Mathf.MoveTowards(_shown, _target, Time.deltaTime / Mathf.Max(0.01f, seconds));
            Apply();
        }

        /// <summary>Somnium attaches the native player some time after load; until its parts exist there is nothing to squash.</summary>
        private bool FindParts()
        {
            if (_parts != null) return true;
            var found = new System.Collections.Generic.List<Transform>();
            foreach (string name in SQUASHED)
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                    if (t.name == name) { found.Add(t); break; }
            if (found.Count < SQUASHED.Length) return false;

            _parts = found.ToArray();
            _partScale = new Vector3[_parts.Length];
            _partOffset = new Vector3[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
            {
                _partScale[i] = _parts[i].localScale;
                _partOffset[i] = transform.InverseTransformPoint(_parts[i].position);
            }
            Transform controls = null;
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name == "Controls") { controls = t; break; }
            _controls = controls == null ? new Canvas[0] : controls.GetComponentsInChildren<Canvas>(true);
            Logger.Info($"FindParts() '{gameObject.name}' — Somnium's player is here: squashing {_parts.Length} part(s), {_controls.Length} control canvas(es) held until whole");
            return true;
        }

        private void Apply()
        {
            if (!FindParts()) return;

            // Linear while building, so the edge rises at the pace the popcorn arrives; eased when sinking.
            float h = Mathf.Max(_hiddenHeight, _building ? _shown : Mathf.SmoothStep(0f, 1f, _shown));
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] == null) continue;
                _parts[i].localScale = new Vector3(_partScale[i].x, _partScale[i].y * h, _partScale[i].z);
                // Pin the bottom edge: drop the centre by the half-height that is missing, in the player's space.
                _parts[i].position = transform.TransformPoint(_partOffset[i] + new Vector3(0f, -0.5f * (1f - h), 0f));
            }

            bool whole = h >= 0.999f;
            foreach (Canvas c in _controls) if (c != null && c.enabled != whole) c.enabled = whole;

            if (_building)
                _screen.ReportBuildEdge(true, transform.TransformPoint(-0.5f, h - 0.5f, 0f), transform.TransformPoint(0.5f, h - 0.5f, 0f));
        }
    }
}
