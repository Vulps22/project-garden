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


        public PlantSeed ClaimPlantSeed(string seedId)
        {
            Logger.Log($"claimPlantSeed('{seedId}') — available={(_UnifiedPools.TryGetValue(seedId, out var p) ? p.Available : -1)}");
            if (_UnifiedPools.TryGetValue(seedId, out UnifiedPool pool))
            {
                PlantSeed plant = pool.Claim();
                if (plant == null)
                {
                    Logger.Warn($"claimPlantSeed('{seedId}') — pool empty, Claim() returned null");
                    return null;
                }
                Logger.Info($"claimPlantSeed('{seedId}') — returned '{plant.name}', remaining={pool.Available}");
                return plant;
            }
            Logger.Error($"claimPlantSeed('{seedId}') — no PlantPool found for this seedId!");
            return null;
        }

        public void ReturnPlantSeed(string seedId, PlantSeed plant)
        {
            Logger.Log($"returnPlantSeed('{seedId}', '{plant?.name}')");
            if (_UnifiedPools.TryGetValue(seedId, out UnifiedPool pool))
            {
                pool.Return(plant);
                Logger.Info($"returnPlantSeed('{seedId}') — pool now has {pool.Available} available");
            }
            else
            {
                Logger.Error($"returnPlantSeed('{seedId}') — no PlantPool found, plant NOT returned!");
            }

        }
    }
}
