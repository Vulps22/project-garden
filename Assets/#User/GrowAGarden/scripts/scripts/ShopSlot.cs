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
                // Must run even when _currentSeed is already null: the join-time SpawnSeed()
                // fires from OnLocalPlayerJoined, before Fusion has spawned the pooled scene
                // objects, so UnifiedPool.Restore() has not yet flagged anything IsInPool and
                // Claim() returns null. This retry is what stocks the slot once it can.
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
                // Only latch seeds this slot actually sells — a seed of another type carried
                // through the trigger must not become this slot's _currentSeed.
                if (seed.seedDefinition == null || seed.seedDefinition.seedId != _seedDefinition.seedId) return;

                if(!seed.IsBought) _currentSeed = seed;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out PlantSeed seed) && seed == _currentSeed)
            {
                if (!seed.IsBought && seed.InShop)
                {
                    // Runs on every client; the deduction is optimistic (#39). A client that
                    // has not yet received the grabber RPC has nobody to charge — skip the
                    // local deduction and let the master's next broadcast reconcile it. On the
                    // master itself this means the purchase goes uncharged, so log louder.
                    PlayerBalance buyer = seed.GetGrabber();

                    _currentSeed.Purchase();

                    if (buyer != null)
                    {
                        EconomyManager.Instance.RemoveBalance(buyer.GetID(), _seedDefinition.buyPrice);
                    }
                    else if (SceneNetworking.IsMasterClient)
                    {
                        Logger.Error($"OnTriggerExit() '{gameObject.name}' — seed '{seed.name}' left the shop with no known grabber on the master; purchase NOT charged");
                    }
                    else
                    {
                        Logger.Warn($"OnTriggerExit() '{gameObject.name}' — seed '{seed.name}' has no known grabber yet, skipping optimistic local deduction");
                    }

                    _currentSeed = null;
                    if (SceneNetworking.IsMasterClient)
                        SpawnSeed();
                }
            }
        }
    }
}
