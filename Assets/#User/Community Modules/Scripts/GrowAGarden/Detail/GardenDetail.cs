using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.Show;
using Vulps.GrowAGarden.Video;

namespace Vulps.GrowAGarden.Detail
{
    /// <summary>
    /// One Garden's level of detail. Drawing is Unity's: a <see cref="LODGroup"/> on the Garden shows everything near,
    /// only the big shapes further out, and nothing past the player's draw distance. The bubble (this player inside the
    /// dome's sphere) is gameplay and an event: inside, the cut-up hut, the light-show materials, the lights, the fire
    /// and the screen; outside, the solid hut on plain materials and none of the rest. Only renderers, meshes, materials,
    /// lights and particles change, never objects, so nothing networked is touched. Local only. See docs/detail.md.
    /// </summary>
    [RequireComponent(typeof(Islands.Garden))]
    [RequireComponent(typeof(LODGroup))]
    public class GardenDetail : MonoBehaviour
    {
        [Tooltip("The hut's cut-up meshes, shown inside the bubble.")]
        [SerializeField] private MeshFilter[] _pieceFilters;
        [Tooltip("The whole meshes they were cut from, one per filter, shown outside it.")]
        [SerializeField] private Mesh[] _solidMeshes;
        [Tooltip("Light-show materials, swapped outside the bubble for the plain one at the same index.")]
        [SerializeField] private Material[] _showMaterials;
        [SerializeField] private Material[] _plainMaterials;
        [Tooltip("Extra materials drawn only inside the bubble (the fences' equaliser), removed outside it.")]
        [SerializeField] private Material[] _bubbleOnlyMaterials;
        [Tooltip("Out to this distance, in metres, everything is drawn; past it only the big shapes (LOD 1).")]
        [SerializeField] private float _nearDistance = 300f;

        private readonly List<Renderer> _swapped = new List<Renderer>();   // renderers holding a show or bubble-only material
        private readonly List<Renderer> _screens = new List<Renderer>();
        private readonly Dictionary<Renderer, Material> _ownedExtra = new Dictionary<Renderer, Material>();
        private LODGroup _lods;
        private Mesh[] _pieceMeshes;
        private Light[] _lights;
        private ParticleSystem[] _particles;
        private float _bubble = 45f;
        private bool _applied;

        /// <summary>Whether this player is inside this Garden's dome's sphere.</summary>
        public bool InBubble { get; private set; } = true;

        /// <summary>The player came into (true) or left (false) this Garden's bubble.</summary>
        public event System.Action<bool> BubbleChanged;

        /// <summary>How near counts as inside: the dome's radius.</summary>
        public float BubbleRadius => _bubble;

        private void Awake()
        {
            _lods = GetComponent<LODGroup>();
            DomeReveal dome = GetComponentInChildren<DomeReveal>(true);
            if (dome != null && dome.Radius > 0f) _bubble = dome.Radius;

            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                bool swapped = false;
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (System.Array.IndexOf(_bubbleOnlyMaterials, m) >= 0) { _ownedExtra[r] = m; swapped = true; }
                    if (System.Array.IndexOf(_showMaterials, m) >= 0) swapped = true;
                }
                if (swapped) _swapped.Add(r);
            }

            _pieceMeshes = new Mesh[_pieceFilters == null ? 0 : _pieceFilters.Length];
            for (int i = 0; i < _pieceMeshes.Length; i++) _pieceMeshes[i] = _pieceFilters[i] == null ? null : _pieceFilters[i].sharedMesh;
            _lights = GetComponentsInChildren<Light>(true);
            _particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void OnEnable() => DetailManager.Register(this);
        private void OnDisable() => DetailManager.Unregister(this);

