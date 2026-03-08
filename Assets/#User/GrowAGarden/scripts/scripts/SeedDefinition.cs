using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
    [CreateAssetMenu(fileName = "NewSeed", menuName = "Garden/Seed Definition")]
    public class SeedDefinition : ScriptableObject
    {
        public string seedId;
        public string displayName;
        public List<GrowthPhase> phases;
        public int buyPrice;
        public int sellValue;
    }

    [System.Serializable]
    public class GrowthPhase {
        public string name;
        public float duration;
        public float maxScale = 1f;
        public float scaleMultiplier = 1f;
        public bool isDecay;
    }
}