using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    public class SeedPool : MonoBehaviour
    {
        [SerializeField] private SeedDefinition _seedDefinition;

        public string SeedId => _seedDefinition.seedId;
        public int Available => GetComponentsInChildren<SeedObject>(true).Length;

        private void Awake()
        {
            foreach (SeedObject seed in GetComponentsInChildren<SeedObject>(true))
                Restore(seed);
        }

        public SeedObject Claim()
        {
            SeedObject seed = GetComponentInChildren<SeedObject>();
            if (seed == null)
            {
                Debug.LogWarning($"[SeedPool] Pool empty for '{SeedId}'!");
                return null;
            }
            seed.transform.SetParent(null);
            return seed;
        }

        public void Return(SeedObject seed)
        {
            Restore(seed);
        }

        private void Restore(SeedObject seed)
        {
            seed.transform.SetParent(transform);
            seed.transform.position = transform.position;
            seed.transform.rotation = transform.rotation;

            var rb = seed.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }
    }
}