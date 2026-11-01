namespace Vulps.GrowAGarden.Seeds
{
    /// <summary>
    /// A bearing crop with nine blooms, each sold on its own. A rarer find than pumpkin, and a longer
    /// grow, so the blooms are priced to pay for the wait. See docs/weed.md.
    /// </summary>
    public class WeedSeed : SeedDefinition
    {
        protected override void Init()
        {
            seedId = "weed";
            displayName = "Weed";
            buyPrice = 80;
            sellValue = 30;          // per bloom; nine to a plant
            growthDuration = 90f;
            ripenDuration = 60f;
            witherDuration = 30f;
            spawnWeight = 0.15f;     // a find, not a staple
        }
    }
}
