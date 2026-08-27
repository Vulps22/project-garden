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
            Tether(_currentSeed);
        }

        /// <summary>Points the seed's tether at this slot, so a bumped seed drifts back.</summary>
        private void Tether(PlantSeed seed)
        {
            var tether = seed.GetComponent<SlotTether>();
            if (tether != null) tether.Attach(transform);
        }

        /// <summary>Releases the seed from this slot once it belongs to a player.</summary>
        private void Untether(PlantSeed seed)
        {
            var tether = seed.GetComponent<SlotTether>();
            if (tether != null) tether.Detach();
        }

        /// <summary>
        /// Refuses a purchase the player cannot afford: drops it out of their hand and puts it
        /// back in the slot. The seed stays this slot's current seed, so no restock happens.
        /// </summary>
        private void RejectPurchase(PlantSeed seed, PlayerBalance buyer)
        {
            Logger.Info($"RejectPurchase() '{gameObject.name}' — '{buyer.GetID()}' cannot afford {_seedDefinition.seedId} ({buyer.GetBalance()} < {_seedDefinition.buyPrice})");

            seed.ForceRelease();
            // PlaceInShop re-enables the grab and restores position, rotation and shop flags.
            seed.PlaceInShop(transform.position, transform.rotation);
            Tether(seed);
            _currentSeed = seed;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out PlantSeed seed) && seed.IsSeed)
            {
                // Only latch seeds this slot actually sells — a seed of another type carried
                // through the trigger must not become this slot's _currentSeed.
                if (seed.seedDefinition == null || seed.seedDefinition.seedId != _seedDefinition.seedId) return;

                if (!seed.IsBought)
                {
                    _currentSeed = seed;
                    Tether(seed);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out PlantSeed seed) && seed == _currentSeed)
            {
                if (!seed.IsBought && seed.InShop)
                {
                    // Runs on every client. The buyer's balance comes from the replicated
                    // table, so every client reaches the same verdict without a round trip --
                    // the seed reacts to the hand immediately rather than travelling for
                    // ~100ms and then snapping back.
                    PlayerBalance buyer = seed.GetGrabber();

                    if (buyer == null)
                    {
                        // No known grabber yet: the grabber RPC has not landed here. Let it
                        // leave rather than rejecting a purchase that may well be affordable;
                        // the master's next broadcast reconciles.
                        if (SceneNetworking.IsMasterClient)
                            Logger.Error($"OnTriggerExit() '{gameObject.name}' — seed '{seed.name}' left the shop with no known grabber on the master; purchase NOT charged");
                        else
                            Logger.Warn($"OnTriggerExit() '{gameObject.name}' — seed '{seed.name}' has no known grabber yet, skipping optimistic local deduction");

                        Untether(_currentSeed);
                        _currentSeed.Purchase();
                    }
                    else if (buyer.GetBalance() >= _seedDefinition.buyPrice)
                    {
                        Untether(_currentSeed);
                        _currentSeed.Purchase();
                        EconomyManager.Instance.RemoveBalance(buyer.GetID(), _seedDefinition.buyPrice);
                    }
                    else
                    {
                        RejectPurchase(seed, buyer);
                        return;   // slot keeps its seed; nothing to restock
                    }

                    _currentSeed = null;
                    if (SceneNetworking.IsMasterClient)
                        SpawnSeed();
                }
            }
        }
    }
}
