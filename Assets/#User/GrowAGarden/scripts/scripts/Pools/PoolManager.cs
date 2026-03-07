using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        private Dictionary<string, UnifiedPool> _UnifiedPools = new Dictionary<string, UnifiedPool>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Logger.Warn($"Duplicate PoolManager on '{gameObject.name}' — destroying this one");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (UnifiedPool pool in GetComponentsInChildren<UnifiedPool>(true))
            {
                _UnifiedPools[pool.SeedId] = pool;
                Logger.Info($"Registered UnifiedPool for seedId='{pool.SeedId}', available={pool.Available}");
            }

            Logger.Info($"Awake() complete — {_UnifiedPools.Count} plant pool(s)");
        }


        public UnifiedPlantSeed ClaimUnifiedPlantSeed(string seedId)
        {
            Logger.Log($"claimUnifiedPlantSeed('{seedId}') — available={(_UnifiedPools.TryGetValue(seedId, out var p) ? p.Available : -1)}");
            if (_UnifiedPools.TryGetValue(seedId, out UnifiedPool pool))
            {
                UnifiedPlantSeed plant = pool.Claim();
                Logger.Info($"claimUnifiedPlantSeed('{seedId}') — returned '{plant.name}', remaining={pool.Available}");
                return plant;
            }
            Logger.Error($"claimUnifiedPlantSeed('{seedId}') — no PlantPool found for this seedId!");
            return null;
        }

        public void ReturnUnifiedPlantSeed(string seedId, UnifiedPlantSeed plant)
        {
            Logger.Log($"returnUnifiedPlantSeed('{seedId}', '{plant?.name}')");
            if (_UnifiedPools.TryGetValue(seedId, out UnifiedPool pool))
            {
                pool.Return(plant);
                Logger.Info($"returnUnifiedPlantSeed('{seedId}') — pool now has {pool.Available} available");
            }
            else
            {
                Logger.Error($"returnUnifiedPlantSeed('{seedId}') — no PlantPool found, plant NOT returned!");
            }

        }
    }
}
