using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Islands
{
    /// <summary>
    /// One Garden island, on the GardenIsland prefab's root. Every Garden is the same: four Plots, a sell point, three
    /// stalls, a scoreboard, a deed hut answering for its own four Plots, a screen and a dome. Gardens are placed in the
    /// scene, never spawned, grouped into towns for the layout (docs/towns.md); islands keep clear of them. Every client
    /// loads the same scene, so the list of Gardens is the same everywhere and nothing is sent.
    /// </summary>
    public class Garden : MonoBehaviour
    {
        private static readonly List<Garden> _all = new List<Garden>();

        [Tooltip("Where a player who falls out of the world near this Garden is put back: by its hut.")]
        [SerializeField] private Transform _returnPoint;

        private PlotLeaseManager[] _plots;

        /// <summary>Every Garden in the scene, in no particular order.</summary>
        public static IReadOnlyList<Garden> All
        {
            get
            {
                // A caller may come before the Gardens' own Awake; the scene is loaded by then, so look.
                if (_all.Count == 0) foreach (Garden g in FindObjectsByType<Garden>(FindObjectsSortMode.None)) g.Add();
                return _all;
            }
        }

        /// <summary>This Garden's four Plots: what its hut shows and claims for.</summary>
        public PlotLeaseManager[] Plots => _plots ??= GetComponentsInChildren<PlotLeaseManager>(true);

        public Transform ReturnPoint => _returnPoint;

        /// <summary>The Plots a hut piece answers for: its own Garden's, or, outside any, every Plot in the scene.</summary>
        public static PlotLeaseManager[] PlotsFor(Component piece)
        {
            Garden garden = piece.GetComponentInParent<Garden>(true);
            return garden != null ? garden.Plots : FindObjectsByType<PlotLeaseManager>(FindObjectsSortMode.None);
        }

        /// <summary>The Garden nearest a point, flat distance; null if there are none.</summary>
        public static Garden Nearest(Vector3 point)
        {
            Garden best = null;
            float bestSqr = float.MaxValue;
            foreach (Garden g in All)
            {
                if (g == null) continue;
                float sqr = FlatSqr(g.transform.position, point);
                if (sqr < bestSqr) { bestSqr = sqr; best = g; }
            }
            return best;
        }

        public static float FlatSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private void Awake() => Add();

        private void Add()
        {
            if (!_all.Contains(this)) _all.Add(this);
        }

        private void OnDestroy() => _all.Remove(this);
    }
}
