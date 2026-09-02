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
        private Collider[] _propColliders;
        private Collider _slotTrigger;

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
            SetCurrentSeed(null);   // drops the PurchaseRequested subscription
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
                SetCurrentSeed(null);
                SpawnSeed();
            }
        }

        private void SpawnSeed()
        {

            if (!SceneNetworking.IsMasterClient)
            {
                return;
            }

            PlantSeed claimed = PoolManager.Instance.ClaimPlantSeed(_seedDefinition.seedId);
            if (claimed == null)
            {
                return;
            }

            SetCurrentSeed(claimed);
            claimed.PlaceInShop(transform.position, transform.rotation);
            AssignSlot(claimed);
        }

        /// <summary>
        /// Points this slot at a seed, or at nothing.
        ///
        /// The purchase request arrives on the seed, so the master has to be listening to
        /// whichever seed is currently its stock — and must stop listening when it is not, or a
        /// sold seed could be bought a second time from the slot it no longer occupies.
        /// </summary>
        private void SetCurrentSeed(PlantSeed seed)
        {
            if (_currentSeed == seed) return;

            if (_currentSeed != null)
            {
                _currentSeed.PurchaseRequested -= OnPurchaseRequested;
                _currentSeed.LifecycleChanged -= OnCurrentSeedLifecycleChanged;
            }

            _currentSeed = seed;

            if (_currentSeed != null)
            {
                _currentSeed.PurchaseRequested += OnPurchaseRequested;
                _currentSeed.LifecycleChanged += OnCurrentSeedLifecycleChanged;
            }
        }

        /// <summary>
        /// Lets go of a seed that has stopped being stock, on every client.
        ///
        /// Driven by lifecycle rather than by the purchase path because the purchase is decided
        /// on the master alone, while ignoring prop collisions is a local physics setting that
        /// every client applied for itself and so every client has to undo. InShop is replicated,
        /// so this fires everywhere off the same fact instead of each client guessing.
        /// </summary>
        private void OnCurrentSeedLifecycleChanged()
        {
            if (_currentSeed == null || _currentSeed.InShop) return;

            IgnorePropCollisions(_currentSeed, false);
            SetCurrentSeed(null);
        }

        /// <summary>
        /// Tells the seed this slot is where it belongs.
        ///
        /// Split by ownership. Recall and realign are bookkeeping about where a seed *should*
        /// be, so the master arms them and the master alone — armed on every client, each one
        /// independently decides a seed has wandered and only the clearing half is authority
        /// gated, which is how a bought seed ends up being dragged back to the shop.
        ///
        /// Ignoring prop collisions is not bookkeeping. Physics.IgnoreCollision is a local
        /// setting on every client, so it has to run on all of them or the stock bounces around
        /// inside the barrow on everyone else's screen.
        /// </summary>
        private void AssignSlot(PlantSeed seed)
        {
            IgnorePropCollisions(seed, true);

            if (!SceneNetworking.IsMasterClient) return;

            var ret = seed.GetComponent<ReturnableEntity>();
            if (ret != null)
            {
                ret.SetReturnTarget(transform);
                ret.SetAutoRecall(true);   // a nudge tidies itself up
            }

            // PlaceInShop has already set the seed to the socket's rotation, so whatever it is
            // facing now is correct — the slot never has to name a rotation.
            var align = seed.GetComponent<AlignableEntity>();
            if (align != null)
            {
                align.CaptureAlignTarget();
                align.SetAutoRealign(true);
            }
        }

        /// <summary>The seed belongs to a player now; it is no longer this slot's business.</summary>
        private void ReleaseSlot(PlantSeed seed)
        {
            IgnorePropCollisions(seed, false);

            if (!SceneNetworking.IsMasterClient) return;

            var ret = seed.GetComponent<ReturnableEntity>();
            if (ret != null)
            {
                ret.SetAutoRecall(false);
                ret.ClearReturnTarget();
            }

            var align = seed.GetComponent<AlignableEntity>();
            if (align != null)
            {
                align.SetAutoRealign(false);
                align.ClearAlignTarget();
            }
        }

        /// <summary>
        /// Lets stock and the prop it sits in pass through each other.
        ///
        /// The slot anchor is inside the prop's mesh -- the barrow collider spans roughly
        /// y=0.4 to 1.8 and the anchor sits at 1.16 -- so the seed hangs in a bowl. Once stock
        /// became physical, any nudge bounced it off the inside walls. The tether is anchored to
        /// this slot's transform rather than to the prop, so removing the collision between them
        /// changes nothing about where the seed is held.
        ///
        /// Done per collider pair rather than through layers: the physics collision matrix
        /// lives in ProjectSettings, which does not travel inside an exported asset bundle, so
        /// a layer-based rule would work in the Editor and quietly do nothing in-world.
        ///
        /// Re-applied on every AssignSlot() because Unity drops ignored pairs when a collider is
        /// disabled and re-enabled, which is exactly what pooling does.
        /// </summary>
        private void IgnorePropCollisions(PlantSeed seed, bool ignore)
        {
            if (_propColliders == null) CachePropColliders();
            if (_propColliders.Length == 0 || seed == null) return;

            foreach (Collider seedCol in seed.GetComponentsInChildren<Collider>(true))
            {
                if (seedCol.isTrigger) continue;   // triggers do not collide anyway
                foreach (Collider propCol in _propColliders)
                {
                    if (propCol == null) continue;
                    Physics.IgnoreCollision(seedCol, propCol, ignore);
                }
            }
        }

        private void CachePropColliders()
        {
            Transform root = transform.parent != null ? transform.parent : transform;
            var found = new System.Collections.Generic.List<Collider>();
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;                                  // our own slot trigger
                if (c.GetComponentInParent<PlantSeed>() != null) continue;  // a seed, not the prop
                found.Add(c);
            }
            _propColliders = found.ToArray();
        }

        /// <summary>
        /// Puts a seed back where it belongs without charging anyone. Used for every exit that
        /// is not a genuine purchase; the slot keeps its seed, so nothing restocks.
        /// </summary>
        private void ReturnToSlot(PlantSeed seed, string why)
        {
            Logger.Warn($"ReturnToSlot() '{gameObject.name}' — seed '{seed.name}' {why}; not a sale");

            seed.ForceRelease();
            seed.RestoreToShop();      // shop state, but leave it where it stands
            SendHome(seed);
            SetCurrentSeed(seed);
        }

        /// <summary>Asks the seed to come back to this slot.</summary>
        private void SendHome(PlantSeed seed)
        {
            var ret = seed.GetComponent<ReturnableEntity>();
            if (ret == null) return;
            ret.SetReturnTarget(transform);
            ret.Recall(shouldIgnoreCollisions: true);

            var align = seed.GetComponent<AlignableEntity>();
            if (align != null) align.Realign();
        }

        /// <summary>
        /// Refuses a purchase the player cannot afford: drops it out of their hand and puts it
        /// back in the slot. The seed stays this slot's current seed, so no restock happens.
        /// </summary>
        private void RejectPurchase(PlantSeed seed, PlayerBalance buyer)
        {
            Logger.Info($"RejectPurchase() '{gameObject.name}' — '{buyer.GetID()}' cannot afford {_seedDefinition.seedId} ({buyer.GetBalance()} < {_seedDefinition.buyPrice})");

            seed.ForceRelease();
            seed.RestoreToShop();      // shop state, but leave it where it stands
            SendHome(seed);
            SetCurrentSeed(seed);
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
                    SetCurrentSeed(seed);
                    AssignSlot(seed);
                }
            }
        }

        /// <summary>
        /// Whether the seed has genuinely left this slot's volume.
        ///
        /// OnTriggerExit cannot be trusted on its own. Unity re-creates the physics actor when
        /// isKinematic changes, and fires trigger exits for everything it was overlapping even
        /// though nothing moved. Three things toggle that here: XRGrabInteractable sets
        /// isKinematic on grab (movement type is Instantaneous), KinematicController sets it
        /// from the lifecycle, and ReturnableEntity toggles detectCollisions during an enforced
        /// return. UpdateVisuals swapping colliders does the same.
        ///
        /// Grabbing a seed therefore raised an exit while it sat motionless in the slot, and the
        /// purchase path charged for it and restocked. Check where the seed actually is instead.
        /// </summary>
        private bool HasLeftSlot(PlantSeed seed)
        {
            if (_slotTrigger == null) _slotTrigger = GetComponent<Collider>();
            if (_slotTrigger == null) return true;   // no volume to test against; trust the event
            return !_slotTrigger.bounds.Contains(seed.transform.position);
        }

        /// <summary>
        /// A seed has left the slot. Two clients care, for two different reasons.
        ///
        /// The holder asks to buy it, because only the holder knows its own hand is on the seed
        /// — XRGrabInteractable.isSelected is true on exactly one machine, and reading it
        /// anywhere else is what made every other client believe the seed had been knocked
        /// loose. Here it is read on the one machine where it means something.
        ///
        /// The master handles everything that is not a purchase, using the replicated holder id
        /// rather than the local interactable, so it agrees with the holder about what happened.
        /// </summary>
        private void OnTriggerExit(Collider other)
        {
            if (!other.TryGetComponent(out PlantSeed seed) || seed != _currentSeed) return;

            // Still inside: the event came from a physics-state toggle, not from movement.
            if (!HasLeftSlot(seed)) return;
            if (seed.IsBought || !seed.InShop) return;

            if (seed.IsHeld)
            {
                seed.RequestPurchase();
                return;
            }

            // Nobody anywhere is holding it, so it was shoved out by a hand, an elbow or
            // another seed. Stock is physical, and treating that as a sale handed out free
            // seeds and restocked the slot, which is a straightforward way to print crops.
            if (!SceneNetworking.IsMasterClient) return;
            if (!string.IsNullOrEmpty(seed.HolderId)) return;   // someone has it; wait for their request

            ReturnToSlot(seed, "was knocked out of the slot, not taken");
        }

        /// <summary>
        /// The holder has asked to buy this seed. Master client only — it owns the economy, and
        /// deciding this in more than one place is what let a purchase commit on the buyer's
        /// machine while the seed was sent home on everyone else's.
        /// </summary>
        private void OnPurchaseRequested(PlantSeed seed)
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (seed != _currentSeed) return;
            if (seed.IsBought || !seed.InShop) return;

            PlayerBalance buyer = seed.GetGrabber();

            if (buyer == null)
            {
                // The request came from the holder, so somebody has it — but this client has
                // not been told who. Refuse rather than guess: an uncharged sale cannot be
                // undone, whereas the player can simply pick it up again.
                ReturnToSlot(seed, "was requested by a buyer this client does not know yet");
                return;
            }

            if (!seed.HasKnownAuthority)
            {
                // Nobody owns the object, so Purchase()'s broadcast would reach no one and the
                // sale would exist only on this client.
                ReturnToSlot(seed, "has no state authority");
                return;
            }

            if (buyer.GetBalance() < _seedDefinition.buyPrice)
            {
                RejectPurchase(seed, buyer);
                return;
            }

            ReleaseSlot(seed);
            seed.Purchase();
            EconomyManager.Instance.RemoveBalance(buyer.GetID(), _seedDefinition.buyPrice);

            SetCurrentSeed(null);
            SpawnSeed();
        }
    }
}
