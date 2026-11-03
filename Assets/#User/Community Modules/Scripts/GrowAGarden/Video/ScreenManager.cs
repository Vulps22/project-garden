using System;
using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.Islands;

namespace Vulps.GrowAGarden.Video
{
    /// <summary>
    /// One Garden's screen, on its video player beside ScreenReveal: whether it is up, and which grown popcorn plant in the
    /// Garden holds it, the oldest. When a plant takes it while this client watches, the plant's popcorn streams to the
    /// screen and builds it from the bottom edge up. Local on every client and sends nothing: each client sees the same
    /// plants grow from the same clock, so each arrives at the same answer. See docs/popcorn.md and docs/towns.md.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        private static readonly List<ScreenManager> _all = new List<ScreenManager>();
        private readonly List<PopcornPlant> _grown = new List<PopcornPlant>();
        private PopcornPlant _current;
        private bool _hadCurrent;
        private Vector3 _edgeLeft, _edgeRight;
        private bool _building;

        [Tooltip("Seconds for the popcorn to build the screen, bottom edge to top.")]
        [SerializeField] private float _buildSeconds = 6f;

        /// <summary>The screen is up already (true: a late joiner's first sight) or gone (false). Not raised when it is built.</summary>
        public event Action<bool> VisibilityChanged;

        /// <summary>A plant took the screen in view: the screen builds from nothing, fed by this plant's popcorn.</summary>
        public event Action<PopcornPlant> Assembling;

        public bool IsVisible => _current != null;

        /// <summary>The plant the screen renders from, or null.</summary>
        public PopcornPlant Holder => _current;

        /// <summary>Seconds a build takes, bottom to top; the popcorn and the screen both keep to it.</summary>
        public float BuildSeconds => _buildSeconds;

        /// <summary>The screen of the Garden nearest a point: where a popcorn plant planted there builds. Null if there are none.</summary>
        public static ScreenManager For(Vector3 point)
        {
            Garden garden = Garden.Nearest(point);
            foreach (ScreenManager screen in _all)
                if (screen != null && (garden == null || screen.GetComponentInParent<Garden>() == garden)) return screen;
            return null;
        }

        /// <summary>Whether the screen is being built now, and where its building edge is, in world space.</summary>
        public bool TryGetBuildEdge(out Vector3 left, out Vector3 right)
        {
            left = _edgeLeft;
            right = _edgeRight;
            return _building;
        }

        /// <summary>The screen reports its building edge each frame while it builds, and stops when done.</summary>
        public void ReportBuildEdge(bool building, Vector3 left, Vector3 right)
        {
            _building = building;
            _edgeLeft = left;
            _edgeRight = right;
        }

        /// <summary>A popcorn plant in this Garden has finished growing. seenGrowing: this client watched it grow, rather than joining after.</summary>
        public void Register(PopcornPlant plant, bool seenGrowing)
        {
            if (plant == null || _grown.Contains(plant)) return;
            _grown.Add(plant);
            Choose(seenGrowing);
        }

        /// <summary>A popcorn plant is gone. If it held the screen, the next oldest takes over.</summary>
        public void Unregister(PopcornPlant plant)
        {
            if (!_grown.Remove(plant)) return;
            if (plant == _current) Choose(false);
        }

        private void Awake()
        {
            if (!_all.Contains(this)) _all.Add(this);
        }

        private void OnDestroy()
        {
            _all.Remove(this);
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
