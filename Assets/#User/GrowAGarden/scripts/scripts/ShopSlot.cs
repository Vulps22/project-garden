using Fusion;
using SomniumSpace.Network.Bridge;
using System.Collections;
using UnityEngine;

namespace GrowAGarden
{
    public class ShopSlot : MonoBehaviour
    {
        [SerializeField] private SeedDefinition _seedDefinition;

        [Tooltip("The seed this slot sells. Must also be listed on the SceneNetworking " +
                 "component, or Fusion has no id to spawn it by.")]
        [SerializeField] private NetworkObject _seedPrefab;

        private Seed _currentSeed;
        private Collider[] _propColliders;
        private Collider _slotTrigger;

        [Tooltip("How long the shop waits to be given ownership of a seed before giving up on " +
                 "bringing it home. A safety net, not a normal path.")]
        [SerializeField] private float _authorityTimeout = 2f;

        /// <summary>How long to wait before trying to stock an empty slot again.</summary>
        private const float RESTOCK_RETRY_SECONDS = 0.5f;
        private float _lastStockAttempt = float.NegativeInfinity;

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

            // A seed that is bought, planted or otherwise gone is despawned, so a Unity-null
            // _currentSeed is now the only way a slot is empty. There is no second state to test
            // for: a seed is a seed for its whole life.
            if (_currentSeed != null) return;

            // Restocking is a retry, not a per-frame job. Spawning is the one thing here that
            // can fail for reasons outside this slot — the session not being ready, the prefab
            // table not built — and at 90 Hz a failing retry becomes hundreds of attempts and,
            // if Fusion throws rather than refusing, hundreds of orphans.
            if (Time.time - _lastStockAttempt < RESTOCK_RETRY_SECONDS) return;
            _lastStockAttempt = Time.time;

            SetCurrentSeed(null);
            SpawnSeed();
        }

        private void SpawnSeed()
        {

            if (!SceneNetworking.IsMasterClient)
            {
                return;
            }

            if (!CanSpawn())
            {
                return;
            }

            Seed claimed = SpawnFreshSeed();
            if (claimed == null)
            {
                Logger.Warn($"SpawnSeed() '{gameObject.name}' — no seed available for '{_seedDefinition.seedId}'; slot left empty, will retry");
                return;
            }

            Logger.Info($"SpawnSeed() '{gameObject.name}' — stocked '{claimed.name}' authority={claimed.HasLocalAuthority} at {transform.position}");

            SetCurrentSeed(claimed);
            claimed.PlaceInShop(transform.position, transform.rotation);
            AssignSlot(claimed);
            StartCoroutine(AnnounceStock(claimed));
        }

        /// <summary>
        /// Whether Fusion can actually create an object right now.
        ///
        /// Asks the runner, not our own bookkeeping. IsSharedModeMasterClient goes true as soon
        /// as the peer is in a room, and SceneNetworking.IsNetworkReady is a static that outlives
        /// the scene it describes — during a world transition the old value is still standing
        /// while the new scene's Update loops are already running. Either one alone said "go"
        /// while Fusion's simulation had no player index yet, and Runner.Spawn() in that window
        /// instantiates the prefab and *then* throws out of Simulation.GetNextId(), leaving an
        /// orphaned GameObject that is never networked and never told what it is. Those orphans
        /// are what put two seeds in one slot.
        ///
        /// LocalPlayer.IsRealPlayer is the question that actually matters — it is false until
        /// this peer has a player index, which is precisely what GetNextId() needs.
        /// </summary>
        private bool CanSpawn()
        {
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            return runner != null
                && runner.IsRunning
                && runner.LocalPlayer.IsRealPlayer
                && SceneNetworking.IsNetworkReady;
        }

