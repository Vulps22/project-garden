using UnityEngine;

namespace GrowAGarden
{
    public class CarrotPlant : Plantable
    {
        private void Awake()
        {
            Debug.Log($"[CarrotPlant] Awake() on '{gameObject.name}' � growDuration={growDurationSeconds}s, maxScale={maxScale}, scaleMultiplier={scaleMultiplier}");
        }

        public override void OnHarvested()
        {
            Debug.Log($"[CarrotPlant] OnHarvested() '{gameObject.name}' � completion was {GetGrowthCompletion():F3}");
            GetComponent<Rigidbody>().isKinematic = false;

        }
    }
}
