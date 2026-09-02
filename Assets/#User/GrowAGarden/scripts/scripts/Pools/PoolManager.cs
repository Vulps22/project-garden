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

        /// <summary>
        /// Hands a seed back to storage. False means no pool owns this instance — it was spawned
        /// at runtime, and whoever is holding it has to dispose of it themselves. Not an error:
        /// while both stocking routes exist side by side, the caller cannot tell them apart and
        /// is not meant to guess.
        /// </summary>
        public bool ReturnPlantSeed(string seedId, PlantSeed plant)
        {
            if (_UnifiedPools.TryGetValue(seedId, out UnifiedPool pool))
            {
                return pool.Return(plant);
            }

            // No pool at all for this crop is worth saying out loud — it means the scene is
            // missing one, not that the seed was spawned. A spawned seed of a crop that does
            // still have a pool is refused quietly by the pool itself.
            Logger.Warn($"ReturnPlantSeed('{seedId}') — no UnifiedPool for this seed; caller must dispose of '{plant.name}' itself");
            return false;
        }
    }
}
