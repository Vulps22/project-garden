namespace Vulps.GrowAGarden.Seeds
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
            tagline = "a little slower, a little richer";
            description = "The carrot's patient cousin: a few seconds longer in the ground, a few more Thatch when you sell it.";
            buyPrice = 18;
            sellValue = 26;
            growthDuration = 15f;
            ripenDuration = 1f;
            witherDuration = 0f;
            spawnWeight = 0.6f;      // rarer than the staple, and dearer
        }
    }
}
