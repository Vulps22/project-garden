namespace Vulps.GrowAGarden.Seeds
{
    /// <summary>A regrowing crop: 4 fruit on the plant, each socket bearing 3 times before the plant withers.</summary>
    public class TomatoSeed : SeedDefinition
    {
        protected override void Init()
        {
            seedId = "tomato";
            displayName = "Tomato";
            tagline = "fruits three times";
            description = "A cheap plant that fruits three times before it withers, so one seed keeps paying.";
            buyPrice = 20;
            sellValue = 22;         // per fruit; 4 sockets x 3 harvests = 12 fruit
            growthDuration = 20f;
            ripenDuration = 15f;
            witherDuration = 20f;
            harvestCount = 3;
            fruitPerPlant = 12;
            spawnWeight = 0.5f;      // the common island find
        }
    }
}
