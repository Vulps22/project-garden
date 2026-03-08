using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
    public class TurnipSeed : SeedDefinition
    {
        private void OnValidate() => Init();

        protected override void Init()
        {
            seedId      = "turnip";
            displayName = "Turnip";
            buyPrice    = 18;
            sellValue   = 28;
            phases      = new List<GrowthPhase>
            {
                new GrowthPhase { name = "plant", duration = 120, maxScale = 1, scaleMultiplier = 1 }
            };
        }
    }
}
