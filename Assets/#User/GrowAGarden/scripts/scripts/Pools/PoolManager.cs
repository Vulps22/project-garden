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
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (UnifiedPool pool in GetComponentsInChildren<UnifiedPool>(true))
            {
                if (pool.SeedId == null) throw new System.Exception("Null Seed ID found in UnifiedPool: " + pool.name);
                _UnifiedPools[pool.SeedId] = pool;
            }
        }


        public PlantSeed ClaimPlantSeed(string seedId)
        {
            if (_UnifiedPools.TryGetValue(seedId, out UnifiedPool pool))
            {
                PlantSeed plant = pool.Claim();
                if (plant == null)
                {
                    return null;
                }
                return plant;
            }
            return null;
        }

        public void ReturnPlantSeed(string seedId, PlantSeed plant)
        {
            if (_UnifiedPools.TryGetValue(seedId, out UnifiedPool pool))
            {
                pool.Return(plant);
            }
            else
            {
                Logger.Error($"returnPlantSeed('{seedId}') — no PlantPool found, plant NOT returned!");
            }

        }
    }
}
