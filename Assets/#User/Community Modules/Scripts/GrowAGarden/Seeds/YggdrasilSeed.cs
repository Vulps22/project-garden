namespace Vulps.GrowAGarden.Seeds
{
    /// <summary>
    /// The god seed, a buff (docs/yggdrasil.md): a sapling that builds the world tree behind its Plot, which multiplies
    /// what the Plot grows for 30 minutes, then falls. Developer Market only for now. Numbers are code, not Inspector values.
    /// </summary>
    public class YggdrasilSeed : SeedDefinition
    {
        /// <summary>From the tree being formed to its fall (Vulps: 30 minutes).</summary>
        public const float LifeSeconds = 1800f;

        /// <summary>One at a time, worldwide: no stall stocks it while a Yggdrasil stands.</summary>
        public override bool MayBeStocked => !WorldTree.YggdrasilPlant.AnyStanding;

        protected override void Init()
        {
            seedId = "yggdrasil";
            displayName = "Yggdrasil";
            tagline = "the world tree";
            description = "Grows a sapling that raises the world tree behind your Plot. For thirty minutes everything your Plot grows grows huge, and worth as much more. Then it falls.";
            buyPrice = 2000;         // 5,000 once Thatch persists (Vulps)
            sellValue = 0;           // a buff, not a crop
            buffMultiplier = 20f;    // Vulps: 20–30×; 20 until chosen
            growthDuration = 180f;   // the sapling; a placeholder until Vulps sets it
            ripenDuration = 0f;
            witherDuration = WorldTree.YggdrasilTree.DyingSeconds;   // from the leaves turning to the tree gone below the floor
            spawnWeight = 0.0008f;   // the rarest: about one island stall in 1,000 (Vulps)
        }
    }
}
