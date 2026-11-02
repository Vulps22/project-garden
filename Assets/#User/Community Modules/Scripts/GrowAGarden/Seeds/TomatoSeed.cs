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
            buyPrice = 20;
            sellValue = 6;          // per fruit; 4 sockets x 3 harvests = 12 fruit
            growthDuration = 20f;
            ripenDuration = 15f;
            witherDuration = 20f;
            harvestCount = 3;
            spawnWeight = 0.85f;      // a cheap common that keeps giving
        }
    }
}
