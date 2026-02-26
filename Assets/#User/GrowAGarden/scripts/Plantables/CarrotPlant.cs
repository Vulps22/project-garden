using UnityEngine;

namespace GrowAGarden
{
    public class CarrotPlant : Plantable
    {
        private void Awake()
        {
            Logger.Info($"Awake() on '{gameObject.name}' — growDuration={growDurationSeconds}s, maxScale={maxScale}, scaleMultiplier={scaleMultiplier}");
        }

        public override void OnHarvested()
        {
            Logger.Info($"OnHarvested() '{gameObject.name}' — completion was {GetGrowthCompletion():F3}");
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
                Logger.Error($"OnHarvested() '{gameObject.name}' — no Rigidbody found, cannot release physics!");
            else
                rb.isKinematic = false;
        }
    }
}
