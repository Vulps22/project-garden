namespace GrowAGarden
{
    /// <summary>
    /// A rooted crop: the plant is the carrot. It grows for ten seconds and is then replaced by its
    /// produce, which sits in the plot until somebody pulls it. Nothing to ripen, so the produce
    /// arrives all but ready.
    ///
    /// Numbers are hardcoded so balance changes are code changes rather than Inspector edits. This
    /// one definition is referenced by all three of the crop's prefabs — seed, plant and produce.
    /// </summary>
    public class CarrotSeed : SeedDefinition
    {
        protected override void Init()
        {
            seedId = "carrot";
            displayName = "Carrot";
            buyPrice = 10;
            sellValue = 15;
            growthDuration = 10f;
            ripenDuration = 1f;
            witherDuration = 0f;
        }
    }
}
