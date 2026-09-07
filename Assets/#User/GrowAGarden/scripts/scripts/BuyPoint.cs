using Fusion;
using System.Collections;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// A shop stall. Decides what is sold, what it costs, and whether a sale happened. The Socket
    /// beneath it holds the stock and knows none of that.
    ///
    /// This was ShopSlot, which did six jobs at once — spawning, holding, arming recall, ignoring
    /// prop collisions, judging geometry and deciding purchases — in the same way PlantSeed used
    /// to be a seed, a crop and a fruit by turns. The half that is about *a fixture holding a
    /// thing* is now Socket and is reusable; what is left here is the half that is about money.
    ///
    /// It was also three prefabs — BuyPointCarrot, BuyPointTurnip, BuyPointPumpkin — differing
    /// only in which crop they pointed at, each carrying its own copy of that crop's definition.
    /// One prefab now, and the crop is a roll rather than a serialized fact.
    /// </summary>
    public class BuyPoint : MonoBehaviour
    {
        [Tooltip("The socket this stall stocks. It holds the goods; this component decides what " +
                 "they are and what they cost.")]
        [SerializeField] private Socket _socket;

        [Tooltip("What this stall is currently selling. A starting offer only — a stock cycle " +
                 "replaces it, unless an override below pins it.")]
        [SerializeField] private SeedDefinition _seedDefinition;

        [Tooltip("Pin this stall to one crop. A stall with an override stocks it once and then " +
                 "never cycles, so the shop can always be relied on to sell it. Leave empty for " +
                 "a stall that rolls with the rest of the world.")]
        [SerializeField] private SeedDefinition _override;

        [Tooltip("How much this stall favours rare stock. 1 leaves the world's weights alone; " +
                 "higher makes uncommon things more likely, which is what turns a garden stall " +
                 "into an exploration stall without needing a second table.")]
        [SerializeField] private float _rarityMultiplier = 1f;

        [Tooltip("The sign. Optional — a stall with no sign simply does not show its prices.")]
        [SerializeField] private StallDisplay _display;

        private Seed _currentSeed;
        private bool _pinned;

        /// <summary>
        /// Restocking is a retry, not a per-frame job.
        ///
        /// Events drive the happy path — the socket says it is empty and this stall fills it —
        /// but a spawn can fail for reasons outside this stall: the session not ready, the prefab
        /// table not built. There is no second event to wait for when that happens, so the want
        /// is remembered and retried. Rate-limited because at 90 Hz a failing per-frame spawn is
        /// hundreds of attempts and, when Fusion throws rather than refuses, hundreds of orphans.
        /// </summary>
        private const float RESTOCK_RETRY_SECONDS = 0.5f;
        private bool _wantsStock;
        private float _lastStockAttempt = float.NegativeInfinity;

        private void OnEnable()
        {
            WorldManager.StockRecycled += OnStockRecycled;

            // A stall enabled after the world's first cycle — a late-spawned island, a disabled
            // stall switched on — would otherwise stand empty until the next cycle minutes later.
            // Asking for stock is free: Update() only acts on the master and only when the socket
            // is empty, and SpawnStock needs a definition it will not have unless one was rolled.
            _wantsStock = true;
            if (_socket == null) return;
            _socket.Entered += OnEntered;
            _socket.Leaving += OnLeaving;
            _socket.Left    += OnLeft;
        }

        private void OnDisable()
        {
            WorldManager.StockRecycled -= OnStockRecycled;
            if (_socket == null) return;
            _socket.Entered -= OnEntered;
            _socket.Leaving -= OnLeaving;
            _socket.Left    -= OnLeft;
        }

        private void OnDestroy() => SetCurrentSeed(null);   // drops the subscriptions

        private void Update()
        {
            if (!_wantsStock) return;
            if (!SceneNetworking.IsMasterClient) return;
            if (_currentSeed != null) { _wantsStock = false; return; }

            if (Time.time - _lastStockAttempt < RESTOCK_RETRY_SECONDS) return;
            _lastStockAttempt = Time.time;
            SpawnStock();
        }

        // ── What is sold ──────────────────────────────────────────────────────────

        /// <summary>
        /// Rolls this stall's next crop. Master only — Offer() refuses on anyone else anyway, but
        /// rolling everywhere would burn a different random number on each client and read as if
        /// the decision were shared when it is not.
        /// </summary>
        private void OnStockRecycled()
        {
            if (!SceneNetworking.IsMasterClient) return;

            if (_socket == null)
            {
                Logger.Error($"OnStockRecycled() '{gameObject.name}' — no Socket assigned; stall will never stock");
                return;
            }

            // A pinned stall takes the world's first cycle to stock itself and ignores every one
            // after it. Without at least one of these, three stalls can roll pumpkins at once and
            // a player who has just arrived with ten thatch has nothing in the world they can
            // afford — a dead start lasting until the next cycle.
            if (_override != null)
            {
                if (_pinned) return;
                _pinned = true;
                Logger.Info($"OnStockRecycled() '{gameObject.name}' — pinned to '{_override.seedId}'; will not cycle");
                Offer(_override);
                return;
            }

            SeedDefinition rolled = Roll();
            if (rolled == null)
            {
                Logger.Warn($"OnStockRecycled() '{gameObject.name}' — nothing to roll from; leaving stock as it is");
                return;
            }

            Offer(rolled);
        }

        /// <summary>
        /// Weighted pick from the world's catalogue.
        ///
        /// UnityEngine.Random is fine here, unlike in anything derived: this is a master decision
        /// and the spawned seed is its announcement, so no other client needs to reach the same
        /// answer. The procedural-island seed is the opposite case and must not use it.
        /// </summary>
        private SeedDefinition Roll()
        {
            WorldManager world = WorldManager.Instance;
            if (world == null || world.Buyables == null || world.Buyables.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < world.Buyables.Count; i++) total += WeightOf(world.Buyables[i]);
            if (total <= 0f) return null;

            float pick = Random.Range(0f, total);
            for (int i = 0; i < world.Buyables.Count; i++)
            {
                pick -= WeightOf(world.Buyables[i]);
                if (pick <= 0f) return world.Buyables[i];
            }

            // Floating point can leave a sliver unclaimed; the last valid entry is the answer.
            for (int i = world.Buyables.Count - 1; i >= 0; i--)
                if (WeightOf(world.Buyables[i]) > 0f) return world.Buyables[i];

            return null;
        }

        /// <summary>
        /// A crop's chance at this stall. The multiplier lifts anything the world considers
        /// uncommon — a weight below the staple's 1 — and leaves common stock alone, so one
        /// number turns a garden stall into a void stall without a second catalogue.
        /// </summary>
        private float WeightOf(SeedDefinition definition)
        {
            if (definition == null || definition.seedPrefab == null) return 0f;
            float weight = definition.spawnWeight;
            if (weight <= 0f) return 0f;
            return weight < 1f ? weight * _rarityMultiplier : weight;
        }

        /// <summary>Changes what this stall sells, clearing whatever is on the shelf first.</summary>
        public void Offer(SeedDefinition definition)
        {
            if (!SceneNetworking.IsMasterClient) return;

            if (definition == null || definition.seedPrefab == null)
            {
                Logger.Warn($"Offer() '{gameObject.name}' — refused an empty definition; keeping '{(_seedDefinition == null ? "<none>" : _seedDefinition.seedId)}'");
                return;
            }

            Logger.Info($"Offer() '{gameObject.name}' — now selling '{definition.seedId}' at {definition.buyPrice}");

            if (_currentSeed == null)
            {
                _seedDefinition = definition;
                _wantsStock = true;
                return;
            }

            StartCoroutine(ReplaceStock(definition));
        }

        /// <summary>
        /// Takes the old stock off the shelf, taking control of it by force on the way.
        ///
        /// **The cycle always completes.** There is no timeout and no path that gives up, because
        /// every reason a client might hold this object is a reason it does not get to keep it.
        /// A player cannot veto a cycle: holding is not owning — HolderId says who has it, OwnerId
        /// says whose it is, and only the second is a claim — so a player with no thatch must not
        /// be able to squat on the rarest thing in the shop until the timer flips. Authority is
        /// not a claim either; it transfers on *hover*, so someone who merely waved a hand near
        /// the barrow would otherwise hold the world's whole cycle up.
        ///
        /// So the master asks, and keeps asking, until it has authority — then despawns. Discard()
        /// is silent without authority, since Fusion's Despawn simply does nothing, which is the
        /// same failure GardenLease was counting as a success before it started reporting.
        ///
        /// The only exits are the seed being destroyed under us or bought while we waited, and
        /// both mean the shelf is already clear.
        /// </summary>
        private IEnumerator ReplaceStock(SeedDefinition definition)
        {
            Seed outgoing = _currentSeed;

            // Out of reach immediately, so nobody is carrying a seed that is on its way out.
            // ForceRelease only disables the interactable on *this* client, so every path out of
            // here has to put it back. A seed that survives the cycle — bought while we waited —
            // would otherwise be permanently ungrabbable on the master's machine, which is the
            // same untouchable-seed bug ReturnToSocket's comment records.
            outgoing.ForceRelease();
            _seedDefinition = definition;

            NetworkObject obj = outgoing.networkBridge == null ? null : outgoing.networkBridge.Object;

            while (obj != null && !obj.HasStateAuthority)
            {
                obj.RequestStateAuthority();

                // Re-asked rather than asked once: a request can be lost, and the client holding
                // it may leave, in which case the next ask is the one that lands.
                yield return new WaitForSeconds(0.25f);

                if (Abandoned(outgoing)) yield break;   // gone or sold
                obj = outgoing.networkBridge == null ? null : outgoing.networkBridge.Object;
            }

            if (Abandoned(outgoing)) yield break;

            if (!outgoing.Discard())
            {
                // Authority was held and the despawn still refused, which should not happen. Say
                // so loudly rather than restocking on top of a seed that never went — two seeds in
                // one socket is the orphan symptom runtime-spawn.md §4b describes.
                Logger.Error($"ReplaceStock() '{gameObject.name}' — held authority of '{outgoing.name}' and Discard() still refused; stock not cycled");
                outgoing.SetGrabEnabled(true);
                yield break;
            }

            SetCurrentSeed(null);
            _socket.Release();
            _wantsStock = true;
        }

        /// <summary>
        /// Whether the seed we set out to cycle has stopped being ours, and hands it back if it
        /// still exists. Nothing else re-enables the grab we took away at the top of ReplaceStock.
        /// </summary>
        private bool Abandoned(Seed outgoing)
        {
            if (outgoing == null) return true;
            if (_currentSeed == outgoing) return false;

            outgoing.SetGrabEnabled(true);
            return true;
        }

        // ── The socket's three facts ──────────────────────────────────────────────

        /// <summary>
        /// Something is in an empty socket. This is how a proxy learns what its stall holds: it
        /// never spawns anything, so the seed arriving is the only thing that ever tells it.
        ///
        /// It used to match seedId against the stall's own definition, which cannot survive
        /// dynamic stock — the type is the thing now changing, and proxies are deliberately not
        /// told what the master rolled. A seed that is shop stock and unbought is stock.
        /// </summary>
        private void OnEntered(GameObject candidate)
        {
            if (_currentSeed != null) return;
            if (candidate == null || !candidate.TryGetComponent(out Seed seed)) return;
            if (seed.IsBought || !seed.InShop) return;

            SetCurrentSeed(seed);
            _socket.Hold(seed.gameObject);
        }

        /// <summary>
        /// The stock has genuinely left the socket. Two clients care, for two different reasons.
        ///
        /// The holder asks to buy it, because only that machine knows its own hand is on the seed
        /// — XRGrabInteractable.isSelected is true on exactly one client, and reading it anywhere
        /// else is what made every other client believe the seed had been knocked loose.
        ///
        /// The master handles everything that is not a purchase, using the replicated holder id
        /// rather than the local interactable, so it agrees with the holder about what happened.
        /// If nobody anywhere is holding it, a hand or another seed shoved it out — treating that
        /// as a sale handed out free seeds and restocked the stall, which is a straightforward
        /// way to print crops.
        /// </summary>
        private void OnLeaving(GameObject leaving)
        {
            if (leaving == null || !leaving.TryGetComponent(out Seed seed)) return;
            if (seed != _currentSeed) return;
            if (seed.IsBought || !seed.InShop) return;

            if (seed.IsHeld)
            {
                seed.RequestPurchase();
                return;
            }

            if (!SceneNetworking.IsMasterClient) return;
            if (!string.IsNullOrEmpty(seed.HolderId)) return;   // someone else has it; their client will ask

            ReturnToSocket(seed, "left the socket with nobody holding it");
        }

        /// <summary>The socket is empty. Fill it.</summary>
        private void OnLeft(GameObject _) => _wantsStock = true;

        // ── Deciding a sale ───────────────────────────────────────────────────────

        /// <summary>
        /// Points this stall at a seed, or at nothing.
        ///
        /// The purchase request arrives on the seed, so the master has to be listening to
        /// whichever seed is currently its stock — and must stop listening when it is not, or a
        /// sold seed could be bought a second time from the stall it no longer occupies.
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

                // The sign follows the stock rather than the roll, so it is right on every client
                // and not just on the one that decided. See StallDisplay.
                if (_display != null) _display.Show(_currentSeed.seedDefinition);
            }
        }

        /// <summary>
        /// Lets go of a seed that has stopped being stock, on every client.
        ///
        /// Driven by lifecycle rather than by the purchase path, because the purchase is decided
        /// on the master alone while releasing the socket is something every client has to do for
        /// itself — ignoring prop collisions is a local physics setting. InShop is replicated, so
        /// this fires everywhere off the same fact instead of each client guessing.
        /// </summary>
        private void OnCurrentSeedLifecycleChanged()
        {
            if (_currentSeed == null || _currentSeed.InShop) return;

            SetCurrentSeed(null);
            _socket.Release();
        }

        /// <summary>
        /// The holder has asked to buy. Master client only — it owns the economy, and deciding
        /// this in more than one place is what let a purchase commit on the buyer's machine while
        /// the seed was sent home on everyone else's.
        /// </summary>
        private void OnPurchaseRequested(Seed seed)
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (seed != _currentSeed) return;
            if (seed.IsBought || !seed.InShop) return;

            PlayerBalance buyer = seed.GetGrabber();

            if (buyer == null)
            {
                // The request came from the holder, so somebody has it — but this client has not
                // been told who. Refuse rather than guess: an uncharged sale cannot be undone,
                // whereas the player can simply pick it up again.
                ReturnToSocket(seed, "was requested by a buyer this client does not know yet");
                return;
            }

            if (!seed.HasKnownAuthority)
            {
                // Nobody owns the object, so Purchase()'s broadcast would reach no one and the
                // sale would exist only on this client.
                ReturnToSocket(seed, "has no state authority");
                return;
            }

            // Priced from the seed, not from _seedDefinition. The stall's definition is what it
            // intends to sell *next* the moment a cycle starts, and ReplaceStock can be waiting on
            // authority while the seed on the shelf is still the old crop. Charging from the stall
            // would sell a carrot at pumpkin prices for exactly that window.
            SeedDefinition sold = seed.seedDefinition;
            if (sold == null)
            {
                ReturnToSocket(seed, "has no definition, so it has no price");
                return;
            }

            if (buyer.GetBalance() < sold.buyPrice)
            {
                Logger.Info($"OnPurchaseRequested() '{gameObject.name}' — '{buyer.GetID()}' cannot afford {sold.seedId} ({buyer.GetBalance()} < {sold.buyPrice})");
                ReturnToSocket(seed, "cannot be afforded");
                return;
            }

            // Purchase() flips InShop, which reaches every client and releases the socket through
            // OnCurrentSeedLifecycleChanged. Restocking follows from the socket saying it is empty.
            seed.Purchase(buyer.GetID());
            EconomyManager.Instance.RemoveBalance(buyer.GetID(), sold.buyPrice);
        }

        /// <summary>
        /// Puts a seed back without charging anyone. Used for every exit that is not a genuine
        /// sale; the socket keeps holding it, so nothing restocks.
        /// </summary>
        private void ReturnToSocket(Seed seed, string why)
        {
            Logger.Warn($"ReturnToSocket() '{gameObject.name}' — seed '{seed.name}' {why}; not a sale");

            // No ForceRelease() here: RestoreToShop() cancels the grab on every client, which is
            // the only way to get it out of a remote player's hand. Releasing it locally first
            // also stopped ReturnableEntity recording that it was the one that suspended the grab,
            // so nothing ever re-enabled it and the seed became permanently untouchable.
            seed.RestoreToShop();      // shop state, but leave it where it stands
            _socket.Recall();
        }

        // ── Spawning ──────────────────────────────────────────────────────────────

        private void SpawnStock()
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (!CanSpawn()) return;

            Seed spawned = SpawnFreshSeed();
            if (spawned == null) return;   // SpawnFreshSeed has already said why; Update retries

            Logger.Info($"SpawnStock() '{gameObject.name}' — stocked '{spawned.name}' authority={spawned.HasLocalAuthority} at {_socket.transform.position}");

            _wantsStock = false;
            SetCurrentSeed(spawned);
            spawned.PlaceInShop(_socket.transform.position, _socket.transform.rotation);
            _socket.Hold(spawned.gameObject);
            StartCoroutine(AnnounceStock(spawned));
        }

        /// <summary>
        /// Whether Fusion can actually create an object right now.
        ///
        /// Asks the runner, not our own bookkeeping. IsSharedModeMasterClient goes true as soon as
        /// the peer is in a room, and SceneNetworking.IsNetworkReady is a static that outlives the
        /// scene it describes — during a world transition the old value is still standing while
        /// the new scene's Update loops are already running. Either one alone said "go" while
        /// Fusion's simulation had no player index yet, and Runner.Spawn() in that window
        /// instantiates the prefab and *then* throws out of Simulation.GetNextId(), leaving an
        /// orphaned GameObject that is never networked and never told what it is. Those orphans
        /// are what put two seeds in one slot.
        ///
        /// LocalPlayer.IsRealPlayer is the question that actually matters — it is false until this
        /// peer has a player index, which is precisely what GetNextId() needs.
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
        /// SharedModeStateAuthMasterClient is the point of the exercise: the master owns the stock
        /// from the instant it exists, so the client that decides what stock is is also the client
        /// that can broadcast it. Under pooling those were routinely different machines, and
        /// broadcastState() self-gates on authority, so the master's decisions silently went
        /// nowhere.
        /// </summary>
        private Seed SpawnFreshSeed()
        {
            SceneNetworking net = SceneNetworking.Instance;
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (net == null || runner == null) return null;

            NetworkObject prefab = _seedDefinition == null ? null : _seedDefinition.seedPrefab;
            if (prefab == null)
            {
                Logger.Error($"SpawnFreshSeed() '{gameObject.name}' — definition '{(_seedDefinition == null ? "<none>" : _seedDefinition.seedId)}' names no seedPrefab");
                return null;
            }

            if (!net.NetworkPrefabs.TryGetValue(prefab, out NetworkPrefabId prefabId))
            {
                Logger.Warn($"SpawnFreshSeed() '{gameObject.name}' — '{prefab.name}' is not registered on SceneNetworking yet");
                return null;
            }

            NetworkObject spawned;
            try
            {
                spawned = runner.Spawn(prefabId, _socket.transform.position, _socket.transform.rotation,
                                       null, null,
                                       NetworkSpawnFlags.SharedModeStateAuthMasterClient);
            }
            catch (System.Exception e)
            {
                // Fusion instantiates the prefab before it allocates an id, so a throw in here has
                // already left a GameObject in the scene that will never be networked. There is no
                // handle to clean it up with — the only real defence is CanSpawn() above, and the
                // retry interval that stops a bad frame becoming a hundred of them.
                //
                // Caught rather than left to propagate because ExceptionAlarm blacks the world out
                // on any exception from this assembly, and a shop that cannot restock is not worth
                // making the garden unplayable for. The error still says so, loudly.
                Logger.Error($"SpawnFreshSeed() '{gameObject.name}' — Fusion threw spawning '{prefab.name}': {e.Message}");
                return null;
            }

            if (spawned == null)
            {
                Logger.Error($"SpawnFreshSeed() '{gameObject.name}' — Fusion refused to spawn '{prefab.name}'");
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
        /// ticks later, so the state broadcast that follows a spawn can be about an object the
        /// other clients do not have yet, and an RPC with nowhere to land is simply dropped.
        /// Pooling never had this problem: every seed existed on every client from scene load, so
        /// no message about one could outrun the object it described.
        ///
        /// Repeating the broadcast is the crude fix, and deliberately so. If the logs show proxies
        /// were already in step, delete the loop and keep a single broadcast.
        /// </summary>
        private IEnumerator AnnounceStock(Seed seed)
        {
            // Gaps, not timestamps — these land at 0.25 s, 1 s and 2 s after the spawn.
            float[] gaps = { 0.25f, 0.75f, 1f };
            foreach (float gap in gaps)
            {
                yield return new WaitForSeconds(gap);

                // Only while it is still ours and still stock — a seed bought in the meantime has
                // moved on, and re-asserting shop state over it is exactly the bug the
                // purchase-authority work removed.
                if (seed == null || seed != _currentSeed || !seed.InShop) yield break;

                seed.broadcastState();
            }
        }

        private void OnValidate()
        {
            if (_socket == null) _socket = GetComponentInChildren<Socket>(true);
            if (_display == null) _display = GetComponentInChildren<StallDisplay>(true);
            if (_rarityMultiplier < 1f) _rarityMultiplier = 1f;
            if (_override != null && _override.seedPrefab == null)
                Logger.Warn($"OnValidate() '{gameObject.name}' — override '{_override.seedId}' names no seedPrefab; stall cannot stock");
        }
    }
}