        /// <summary>
        /// Sets the LODGroup's switch distances in metres for this camera: everything out to the near distance, the big
        /// shapes out to <paramref name="draw"/>, nothing past it. Unity measures a share of the screen and scales it by
        /// its LOD bias, so both are folded in to keep the distances true.
        /// </summary>
        public void SetDrawDistance(float draw, float fieldOfView)
        {
            if (_lods == null) return;
            LOD[] lods = _lods.GetLODs();
            if (lods.Length < 2) return;
            float near = Mathf.Min(_nearDistance, draw * 0.9f);
            lods[0].screenRelativeTransitionHeight = HeightAt(near, fieldOfView);
            lods[1].screenRelativeTransitionHeight = Mathf.Min(HeightAt(draw, fieldOfView), lods[0].screenRelativeTransitionHeight * 0.99f);
            _lods.SetLODs(lods);
        }

        private float HeightAt(float distance, float fieldOfView) =>
            _lods.size * QualitySettings.lodBias / (2f * Mathf.Max(1f, distance) * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad));

        /// <summary>
        /// Inside the bubble or out. Lights, fire and the screen switch only on a change; the hut's meshes and materials
        /// are checked again when <paramref name="recheck"/> is set, since the trip's own material swap can put ours back.
        /// </summary>
        public void SetBubble(bool inside, bool recheck)
        {
            bool changed = inside != InBubble || !_applied;
            _applied = true;
            InBubble = inside;
            if (!changed && !recheck) return;

            for (int i = 0; i < _pieceMeshes.Length; i++)
            {
                MeshFilter filter = _pieceFilters[i];
                Mesh want = inside || i >= _solidMeshes.Length || _solidMeshes[i] == null ? _pieceMeshes[i] : _solidMeshes[i];
                if (filter != null && want != null && filter.sharedMesh != want) filter.sharedMesh = want;
            }
            foreach (Renderer r in _swapped) if (r != null) SwapMaterials(r, inside);
            if (!changed) return;

            foreach (Light l in _lights) if (l != null) l.enabled = inside;
            foreach (ParticleSystem p in _particles)
            {
                if (p == null) continue;
                if (inside) { if (!p.isPlaying && p.main.playOnAwake) p.Play(false); }
                else if (p.isPlaying) p.Pause(false);
            }
            // Somnium builds the screen's renderers at runtime, so they are looked for at the change.
            _screens.Clear();
            foreach (ScreenReveal screen in GetComponentsInChildren<ScreenReveal>(true)) _screens.AddRange(screen.GetComponentsInChildren<Renderer>(true));
            foreach (Renderer r in _screens) if (r != null) r.forceRenderingOff = !inside;
            BubbleChanged?.Invoke(inside);
        }

        /// <summary>Show materials for plain ones and back; bubble-only materials removed or put back at the end.</summary>
        private void SwapMaterials(Renderer r, bool inside)
        {
            Material[] current = r.sharedMaterials;
            List<Material> next = null;
            for (int i = 0; i < current.Length; i++)
            {
                Material m = current[i];
                int show = System.Array.IndexOf(_showMaterials, m), plain = System.Array.IndexOf(_plainMaterials, m);
                Material want = m;
                if (!inside && show >= 0 && show < _plainMaterials.Length && _plainMaterials[show] != null) want = _plainMaterials[show];
                if (inside && plain >= 0 && plain < _showMaterials.Length) want = _showMaterials[plain];
                bool drop = !inside && System.Array.IndexOf(_bubbleOnlyMaterials, m) >= 0;
                if (want == m && !drop) { next?.Add(m); continue; }
                next ??= new List<Material>(current.Length);
                if (next.Count == 0) for (int k = 0; k < i; k++) next.Add(current[k]);
                if (!drop) next.Add(want);
            }
            if (inside && _ownedExtra.TryGetValue(r, out Material extra) && System.Array.IndexOf(current, extra) < 0)
            {
                next ??= new List<Material>(current);
                next.Add(extra);
            }
            if (next != null) r.sharedMaterials = next.ToArray();
        }
    }
}
