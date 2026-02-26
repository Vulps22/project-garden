using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    public class PlantPool : MonoBehaviour
    {
        [SerializeField] private SeedDefinition _seedDefinition;

        public string SeedId => _seedDefinition.seedId;
        public int Available => GetComponentsInChildren<Plantable>().Length;

        private void Awake()
        {
            var plants = GetComponentsInChildren<Plantable>();
            Logger.Info($"Awake() '{gameObject.name}' — seedId='{SeedId}', found {plants.Length} plant(s) to restore");
            foreach (Plantable plant in plants)
                Restore(plant);
        }

        public Plantable Claim()
        {
            Plantable plant = GetComponentInChildren<Plantable>();
            if (plant == null)
            {
                Logger.Warn($"Claim() — pool empty for '{SeedId}'!");
                return null;
            }
            Logger.Log($"Claim() '{SeedId}' — claiming '{plant.name}', remaining after={Available - 1}");
            plant.transform.SetParent(null);
            return plant;
        }

        public void Return(Plantable plant)
        {
            Logger.Log($"Return() '{SeedId}' — returning '{plant?.name}'");
            Restore(plant);
        }

        private void Restore(Plantable plant)
        {
            Logger.Log($"Restore() '{SeedId}' — restoring '{plant?.name}' to pool position {transform.position}");
            plant.transform.SetParent(transform);
            plant.transform.position = transform.position;
            plant.transform.rotation = transform.rotation;

            var grab = plant.GetComponent<XRGrabInteractable>();
            var rb = plant.GetComponent<Rigidbody>();
            if (grab != null) grab.enabled = false;
            if (rb != null) rb.isKinematic = true;

            plant.ResetForPool();

            var harvest = plant.GetComponent<HarvestInteractable>();
            if (harvest != null) harvest.ResetState();

            Logger.Log($"Restore() '{SeedId}' — '{plant?.name}' restore complete");
        }
    }
}
