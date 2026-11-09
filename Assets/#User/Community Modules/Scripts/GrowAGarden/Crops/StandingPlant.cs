using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Crops
{
    /// <summary>
    /// A plant that is an upgrade rather than a crop: it grows, then stands for the rest of the session,
    /// bearing nothing and never withering. Its PlantSlot is freed only when the Plot is cleaned up after its
    /// owner leaves (Uproot). What it does once grown belongs to the components beside it, which read
    /// IsGrown; see docs/fun-seeds.md. Once grown it raises the profit of what its Plot grows (docs/economy.md).
    /// </summary>
    public class StandingPlant : Plant, IPlotUpgrade
    {
        /// <summary>Fully grown, on every client: derived from the clock like all growth.</summary>
        public bool IsGrown => GetGrowthCompletion() >= 1f;

        /// <summary>Unix seconds when this was planted, or 0 before the state has arrived. Orders standing plants by age.</summary>
        public long PlantedAt => _plantedTimestamp;

        /// <summary>The body that grows, for whatever decorates it.</summary>
        public UnityEngine.Transform Body => _bodyToScale;

        public string Kind => seedDefinition == null ? "" : seedDefinition.seedId;
        public float ActiveBonus => IsGrown && seedDefinition != null ? seedDefinition.upgradeBonus : 0f;

        // Kept apart from _plot, which Uproot clears before the plant is gone.
        private PlotStateManager _upgrading;

        protected override void OnPlotKnown(PlotStateManager plot)
        {
            if (_upgrading == plot) return;
            if (_upgrading != null) _upgrading.RemoveUpgrade(this);
            _upgrading = plot;
            _upgrading.AddUpgrade(this);
        }

        protected override void OnDestroy()
        {
            if (_upgrading != null) _upgrading.RemoveUpgrade(this);
            base.OnDestroy();
        }

        protected override void OnFullyGrown()
        {
            Logger.Info($"OnFullyGrown() '{gameObject.name}' — grown at {transform.position}; standing for the session");
        }
    }
}
