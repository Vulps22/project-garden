namespace GrowAGarden
{
    /// <summary>
    /// A bearing crop. The vine grows for a minute, bears a pumpkin that ripens for another minute,
    /// and withers over thirty seconds once the pumpkin has been taken.
    ///
    /// Its seed prefab uses the fruit mesh rather than the foliage: every other crop shows its
    /// seedling leaves, but a pumpkin's foliage is a whole vine and looked absurd at shop scale.
    /// A stripped-down young-vine mesh is the real answer and is on the art list.
    ///
    /// Numbers are hardcoded so balance changes are code changes rather than Inspector edits. This
    /// one definition is referenced by all three of the crop's prefabs — seed, plant and produce.
    /// </summary>
    public class PumpkinSeed : SeedDefinition
    {
        protected override void Init()
        {
            seedId = "pumpkin";
            displayName = "Pumpkin";
            buyPrice = 60;
            sellValue = 110;
            growthDuration = 60f;
            ripenDuration = 60f;
            witherDuration = 30f;
            spawnWeight = 0.25f;     // the one you hope to find in a slot
        }
    }
}
