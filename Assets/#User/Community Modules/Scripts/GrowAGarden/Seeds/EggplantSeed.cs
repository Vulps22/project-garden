namespace Vulps.GrowAGarden.Seeds
{
    /// <summary>A regrowing crop: 3 fruit on the plant, each socket bearing 3 times before the plant withers.</summary>
    public class EggplantSeed : SeedDefinition
    {
        protected override void Init()
        {
            seedId = "eggplant";
            displayName = "Eggplant";
            tagline = "did someone say SPS? ;)";
            description = "Fruits three times on three branches: nine to sell from one seed. Some say it has other uses.";
            buyPrice = 45;
            sellValue = 14;          // per fruit; 3 sockets x 3 harvests = 9 fruit
            growthDuration = 40f;
            ripenDuration = 25f;
            witherDuration = 20f;
            harvestCount = 3;
            spawnWeight = 0.3f;      // the dear regrowing crop
        }
    }
}
