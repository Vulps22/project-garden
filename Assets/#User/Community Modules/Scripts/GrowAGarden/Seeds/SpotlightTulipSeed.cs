namespace Vulps.GrowAGarden.Seeds
{
    /// <summary>
    /// An ARS seed (docs/fun-seeds.md): grows, then stands for the session as part of the Garden's light show. Nothing
    /// to harvest or sell. Numbers are code, not Inspector values.
    /// </summary>
    public class SpotlightTulipSeed : SeedDefinition
    {
        protected override void Init()
        {
            seedId = "spotlight_tulip";
            displayName = "Spotlight Tulip";
            tagline = "a stage light for the Garden";
            buyPrice = 60;
            sellValue = 0;           // an upgrade, not a crop
            growthDuration = 40f;
            ripenDuration = 0f;
            witherDuration = 0f;
            spawnWeight = 0.2f;      // uncommon: a light rig is a few of them
        }
    }
}
