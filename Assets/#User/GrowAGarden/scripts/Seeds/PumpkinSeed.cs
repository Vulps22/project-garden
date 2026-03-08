using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
    public class PumpkinSeed : SeedDefinition
    {
        private void OnValidate() => Init();

        protected override void Init()
        {
            seedId      = "pumpkin";
            displayName = "Pumpkin";
            buyPrice    = 1;
            sellValue   = 10000000;
            phases      = new List<GrowthPhase>
            {
                new GrowthPhase { name = "vine",  duration = 360, maxScale = 1, scaleMultiplier = 1 },
                new GrowthPhase { name = "fruit", duration = 360, maxScale = 1, scaleMultiplier = 1 },
                new GrowthPhase { name = "decay", duration = 30,  maxScale = 1, scaleMultiplier = 1, isDecay = true }
            };
        }
    }
}
