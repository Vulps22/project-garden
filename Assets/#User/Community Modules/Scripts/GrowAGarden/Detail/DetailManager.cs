using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.Settings;

namespace Vulps.GrowAGarden.Detail
{
    /// <summary>
    /// Tells each Garden whether this player is inside its bubble (the dome's sphere), whenever the player moves a few
    /// metres, and hands every Garden's LODGroup the draw distance whenever it changes. Unity's LODGroups do the drawing.
    /// Local only. See docs/detail.md.
    /// </summary>
    public class DetailManager : MonoBehaviour
    {
        [Tooltip("How much further out the bubble's edge is when leaving it than when entering, so it does not flicker.")]
        [SerializeField] private float _hysteresis = 10f;
        [Tooltip("How far the player moves, in metres, before the bubbles are worked out again.")]
        [SerializeField] private float _step = 5f;

        private static readonly List<GardenDetail> _gardens = new List<GardenDetail>();
        private static DetailManager _instance;
        private float _fieldOfView = -1f;

        public static void Register(GardenDetail garden)
        {
            if (_gardens.Contains(garden)) return;
            _gardens.Add(garden);
            if (_instance == null) return;
            _instance.ApplyDrawDistance(garden);
            _instance.Evaluate(garden, false);
        }

        public static void Unregister(GardenDetail garden) => _gardens.Remove(garden);

        /// <summary>The Garden whose bubble the player is in, by its town and where it stands, or null.</summary>
        public static string CurrentBubble
        {
            get
            {
                foreach (GardenDetail g in _gardens)
                {
                    if (g == null || !g.InBubble) continue;
                    Vector3 p = g.transform.position;
                    return $"{(g.transform.parent != null ? g.transform.parent.name : g.name)}@{p.x:0},{p.z:0}";
                }
                return null;
            }
        }

        private void OnEnable()
        {
            _instance = this;
            PlayerTracker.Watch(PlayerTracker.Grid(_step), OnPlayerMoved);
            GameSettings.Changed += OnSettingsChanged;
            Trip.TripManager.MaterialsSwapped += OnMaterialsSwapped;
        }

        private void OnDisable()
        {
            PlayerTracker.Unwatch(OnPlayerMoved);
            GameSettings.Changed -= OnSettingsChanged;
            Trip.TripManager.MaterialsSwapped -= OnMaterialsSwapped;
            if (_instance == this) _instance = null;
        }

        private void OnDestroy() => _gardens.Clear();

        private void OnPlayerMoved(Vector3 at)
        {
            // The camera's field of view is only known once there is a camera, which is when the player first moves.
            float fov = Camera.main != null ? Camera.main.fieldOfView : 90f;
            if (!Mathf.Approximately(fov, _fieldOfView)) { _fieldOfView = fov; foreach (GardenDetail g in _gardens) ApplyDrawDistance(g); }
            foreach (GardenDetail g in _gardens) Evaluate(g, false);
        }

        private void OnSettingsChanged()
        {
            foreach (GardenDetail g in _gardens) ApplyDrawDistance(g);
        }

        // The trip puts back the materials it saw when it started, show materials included: put ours right again.
        private void OnMaterialsSwapped()
        {
            foreach (GardenDetail g in _gardens) Evaluate(g, true);
        }

        private void ApplyDrawDistance(GardenDetail g)
        {
            if (g == null || _fieldOfView <= 0f) return;
            GameSettings settings = GameSettings.GetInstance();
            g.SetDrawDistance(settings != null ? settings.DrawDistance : GameSettings.DEFAULT_DRAW_DISTANCE, _fieldOfView);
        }

        private void Evaluate(GardenDetail g, bool recheck)
        {
            if (g == null || !PlayerTracker.HasPosition) return;
            float d = Vector3.Distance(PlayerTracker.Position, g.transform.position);
            bool inside = g.InBubble ? d <= g.BubbleRadius + _hysteresis : d <= g.BubbleRadius;
            g.SetBubble(inside, recheck);
        }
    }
}