        /// <summary>
        /// Makes a new seed rather than reusing one.
        ///
        /// SharedModeStateAuthMasterClient is the point of the exercise: the master owns the
        /// stock from the instant it exists, so the client that decides what stock is is also
        /// the client that can broadcast it. Under pooling those were routinely different
        /// machines, and broadcastState() self-gates on authority, so the master's decisions
        /// silently went nowhere. The flag only works from the master, which SpawnSeed() has
        /// already established.
        ///
        /// Returns null rather than throwing when the prefab table has not been built yet —
        /// registration happens at runner setup, and a slot can reach here first. Update() tries
        /// again shortly afterwards.
        /// </summary>
        private Seed SpawnFreshSeed()
        {
            SceneNetworking net = SceneNetworking.Instance;
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (net == null || runner == null) return null;

            if (!net.NetworkPrefabs.TryGetValue(_seedPrefab, out NetworkPrefabId prefabId))
            {
                Logger.Warn($"SpawnFreshSeed() '{gameObject.name}' — '{_seedPrefab.name}' is not registered on SceneNetworking yet");
                return null;
            }

            NetworkObject spawned;
            try
            {
                spawned = runner.Spawn(prefabId, transform.position, transform.rotation,
                                       null, null,
                                       NetworkSpawnFlags.SharedModeStateAuthMasterClient);
            }
            catch (System.Exception e)
            {
                // Fusion instantiates the prefab before it allocates an id, so a throw in here
                // has already left a GameObject in the scene that will never be networked. There
                // is no handle to clean it up with — the only real defence is CanSpawn() above,
                // and the retry interval that stops a bad frame becoming a hundred of them.
                //
                // Caught rather than left to propagate because ExceptionAlarm blacks the world
                // out on any exception from this assembly, and a shop that cannot restock is not
                // worth making the garden unplayable for. The error still says so, loudly.
                Logger.Error($"SpawnFreshSeed() '{gameObject.name}' — Fusion threw spawning '{_seedPrefab.name}': {e.Message}");
                return null;
            }

            if (spawned == null)
            {
                Logger.Error($"SpawnFreshSeed() '{gameObject.name}' — Fusion refused to spawn '{_seedPrefab.name}'");
                return null;
            }

            Seed seed = spawned.GetComponent<Seed>();
            if (seed == null)
                Logger.Error($"SpawnFreshSeed() '{gameObject.name}' — spawned '{spawned.name}' has no Seed component");

            return seed;
        }

        /// <summary>
        /// Says what fresh stock is, more than once.
        ///
        /// A spawned object exists on the spawner immediately but reaches everyone else a few
        /// ticks later, so the RestoreToShop() that follows a spawn can be about an object the
        /// other clients do not have yet, and an RPC with nowhere to land is simply dropped.
        /// Pooling never had this problem: every seed existed on every client from scene load,
        /// so no message about one could outrun the object it described.
        ///
        /// Repeating the state broadcast is the crude fix, and deliberately so — this is the
        /// one thing about runtime spawn that cannot be settled by reading the SDK, so phase A
        /// exists partly to find out whether it was needed at all. If the logs show proxies
        /// were already in step, delete the loop and keep a single broadcast.
        /// </summary>
        private IEnumerator AnnounceStock(Seed seed)
        {
            // Gaps, not timestamps — these land at 0.25 s, 1 s and 2 s after the spawn.
            float[] gaps = { 0.25f, 0.75f, 1f };
            foreach (float gap in gaps)
            {
                yield return new WaitForSeconds(gap);

                // Only while it is still ours and still stock — a seed bought in the meantime
                // has moved on, and re-asserting shop state over it is exactly the bug the
                // purchase-authority work removed.
                if (seed == null || seed != _currentSeed || !seed.InShop) yield break;

                seed.broadcastState();
            }
        }

        /// <summary>
        /// Points this slot at a seed, or at nothing.
        ///
        /// The purchase request arrives on the seed, so the master has to be listening to
        /// whichever seed is currently its stock — and must stop listening when it is not, or a
        /// sold seed could be bought a second time from the slot it no longer occupies.
        /// </summary>
        private void SetCurrentSeed(Seed seed)
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

