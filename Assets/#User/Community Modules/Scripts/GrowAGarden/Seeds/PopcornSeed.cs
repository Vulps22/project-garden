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
            description = "Not for eating. Grown, it sends its popcorn flying to build the Garden's cinema screen.";
            buyPrice = 400;
            sellValue = 0;           // an upgrade, not a crop
            growthDuration = 60f;
            ripenDuration = 0f;
            witherDuration = 0f;
            spawnWeight = 0.045f;    // rare: a little more common than the tulip
        }
    }
}
