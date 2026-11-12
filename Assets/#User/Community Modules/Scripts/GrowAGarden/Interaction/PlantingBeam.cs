using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.Crops;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Interaction
{
    /// <summary>
    /// A beam down from a held seed to the plant slot it will go into. In VR, while a seed this client holds is over a
    /// dirt block, the beam locks onto the dirt's nearest free slot, and letting go plants it there. Not over dirt, no
    /// beam: the path is not dirt. A 2D player's slot is the one they look at, which the 2D controls mark here.
    /// Local only: the plant itself goes through PlantSlot.RequestPlant as ever. See docs/planting-beam.md.
    /// </summary>
    public class PlantingBeam : MonoBehaviour
    {
        [Tooltip("The beam's material: unlit, transparent.")]
        [SerializeField] private Material _material;

        [Tooltip("The beam's width, in metres.")]
        [SerializeField] private float _width = 0.015f;

        [Tooltip("How far below a seed a dirt block is still looked for, in metres.")]
        [SerializeField] private float _reach = 2.5f;

        private static PlantingBeam _instance;
        private static readonly RaycastHit[] _hits = new RaycastHit[16];

        private readonly List<Seed> _held = new List<Seed>();
        private readonly Dictionary<Seed, PlantSlot> _locked = new Dictionary<Seed, PlantSlot>();
        private readonly List<LineRenderer> _beams = new List<LineRenderer>();
        private Transform _markFrom;
        private PlantSlot _marked;

        /// <summary>Marks the slot a 2D player is looking at, from the seed they will plant; a null slot clears it.</summary>
        public static void Mark(Transform from, PlantSlot slot)
        {
            if (_instance == null) return;
            _instance._markFrom = slot == null ? null : from;
            _instance._marked = slot;
        }

        private void Awake()
        {
            _instance = this;
            Seed.LocalGrabbed += OnGrabbed;
            Seed.LocalReleased += OnReleased;
        }

        private void OnDestroy()
        {
            Seed.LocalGrabbed -= OnGrabbed;
            Seed.LocalReleased -= OnReleased;
            if (_instance == this) _instance = null;
        }

        private void OnGrabbed(Seed seed)
        {
            if (seed != null && !_held.Contains(seed)) _held.Add(seed);
        }

        /// <summary>Letting go of a seed over a locked slot plants it there. A 2D player's hands never let go to plant.</summary>
        private void OnReleased(Seed seed)
        {
            _held.Remove(seed);
            if (!_locked.TryGetValue(seed, out PlantSlot slot)) return;
            _locked.Remove(seed);
            if (slot == null || !PlayerManager.LocalIsVR || seed.IsPlanted || seed.IsDiscarded || !seed.CanBePlanted) return;
            Logger.Info($"OnReleased() '{gameObject.name}' — '{seed.name}' let go over '{slot.name}'; planting there");
            slot.RequestPlant(seed);
        }

        private void LateUpdate()
        {
            int shown = 0;
            if (_held.Count > 0 && PlayerManager.LocalIsVR)   // a seed is held only once the player exists, so VR is known
            {
                for (int i = _held.Count - 1; i >= 0; i--)
                {
                    Seed seed = _held[i];
                    if (seed == null) { _held.RemoveAt(i); continue; }
                    PlantSlot slot = seed.CanBePlanted && !seed.IsPlanted ? SlotUnder(seed) : null;
                    if (slot == null) { _locked.Remove(seed); continue; }
                    _locked[seed] = slot;
                    Draw(shown++, Middle(seed.gameObject), slot.transform.position);
                }
            }
            else if (_marked != null && _markFrom != null)
            {
                Draw(shown++, Middle(_markFrom.gameObject), _marked.transform.position);
            }

            for (int i = shown; i < _beams.Count; i++) if (_beams[i].enabled) _beams[i].enabled = false;
        }

        /// <summary>The nearest free slot of the dirt block straight under the seed, or null if what is under it is not dirt.</summary>
        private PlantSlot SlotUnder(Seed seed)
        {
            Vector3 from = Middle(seed.gameObject);
            int n = Physics.RaycastNonAlloc(from, Vector3.down, _hits, _reach, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(_hits, 0, n, HitDistance.Instance);
            for (int i = 0; i < n; i++)
            {
                Collider c = _hits[i].collider;
                if (c == null || c.attachedRigidbody != null) continue;            // things that move: seeds, plants, players
                if (c.GetComponentInParent<Plant>() != null || c.GetComponentInParent<Produce>() != null) continue;
                return NearestFreeSlot(c, from);                                    // the first fixed thing below decides
            }
            return null;
        }

        /// <summary>The dirt block a collider belongs to, if it is one, and its free slot nearest the point; null otherwise.</summary>
        public static PlantSlot NearestFreeSlot(Collider c, Vector3 point)
        {
            if (c == null) return null;
            PlantSlot own = c.GetComponentInParent<PlantSlot>();
            Transform dirt = own != null && own.transform.parent != null ? own.transform.parent : c.transform;
            PlantSlot best = null;
            float bestSqr = float.PositiveInfinity;
            foreach (Transform child in dirt)   // the block's own slots only: a parent of many blocks is not dirt
            {
                PlantSlot slot = child.GetComponent<PlantSlot>();
                if (slot == null || slot.Plot == null || slot.IsTaken) continue;
                Vector3 d = slot.transform.position - point;
                d.y = 0f;
                if (d.sqrMagnitude < bestSqr) { bestSqr = d.sqrMagnitude; best = slot; }
            }
            return best;
        }

        private static Vector3 Middle(GameObject thing)
        {
            Bounds bounds = default;
            bool any = false;
            foreach (Renderer r in thing.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any ? bounds.center : thing.transform.position;
        }

        private void Draw(int index, Vector3 from, Vector3 to)
        {
            while (_beams.Count <= index) _beams.Add(MakeBeam());
            LineRenderer beam = _beams[index];
            beam.enabled = true;
            beam.SetPosition(0, from);
            beam.SetPosition(1, to);
        }

        private LineRenderer MakeBeam()
        {
            var go = new GameObject("PlantingBeam", typeof(LineRenderer));
            go.transform.SetParent(transform, false);
            LineRenderer beam = go.GetComponent<LineRenderer>();
            beam.positionCount = 2;
            beam.useWorldSpace = true;
            beam.widthMultiplier = _width;
            beam.sharedMaterial = _material;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam.receiveShadows = false;
            beam.enabled = false;
            if (_material == null) Logger.Warn($"MakeBeam() '{gameObject.name}' — no beam material; the beam draws pink");
            return beam;
        }

        private sealed class HitDistance : IComparer<RaycastHit>
        {
            public static readonly HitDistance Instance = new HitDistance();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
