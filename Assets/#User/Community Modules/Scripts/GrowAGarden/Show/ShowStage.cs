using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vulps.GrowAGarden.Show
{
    /// <summary>
    /// One Garden's dome, on its Dome beside DomeReveal and ShowSounds: which grown Moonflower in the Garden holds it (the
    /// oldest), where the star is, and the convergence when two or more beam (the star glides to the top of the dome and
    /// every beam arcs in to meet it). Every Garden has its own (docs/towns.md); the music they all dance to is the
    /// ShowManager's. Local on every client and sends nothing: plants grow on the same clock everywhere.
    /// </summary>
    [RequireComponent(typeof(DomeReveal))]
    public class ShowStage : MonoBehaviour
    {
        private static readonly List<ShowStage> _all = new List<ShowStage>();
        private readonly List<MoonflowerPlant> _grown = new List<MoonflowerPlant>();
        private MoonflowerPlant _current;
        private bool _hadCurrent;
        private DomeReveal _dome;

        private Vector3 _star;
        private Vector3 _starVelocity;
        private bool _starPlaced;
        private float _converged;

        /// <summary>The dome is up already (true: a late joiner's first sight) or gone (false). Not raised when it is raised in view.</summary>
        public event Action<bool> VisibilityChanged;

        /// <summary>A Moonflower took the dome in view: its beam climbs, then night spreads from its tip.</summary>
        public event Action<MoonflowerPlant> Raising;

        /// <summary>A beam starts to climb, from this point: the beam's sound starts there.</summary>
        public event Action<Vector3> BeamClimbing;

        /// <summary>A second Moonflower joined: the star sets off for the top of the dome.</summary>
        public event Action Converging;

        public bool IsVisible => _current != null;
        public MoonflowerPlant Holder => _current;

        /// <summary>Moonflowers beaming: every grown one while the dome is up.</summary>
        public int BeamCount => IsVisible ? _grown.Count : 0;

        public Vector3 Centre => transform.position;
        public float Radius => Dome.Radius;

        /// <summary>Where the star is: above the holder, or gliding to the top of the dome as beams converge.</summary>
        public Vector3 Star => _star;

        /// <summary>0 apart, 1 every beam meets at the top: eased by each beam as it bends.</summary>
        public float Converged => _converged;

        private DomeReveal Dome => _dome != null ? _dome : _dome = GetComponent<DomeReveal>();

        /// <summary>The dome of the Garden nearest a point: where a Moonflower planted there beams. Null if there are none.</summary>
        public static ShowStage For(Vector3 point)
        {
            ShowStage best = null;
            float bestSqr = float.MaxValue;
            foreach (ShowStage stage in _all)
            {
                if (stage == null) continue;
                float dx = stage.Centre.x - point.x, dz = stage.Centre.z - point.z;
                float sqr = dx * dx + dz * dz;
                if (sqr < bestSqr) { bestSqr = sqr; best = stage; }
            }
            return best;
        }

        /// <summary>The point on the dome straight above a position: where a beam from it meets the dome.</summary>
        public Vector3 ApexAbove(Vector3 from)
        {
            Vector3 c = Centre;
            float r = Radius;
            float dx = from.x - c.x, dz = from.z - c.z;
            float y = Mathf.Sqrt(Mathf.Max(0f, r * r - dx * dx - dz * dz));
            return new Vector3(from.x, c.y + y, from.z);
        }

        /// <summary>A beam starts to climb from a Moonflower: its sound.</summary>
        public void ReportClimb(MoonflowerPlant plant)
        {
            if (plant != null) BeamClimbing?.Invoke(plant.HeadPosition);
        }

        /// <summary>A Moonflower in this Garden has finished growing. seenGrowing: this client watched it grow.</summary>
        public void Register(MoonflowerPlant plant, bool seenGrowing)
        {
            if (plant == null || _grown.Contains(plant)) return;
            _grown.Add(plant);
            Choose(seenGrowing);
        }

        /// <summary>A Moonflower is gone. If it held the dome, the next oldest takes over.</summary>
        public void Unregister(MoonflowerPlant plant)
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
            Raising = null;
            BeamClimbing = null;
            Converging = null;
        }

        private void Update()
        {
            _grown.RemoveAll(p => p == null);
            if (_current == null) { _starPlaced = false; _converged = 0f; return; }
            bool converging = _grown.Count >= 2;
            Vector3 target = converging ? ApexAbove(Centre) : ApexAbove(_current.HeadPosition);
            if (!_starPlaced)
            {
                // First sight: no glide from wherever it last was.
                _star = target;
                _starVelocity = Vector3.zero;
                _converged = converging ? 1f : 0f;
                _starPlaced = true;
                return;
            }
            if (converging && _converged <= 0f) Converging?.Invoke();
            float seconds = ShowManager.ConvergeSeconds;
            _star = Vector3.SmoothDamp(_star, target, ref _starVelocity, seconds * 0.35f);
            _converged = Mathf.MoveTowards(_converged, converging ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, seconds));
        }

        private void Choose(bool seenGrowing)
        {
            _grown.RemoveAll(p => p == null);
            MoonflowerPlant next = null;
            foreach (MoonflowerPlant p in _grown)
                if (next == null || p.PlantedAt < next.PlantedAt) next = p;
            if (next == _current) return;

            bool wasVisible = _current != null;
            // A late joiner's first dome is already up: shown, not raised. A takeover, or one grown in view, raises it.
            bool raise = next != null && (_hadCurrent || seenGrowing);
            _current = next;
            if (next != null) _hadCurrent = true;

            Logger.Info($"Choose() '{gameObject.name}' — dome {(next == null ? "down" : $"held by '{next.name}'")}{(raise ? ", raising it" : "")}");
            if (raise) Raising?.Invoke(next);
            else if (wasVisible != (next != null)) VisibilityChanged?.Invoke(next != null);
        }
    }
}
