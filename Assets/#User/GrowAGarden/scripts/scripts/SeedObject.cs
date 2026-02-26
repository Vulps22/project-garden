using UnityEngine;

namespace GrowAGarden
{
    public class SeedObject : MonoBehaviour
    {
        public SeedDefinition seedDefinition;
        public float scaleOverride = 0f; // 0 = use SeedDefinition value

        private void Start()
        {
            Logger.Info($"Start() on '{gameObject.name}' — seedDefinition={(seedDefinition != null ? seedDefinition.displayName : "NULL")}, scaleOverride={scaleOverride}");

            if (seedDefinition == null)
            {
                Logger.Error($"seedDefinition is NULL on '{gameObject.name}' — this seed won't be recognized by PlantSlot!");
                return;
            }

            if (seedDefinition.plantPrefab == null)
                Logger.Error($"seedDefinition '{seedDefinition.displayName}' has NULL plantPrefab!");

            var col = GetComponent<Collider>();
            var colChild = GetComponentInChildren<Collider>();
            if (col == null && colChild == null)
                Logger.Error($"'{gameObject.name}' has NO Collider anywhere — it will never trigger PlantSlot!");
            else
                Logger.Info($"'{gameObject.name}' collider: {(col != null ? col.GetType().Name : colChild.GetType().Name)}");

            var rb = GetComponent<Rigidbody>();
            if (rb == null && GetComponentInParent<Rigidbody>() == null)
                Logger.Warn($"'{gameObject.name}' has no Rigidbody — trigger detection may not work!");
            else
                Logger.Info($"'{gameObject.name}' rigidbody found, isKinematic={rb?.isKinematic}");
        }

        private void OnDestroy()
        {
            Logger.Log($"OnDestroy() '{gameObject.name}'");
        }
    }
}
