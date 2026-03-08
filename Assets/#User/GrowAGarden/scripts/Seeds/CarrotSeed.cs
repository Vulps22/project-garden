using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{    
    public class CarrotSeed : SeedDefinition
    {
        private void OnValidate() => Init();

        protected override void Init()
        {
            seedId      = "carrot";
            displayName = "Carrot";
            buyPrice    = 10;
            sellValue   = 15;
            phases      = new List<GrowthPhase>
            {
                new GrowthPhase { name = "plant", duration = 10, maxScale = 1, scaleMultiplier = 1 }
            };
        }
    }
}
