using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
    public abstract class SeedDefinition : MonoBehaviour
    {
        public string seedId;
        public string displayName;
        public int buyPrice;
        public int sellValue;

        public List<GrowthPhase> phases;

        private void OnEnable()
        {
            Init();
            Logger.Log($"SeedDefinition.OnEnable() '{seedId}' — phaseCount={phases?.Count ?? -1}");
        }

        protected abstract void Init();
    }

    public class GrowthPhase
    {
        public string name;
        public float duration;
        public float maxScale = 1f;
        public float scaleMultiplier = 1f;
        public bool isDecay;
    }
}
