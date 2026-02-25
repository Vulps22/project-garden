using UnityEngine;

namespace GrowAGarden
{
    public class SeedObject : MonoBehaviour
    {
        public SeedDefinition seedDefinition;
        public float scaleOverride = 0f; // 0 = use SeedDefinition value


        private void Start()
        {
            Debug.Log($"[SeedObject] Start() on '{gameObject.name}' � seedDefinition={(seedDefinition != null ? seedDefinition.displayName : "NULL")}");
            if (seedDefinition == null)
                Debug.LogError($"[SeedObject] seedDefinition is NULL on '{gameObject.name}' � this seed won't be recognized by PlantSlot!");
            else if (seedDefinition.plantPrefab == null)
                Debug.LogError($"[SeedObject] seedDefinition '{seedDefinition.displayName}' has NULL plantPrefab!");

            var collider = GetComponent<Collider>();
            var colliderInChildren = GetComponentInChildren<Collider>();
            if (collider == null && colliderInChildren == null)
                Debug.LogError($"[SeedObject] '{gameObject.name}' has NO Collider anywhere � it will never trigger PlantSlot!");
            else
                Debug.Log($"[SeedObject] '{gameObject.name}' has collider: {(collider != null ? collider.GetType().Name : colliderInChildren.GetType().Name)}");

            var rb = GetComponent<Rigidbody>();
            var rbInParent = GetComponentInParent<Rigidbody>();
            if (rb == null && rbInParent == null)
                Debug.LogWarning($"[SeedObject] '{gameObject.name}' has no Rigidbody (checked parents) � trigger detection may not work!");
        }

        private void OnDestroy()
        {
            Debug.Log($"[SeedObject] OnDestroy() '{gameObject.name}'");
        }
    }
}
