using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vulps.GrowAGarden.Video
{
    /// <summary>
    /// Whether the Garden's screen is up, and which grown popcorn plant holds it: the oldest. When a plant takes it while
    /// this client watches, the plant's popcorn streams to the screen and builds it from the bottom edge up. Local on
    /// every client and sends nothing: each client sees the same plants grow from the same clock, so each arrives at the
    /// same answer. See docs/fun-seeds.md.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        private static ScreenManager _instance;
        private readonly List<PopcornPlant> _grown = new List<PopcornPlant>();
        private PopcornPlant _current;
        private bool _hadCurrent;
        private Vector3 _edgeLeft, _edgeRight;
        private bool _building;

        /// <summary>The screen is up already (true: a late joiner's first sight) or gone (false). Not raised when it is built.</summary>
        public static event Action<bool> VisibilityChanged;

        /// <summary>A plant took the screen in view: the screen builds from nothing, fed by this plant's popcorn.</summary>
        public static event Action<PopcornPlant> Assembling;

        public static bool IsVisible => _instance != null && _instance._current != null;

        /// <summary>The plant the screen renders from, or null.</summary>
        public static PopcornPlant Holder => _instance == null ? null : _instance._current;

        /// <summary>Seconds a build takes, bottom to top; the popcorn and the screen both keep to it.</summary>
        public static float BuildSeconds => _instance == null ? 6f : _instance._buildSeconds;

        [Tooltip("Seconds for the popcorn to build the screen, bottom edge to top.")]
        [SerializeField] private float _buildSeconds = 6f;

        /// <summary>Whether the screen is being built now, and where its building edge is, in world space.</summary>
        public static bool TryGetBuildEdge(out Vector3 left, out Vector3 right)
        {
            left = right = Vector3.zero;
            if (_instance == null || !_instance._building) return false;
            left = _instance._edgeLeft;
            right = _instance._edgeRight;
            return true;
        }

        /// <summary>The screen reports its building edge each frame while it builds, and stops when done.</summary>
        public static void ReportBuildEdge(bool building, Vector3 left, Vector3 right)
        {
            if (_instance == null) return;
            _instance._building = building;
            _instance._edgeLeft = left;
            _instance._edgeRight = right;
        }

        /// <summary>A popcorn plant has finished growing. seenGrowing: this client watched it grow, rather than joining after.</summary>
        public static void Register(PopcornPlant plant, bool seenGrowing)
        {
            if (_instance == null || plant == null || _instance._grown.Contains(plant)) return;
            _instance._grown.Add(plant);
            _instance.Choose(seenGrowing);
        }

        /// <summary>A popcorn plant is gone. If it held the screen, the next oldest takes over.</summary>
        public static void Unregister(PopcornPlant plant)
        {
            if (_instance == null || !_instance._grown.Remove(plant)) return;
            if (plant == _instance._current) _instance.Choose(false);
        }

        private void Awake() => _instance = this;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            VisibilityChanged = null;
            Assembling = null;
        }

        private void Choose(bool seenGrowing)
        {
            _grown.RemoveAll(p => p == null);
            PopcornPlant next = null;
            foreach (PopcornPlant p in _grown)
                if (next == null || p.PlantedAt < next.PlantedAt) next = p;
            if (next == _current) return;

            bool wasVisible = _current != null;
            // A late joiner's first screen is already up: shown, not built. A takeover, or one grown in view, builds it.
            bool build = next != null && (_hadCurrent || seenGrowing);
            _current = next;
            if (next != null) _hadCurrent = true;

            Logger.Info($"Choose() '{gameObject.name}' — screen {(next == null ? "down" : $"held by '{next.name}'")}{(build ? ", building it from popcorn" : "")}");
            if (build) Assembling?.Invoke(next);
            else if (wasVisible != (next != null)) VisibilityChanged?.Invoke(next != null);
        }
    }
}
