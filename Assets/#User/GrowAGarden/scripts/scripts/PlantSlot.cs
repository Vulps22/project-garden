using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    public class PlantSlot : MonoBehaviour
    {
        private Plantable _currentPlant = null;
        private string _currentSeedId = null;
        private bool _planting = false;
        private const float TIMEOUT = 2f;


        public bool IsOccupied() => _currentPlant != null || _planting;

        public void Plant(SeedDefinition seed, float scaleOverride = 0f)
        {
            if (IsOccupied()) return;
            _planting = true;
            Plantable plant = PoolManager.Instance.ClaimPlant(seed.seedId);
            if (plant == null)
            {
                _planting = false;
                return;
            }
            StartCoroutine(PlantWithAuthority(plant, seed, scaleOverride));
        }

        private IEnumerator PlantWithAuthority(Plantable plant, SeedDefinition seed, float scaleOverride)
        {
            var networkObject = plant.GetComponent<NetworkObject>();
            if (networkObject != null && !networkObject.HasStateAuthority)
            {
                networkObject.RequestStateAuthority();
                float t = Time.time;
                while (!networkObject.HasStateAuthority)
                {
                    if (Time.time - t > TIMEOUT)
                    {
                        Debug.LogWarning($"[PlantSlot] Timeout getting authority on {plant.name}");
                        PoolManager.Instance.ReturnPlant(seed.seedId, plant);
                        _planting = false;
                        yield break;
                    }
                    yield return null;
                }
            }

            _currentPlant = plant;
            _currentSeedId = seed.seedId;
            _planting = false;

            plant.transform.position = transform.position;
            plant.transform.SetParent(transform);
            plant.growDurationSeconds = seed.growDurationSeconds;
            plant.maxScale = scaleOverride > 0f ? scaleOverride : seed.maxScale;
            plant.scaleMultiplier = seed.scaleMultiplier;
            plant.OnPlanted();
        }

        public void Load(SeedDefinition seed, long savedTimestamp, float scaleOverride = 0f)
        {
            if (IsOccupied()) return;

            _currentPlant = PoolManager.Instance.ClaimPlant(seed.seedId);
            if (_currentPlant == null) return;

            _currentSeedId = seed.seedId;
            _currentPlant.transform.position = transform.position;
            _currentPlant.growDurationSeconds = seed.growDurationSeconds;
            _currentPlant.maxScale = scaleOverride > 0f ? scaleOverride : seed.maxScale;
            _currentPlant.scaleMultiplier = seed.scaleMultiplier;
            _currentPlant.OnLoaded(savedTimestamp);
        }

        public void Harvest()
        {
            if (!IsOccupied()) return;
            _currentPlant.OnHarvested();
            PoolManager.Instance.ReturnPlant(_currentSeedId, _currentPlant);
            _currentPlant = null;
            _currentSeedId = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsOccupied()) return;
            SeedObject seed = other.GetComponent<SeedObject>();
            if (seed == null) return;

            // Force-drop the seed by disabling the grab interactable before returning to pool
            var grab = seed.GetComponent<XRGrabInteractable>();
            if (grab != null)
                grab.enabled = false;

            PoolManager.Instance.ReturnSeed(seed.seedDefinition.seedId, seed);
            Plant(seed.seedDefinition, seed.scaleOverride);
        }
    }
}