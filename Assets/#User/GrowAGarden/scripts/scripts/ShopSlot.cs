using Fusion;
using SomniumSpace.Network.Bridge;
using System.Collections;
using UnityEngine;

namespace GrowAGarden
{
    public class ShopSlot : MonoBehaviour
    {
        [SerializeField] private SeedDefinition _seedDefinition;
        private PlantSeed _currentSeed;

        private void Start()
        {

            if (_seedDefinition == null)
            {
                Logger.Error($"_seedDefinition is NULL on '{gameObject.name}' — slot will not function!");
                return;
            }

            if (SceneNetworking.IsNetworkReady)
            {
                SpawnSeed();
            }
            else
            {
                SceneNetworking.OnLocalPlayerJoined += SpawnSeed;
            }
        }

        private void OnDestroy()
        {
            SceneNetworking.OnLocalPlayerJoined -= SpawnSeed;
        }

        private void Update()
        {
            if (!SceneNetworking.IsMasterClient) return;

            bool seedMissing = _currentSeed == null
                            || _currentSeed.IsInPool   // planted, grown, sold — returned to pool
                            || !_currentSeed.IsSeed;   // planted but not yet returned to pool

            if (seedMissing)
            {
                _currentSeed = null;
                SpawnSeed();
            }
        }

        private void SpawnSeed()
        {

            if (!SceneNetworking.IsMasterClient)
            {
                return;
            }

            _currentSeed = PoolManager.Instance.ClaimPlantSeed(_seedDefinition.seedId);
            if (_currentSeed == null)
            {
                return;
            }


            _currentSeed.PlaceInShop(transform.position, transform.rotation);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out PlantSeed seed) && seed.IsSeed)
            {
                if(!seed.IsBought) _currentSeed = seed;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out PlantSeed seed) && seed == _currentSeed)
            {
                if (!seed.IsBought && seed.InShop)
                {
                    _currentSeed.Purchase();
                    EconomyManager.Instance.RemoveBalance(seed.GetGrabber().GetID(), _seedDefinition.buyPrice);
                    _currentSeed = null;
                    if (SceneNetworking.IsMasterClient)
                        SpawnSeed();
                }
            }
        }
    }
}
