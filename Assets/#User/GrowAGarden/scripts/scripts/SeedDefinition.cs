using UnityEngine;

namespace GrowAGarden
{
    [CreateAssetMenu(fileName = "NewSeed", menuName = "Garden/Seed Definition")]
    public class SeedDefinition : ScriptableObject
    {
        public string seedId;
        public string displayName;
        public Plantable plantPrefab;
        public float growDurationSeconds;
        public float maxScale;
        public float scaleMultiplier = 1f;
        public int buyPrice;
        public int sellValue;
        public Sprite icon;
    }
}