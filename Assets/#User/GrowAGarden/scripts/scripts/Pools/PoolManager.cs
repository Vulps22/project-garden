using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        private Dictionary<string, PlantPool> _plantPools = new Dictionary<string, PlantPool>();
        private Dictionary<string, SeedPool> _seedPools = new Dictionary<string, SeedPool>();
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

            foreach (PlantPool pool in GetComponentsInChildren<PlantPool>(true))
            {
                _plantPools[pool.SeedId] = pool;
                Logger.Info($"Registered PlantPool for seedId='{pool.SeedId}', available={pool.Available}");
            }

            foreach (SeedPool pool in GetComponentsInChildren<SeedPool>(true))
            {
                _seedPools[pool.SeedId] = pool;
                Logger.Info($"Registered SeedPool for seedId='{pool.SeedId}', available={pool.Available}");
            }

            foreach (UnifiedPool pool in GetComponentsInChildren<UnifiedPool>(true))
            {
                _UnifiedPools[pool.SeedId] = pool;
                Logger.Info($"Registered UnifiedPool for seedId='{pool.SeedId}', available={pool.Available}");
            }

            Logger.Info($"Awake() complete — {_plantPools.Count} plant pool(s), {_seedPools.Count} seed pool(s)");
        }

        public int PlantAvailable(string seedId)
        {
            if (_plantPools.TryGetValue(seedId, out PlantPool pool))
                return pool.Available;
            return 0;
        }

        public Plantable ClaimPlant(string seedId)
        {
            Logger.Log($"ClaimPlant('{seedId}') — available={PlantAvailable(seedId)}");
            if (_plantPools.TryGetValue(seedId, out PlantPool pool))
            {
                Plantable plant = pool.Claim();
                Logger.Info($"ClaimPlant('{seedId}') — returned '{(plant != null ? plant.name : "NULL")}', remaining={pool.Available}");
                return plant;
            }
            Logger.Error($"ClaimPlant('{seedId}') — no PlantPool found for this seedId!");
            return null;
        }

        public void ReturnPlant(string seedId, Plantable plant)
        {
            Logger.Log($"ReturnPlant('{seedId}', '{plant?.name}')");
            if (_plantPools.TryGetValue(seedId, out PlantPool pool))
            {
                pool.Return(plant);
                Logger.Info($"ReturnPlant('{seedId}') — pool now has {pool.Available} available");
            }
            else
            {
                Logger.Error($"ReturnPlant('{seedId}') — no PlantPool found, plant NOT returned!");
            }
        }

        public SeedObject ClaimSeed(string seedId)
        {
            Logger.Log($"ClaimSeed('{seedId}') — available={(_seedPools.TryGetValue(seedId, out var p) ? p.Available : -1)}");
            if (_seedPools.TryGetValue(seedId, out SeedPool pool))
            {
                SeedObject seed = pool.Claim();
                Logger.Info($"ClaimSeed('{seedId}') — returned '{(seed != null ? seed.name : "NULL")}', remaining={pool.Available}");
                return seed;
            }
            Logger.Error($"ClaimSeed('{seedId}') — no SeedPool found for this seedId!");
            return null;
        }

        public UnifiedPlantSeed claimUnifiedPlantSeed(string seedId)
        {
            Logger.Log($"claimUnifiedPlantSeed('{seedId}') — available={(_UnifiedPools.TryGetValue(seedId, out var p) ? p.Available : -1)}");
            if (_UnifiedPools.TryGetValue(seedId, out UnifiedPool pool))
            {
                UnifiedPlantSeed plant = pool.Claim();
                if (plant is UnifiedPlantSeed unifiedPlant)
                {
                    Logger.Info($"claimUnifiedPlantSeed('{seedId}') — returned '{unifiedPlant.name}', remaining={pool.Available}");
                    return unifiedPlant;
                }
                else
                {
                    Logger.Warn($"claimUnifiedPlantSeed('{seedId}') — claimed plant '{plant?.name}' is not a UnifiedPlantSeed!");
                    return null;
                }
            }
            Logger.Error($"claimUnifiedPlantSeed('{seedId}') — no PlantPool found for this seedId!");
            return null;
        }

        public void ReturnSeed(string seedId, SeedObject seed)
        {
            Logger.Log($"ReturnSeed('{seedId}', '{seed?.name}')");
            if (_seedPools.TryGetValue(seedId, out SeedPool pool))
            {
                pool.Return(seed);
                Logger.Info($"ReturnSeed('{seedId}') — pool now has {pool.Available} available");
            }
            else
            {
                Logger.Error($"ReturnSeed('{seedId}') — no SeedPool found, seed NOT returned!");
            }
        }
    }
}
