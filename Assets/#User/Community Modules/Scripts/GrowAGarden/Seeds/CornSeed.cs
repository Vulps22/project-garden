namespace Vulps.GrowAGarden.Seeds
{
    /// <summary>A single-harvest bearing crop: a stalk with three cobs, withering once they are picked. Numbers are code, not Inspector values.</summary>
    public class CornSeed : SeedDefinition
    {
        protected override void Init()
        {
            seedId = "corn";
            displayName = "Corn";
            tagline = "three cobs a stalk";
            buyPrice = 25;
            sellValue = 13;          // per cob; 3 cobs, about what the single cob paid
            growthDuration = 20f;
            ripenDuration = 15f;
            witherDuration = 20f;
            spawnWeight = 0.45f;      // mid-table, between turnip and eggplant
        }
    }
}