            // ReleaseSlot rather than just un-ignoring collisions: it also clears the recall and
            // realign targets, which used to happen only on the successful-purchase path. Now
            // that InShop is replicated on every client (Purchase() is announced by RPC), every
            // client can drop its own claim off the same fact.
            ReleaseSlot(_currentSeed);
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
        private void AssignSlot(Seed seed)
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
        private void ReleaseSlot(Seed seed)
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
        /// disabled and re-enabled, which UpdateVisuals does on every state change.
        /// </summary>
        private void IgnorePropCollisions(Seed seed, bool ignore)
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
                if (c.GetComponentInParent<Seed>() != null) continue;  // a seed, not the prop
                found.Add(c);
            }
            _propColliders = found.ToArray();
        }

        /// <summary>
        /// Puts a seed back where it belongs without charging anyone. Used for every exit that
        /// is not a genuine purchase; the slot keeps its seed, so nothing restocks.
        /// </summary>
        private void ReturnToSlot(Seed seed, string why)
        {
            Logger.Warn($"ReturnToSlot() '{gameObject.name}' — seed '{seed.name}' {why}; not a sale");

            // No ForceRelease() here: RestoreToShop() now cancels the grab on every client, which
            // is the only way to get it out of a remote player's hand. Releasing it locally first
            // also stopped ReturnableEntity recording that it was the one that suspended the
            // grab, so nothing ever re-enabled it and the seed became permanently untouchable.
            seed.RestoreToShop();      // shop state, but leave it where it stands
            SendHome(seed);
            SetCurrentSeed(seed);
        }

        /// <summary>
        /// Brings the seed home — but takes ownership of it first.
        ///
        /// Recall is a local operation that moves a rigidbody, and a client without state
        /// authority cannot move a networked rigidbody: ReturnableEntity.FixedUpdate bails at
        /// `if (!hasAuthority) return;`. So the master used to suspend the seed's collisions and
        /// grab on its own copy, wait for a trip that could never start, and time out after 20
        /// seconds — during which its copy was untouchable while the buyer carried the real one
        /// away. The shop has to own the thing it is putting back on the shelf.
        /// </summary>
        private void SendHome(Seed seed)
        {
            var ret = seed.GetComponent<ReturnableEntity>();
            if (ret == null) return;
            ret.SetReturnTarget(transform);
            StartCoroutine(TakeOwnershipAndRecall(seed, ret));
        }

        private IEnumerator TakeOwnershipAndRecall(Seed seed, ReturnableEntity ret)
        {
            NetworkObject obj = seed.networkBridge == null ? null : seed.networkBridge.Object;
            if (obj == null) yield break;

            if (!obj.HasStateAuthority)
            {
                obj.RequestStateAuthority();
                float waited = 0f;
                while (!obj.HasStateAuthority && waited < _authorityTimeout)
                {
                    yield return null;
                    waited += Time.deltaTime;
                }
            }

            if (!obj.HasStateAuthority)
            {
                Logger.Warn($"TakeOwnershipAndRecall() '{gameObject.name}' — could not take ownership of '{seed.name}' within {_authorityTimeout}s; leaving it where it is");
                yield break;
            }

            ret.Recall(shouldIgnoreCollisions: true);

            var align = seed.GetComponent<AlignableEntity>();
            if (align != null) align.Realign();
        }

        /// <summary>
        /// Refuses a purchase the player cannot afford: drops it out of their hand and puts it
        /// back in the slot. The seed stays this slot's current seed, so no restock happens.
        /// </summary>
        private void RejectPurchase(Seed seed, PlayerBalance buyer)
        {
            Logger.Info($"RejectPurchase() '{gameObject.name}' — '{buyer.GetID()}' cannot afford {_seedDefinition.seedId} ({buyer.GetBalance()} < {_seedDefinition.buyPrice})");

            seed.RestoreToShop();      // shop state, but leave it where it stands
            SendHome(seed);
            SetCurrentSeed(seed);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out Seed seed))
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
        private bool HasLeftSlot(Seed seed)
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
            if (!other.TryGetComponent(out Seed seed) || seed != _currentSeed) return;

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

            // An empty HolderId is not proof that nobody took it — only that this client has not
            // been told yet. The grabber RPC is tick-aligned and crosses the same distance as the
            // player, so on a transatlantic connection the master watches the seed leave the slot
            // a good fraction of a second before it hears who is carrying it, and called all 19
            // purchases in one playtest a knock-out.
            //
            // Ownership is the reliable tell, because grabbing takes state authority immediately
            // and locally: if this client still owns the seed, nobody has picked it up. If a peer
            // owns it, somebody has, and their request is on its way.
            if (!seed.HasLocalAuthority) return;

            ReturnToSlot(seed, "was knocked out of the slot, not taken");
        }

        /// <summary>
        /// The holder has asked to buy this seed. Master client only — it owns the economy, and
        /// deciding this in more than one place is what let a purchase commit on the buyer's
        /// machine while the seed was sent home on everyone else's.
        /// </summary>
        private void OnPurchaseRequested(Seed seed)
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
            seed.Purchase(buyer.GetID());
            EconomyManager.Instance.RemoveBalance(buyer.GetID(), _seedDefinition.buyPrice);

            SetCurrentSeed(null);
            SpawnSeed();
        }
    }
}
