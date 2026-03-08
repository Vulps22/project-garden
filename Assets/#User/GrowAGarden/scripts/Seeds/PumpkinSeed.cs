using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GrowAGarden
{
    public class PumpkinSeed : SeedDefinition
    {
        bool debug = false;
        private void OnValidate() => Init();

        protected override void Init()
        {

            Define();
        }

        private void Define()
        {
            seedId = "pumpkin";
            displayName = "Pumpkin";
            buyPrice = debug ? 1 : 60;
            sellValue = debug ? 1 : 110;
            phases = new List<GrowthPhase>
            {
                new GrowthPhase { name = "vine",  duration = debug ? 10 : 360, maxScale = 1, scaleMultiplier = 1 },
                new GrowthPhase { name = "fruit", duration = debug ? 10 : 360, maxScale = 1, scaleMultiplier = 1 },
                new GrowthPhase { name = "decay", duration = debug ? 10 : 30,  maxScale = 1, scaleMultiplier = 1, isDecay = true }
            };
        }

        private void Update()
        {
            bool shouldDebug = Debug_EconomyBoost.Instance.used;
            if (debug != shouldDebug)
            {
                debug = shouldDebug;
                Define();
                Logger.Warn("Debugging Enabled on PumpkinSeed");
            }
        }

    }
}
