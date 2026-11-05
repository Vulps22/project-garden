namespace Vulps.GrowAGarden.Seeds
{
    /// <summary>
    /// An ARS seed (docs/moonflower.md): grows, then stands for the session as part of the Garden's light show. Nothing
    /// to harvest or sell. Numbers are code, not Inspector values.
    /// </summary>
    public class MoonflowerSeed : SeedDefinition
    {
        protected override void Init()
        {
            seedId = "moonflower";
            displayName = "Moonflower";
            tagline = "brings the night";
            description = "Grown, it beams into the sky and closes a black dome over the Garden, a light show to the screen's music. Grow two and the beams meet overhead.";
            buyPrice = 600;
            sellValue = 0;           // an upgrade, not a crop
            upgradeBonus = 0.30f;    // +30% profit on its Plot: rarest, and its night falls on the whole Garden
            growthDuration = 60f;
            ripenDuration = 0f;
            witherDuration = 0f;
            spawnWeight = 0.008f;    // very rare: about one island stall in 100, and the Garden's night
        }
    }
}
