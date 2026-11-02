namespace Vulps.GrowAGarden.Seeds
{
    /// <summary>
    /// The video seed: a popcorn kernel that grows, spraying popcorn, and once grown switches on the Garden's
    /// screen for the session. Nothing to harvest or sell. Numbers are code, not Inspector values.
    /// </summary>
    public class PopcornSeed : SeedDefinition
    {
        protected override void Init()
        {
            seedId = "popcorn";
            displayName = "Popcorn";
            tagline = "grows a cinema screen";
            buyPrice = 120;
            sellValue = 0;           // an upgrade, not a crop
            growthDuration = 60f;
            ripenDuration = 0f;
            witherDuration = 0f;
            spawnWeight = 0.12f;     // a lucky find, rarer than weed
        }
    }
}
