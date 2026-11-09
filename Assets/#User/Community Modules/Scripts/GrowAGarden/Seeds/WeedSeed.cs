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
            tagline = "sell the bloom, or eat it raw";
            description = "Thanks DamDamDaaam for the models! You can sell it for a spliff OR eat it!";
            buyPrice = 200;
            sellValue = 140;         // per bloom; nine to a plant
            fruitPerPlant = 9;
            growthDuration = 90f;
            ripenDuration = 60f;
            witherDuration = 30f;
            spawnWeight = 0.02f;     // very rare: about one island stall in 45, and a windfall
        }
    }
}
