using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    public class PlantPool : MonoBehaviour
    {
        [SerializeField] private SeedDefinition _seedDefinition;

        public string SeedId => _seedDefinition.seedId;
        public int Available => GetComponentsInChildren<Plantable>(true).Length;

        private void Awake()
        {
            foreach (Plantable plant in GetComponentsInChildren<Plantable>(true))
                Restore(plant);
        }

        public Plantable Claim()
        {
            Plantable plant = GetComponentInChildren<Plantable>(true);
            if (plant == null)
            {
                Debug.LogWarning($"[PlantPool] Pool empty for '{SeedId}'!");
                return null;
            }
            plant.transform.SetParent(null);
            plant.gameObject.SetActive(true);
            return plant;
        }

        public void Return(Plantable plant)
        {
            Restore(plant);
        }

        private void Restore(Plantable plant)
        {
            plant.gameObject.SetActive(false);
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
        }
    }
}