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
            description = "A tall stalk bearing three cobs at once. Each cob sells on its own.";
            buyPrice = 30;
            sellValue = 55;          // per cob; 3 cobs
            growthDuration = 20f;
            ripenDuration = 15f;
            witherDuration = 20f;
            spawnWeight = 0.2f;       // uncommon
        }
    }
}
