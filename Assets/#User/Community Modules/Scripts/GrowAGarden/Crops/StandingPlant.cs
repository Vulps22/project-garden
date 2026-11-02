namespace Vulps.GrowAGarden.Crops
{
    /// <summary>
    /// A plant that is an upgrade rather than a crop: it grows, then stands for the rest of the session,
    /// bearing nothing and never withering. Its PlantSlot is freed only when the Plot is cleaned up after its
    /// owner leaves (Uproot). What it does once grown belongs to the components beside it, which read
    /// IsGrown; see docs/fun-seeds.md.
    /// </summary>
    public class StandingPlant : Plant
    {
        /// <summary>Fully grown, on every client: derived from the clock like all growth.</summary>
        public bool IsGrown => GetGrowthCompletion() >= 1f;

        /// <summary>Unix seconds when this was planted, or 0 before the state has arrived. Orders standing plants by age.</summary>
        public long PlantedAt => _plantedTimestamp;

        /// <summary>The body that grows, for whatever decorates it.</summary>
        public UnityEngine.Transform Body => _bodyToScale;

        protected override void OnFullyGrown()
        {
            Logger.Info($"OnFullyGrown() '{gameObject.name}' — grown at {transform.position}; standing for the session");
        }
    }
}
