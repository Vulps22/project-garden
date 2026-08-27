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
        /// Puts a seed back where it belongs without charging anyone. Used for every exit that
        /// is not a genuine purchase; the slot keeps its seed, so nothing restocks.
        /// </summary>
        private void ReturnToSlot(PlantSeed seed, string why)
        {
            Logger.Warn($"ReturnToSlot() '{gameObject.name}' — seed '{seed.name}' {why}; not a sale");

            seed.ForceRelease();
            seed.PlaceInShop(transform.position, transform.rotation);
            Tether(seed);
            _currentSeed = seed;
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
                    // A seed leaving the slot is only a purchase if someone is actually
                    // carrying it away. Stock is physical now, so it can be shoved out by a
                    // hand, an elbow or another seed -- none of which is a sale. Treating an
                    // unheld exit as a purchase handed out free seeds and restocked the slot,
                    // which is a straightforward way to print unlimited crops.
                    PlayerBalance buyer = seed.GetGrabber();

                    if (!seed.IsHeld)
                    {
                        ReturnToSlot(seed, "was knocked out of the slot, not taken");
                        return;
                    }

                    if (buyer == null)
                    {
                        // Held, but we do not yet know by whom -- the grabber RPC has not
                        // landed here. Refuse rather than guess: an uncharged sale cannot be
                        // undone, whereas the player can simply pick it up again.
                        ReturnToSlot(seed, "has no known grabber yet");
                        return;
                    }

                    if (!seed.HasKnownAuthority)
                    {
                        // Nobody owns the object, so Purchase()'s broadcast would reach no one
                        // and the sale would exist only on this client.
                        ReturnToSlot(seed, "has no state authority");
                        return;
                    }

                    if (buyer.GetBalance() < _seedDefinition.buyPrice)
                    {
                        RejectPurchase(seed, buyer);
                        return;
                    }

                    Untether(_currentSeed);
                    _currentSeed.Purchase();
                    EconomyManager.Instance.RemoveBalance(buyer.GetID(), _seedDefinition.buyPrice);

                    _currentSeed = null;
                    if (SceneNetworking.IsMasterClient)
                        SpawnSeed();
                }
            }
        }
    }
}
