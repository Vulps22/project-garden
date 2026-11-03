namespace Vulps.GrowAGarden.Seeds
{
    /// <summary>
    /// An ARS seed (docs/fun-seeds.md): grows, then stands for the session as part of the Garden's light show. Nothing
    /// to harvest or sell. Numbers are code, not Inspector values.
    /// </summary>
    public class MoonflowerSeed : SeedDefinition
    {
        protected override void Init()
        {
            seedId = "moonflower";
            displayName = "Moonflower";
            tagline = "brings the night";
            buyPrice = 150;
            sellValue = 0;           // an upgrade, not a crop
            growthDuration = 60f;
            ripenDuration = 0f;
            witherDuration = 0f;
            spawnWeight = 0.1f;      // a rare find, and the Garden's night
        }
    }
}
