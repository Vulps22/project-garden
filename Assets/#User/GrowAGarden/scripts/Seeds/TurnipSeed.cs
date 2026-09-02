namespace GrowAGarden
{
    /// <summary>
    /// Rooted, like the carrot, but slower and worth more.
    ///
    /// Numbers are hardcoded so balance changes are code changes rather than Inspector edits. This
    /// one definition is referenced by all three of the crop's prefabs — seed, plant and produce.
    /// </summary>
    public class TurnipSeed : SeedDefinition
    {
        protected override void Init()
        {
            seedId = "turnip";
            displayName = "Turnip";
            buyPrice = 18;
            sellValue = 25;
            growthDuration = 15f;
            ripenDuration = 1f;
            witherDuration = 0f;
        }
    }
}
