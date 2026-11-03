using UnityEngine;
using Vulps.GrowAGarden.Interaction;
using Vulps.GrowAGarden.Islands;

namespace Vulps.GrowAGarden
{
    /// <summary>
    /// A shop stall, on whichever clients have it loaded. It holds and shows its stock, asks the
    /// master for more when it is empty, and tells the master when a hand takes a seed. What is
    /// stocked, what it costs and whether a sale happened are decided by <see cref="ShopKeeper"/>,
    /// by stall id, so the master never needs this stall loaded.
    ///
    /// Every stall behaves the same, garden or island: one sale per cycle, restocked when the
    /// world cycles. A pinned stall (Override set) restocks after every sale.
    /// </summary>
    public class BuyPoint : MonoBehaviour
    {
        /// <summary>Seconds between asks while the stall stands empty, covering a lost request or a failed spawn.</summary>
        private const float ASK_SECONDS = 5f;

        [Tooltip("The socket this stall stocks. It holds the goods; the ShopKeeper decides what they are and what they cost.")]
        [SerializeField] private Socket _socket;

        [Tooltip("Pin this stall to one crop: it always stocks this rather than rolling, and restocks after every sale. Must be in WorldManager's buyables.")]
        [SerializeField] private SeedDefinition _override;

        [Tooltip("How much this stall favours rare stock. 1 leaves the world's weights alone; higher makes uncommon things more likely.")]
        [SerializeField] private float _rarityMultiplier = 1f;

        [Tooltip("The sign. Optional — a stall with no sign simply does not show its prices.")]
        [SerializeField] private StallDisplay _display;

        private Seed _currentSeed;
        private bool _hasId;
        private StallId _id;
        private float _nextAsk;

        /// <summary>Sets how much this stall favours rare stock; an island's stall gets this from the world seed.</summary>
        public void SetRarityMultiplier(float multiplier) => _rarityMultiplier = Mathf.Max(1f, multiplier);

        /// <summary>This stall's id, once it can be worked out.</summary>
        public bool TryGetId(out StallId id)
        {
            id = _id;
            return _hasId;
        }

        private void OnEnable()
        {
            ShopKeeper.Recycled += AskSoon;
            if (_socket != null) _socket.Leaving += OnLeaving;
            _nextAsk = 0f;
        }

        private void OnDisable()
        {
            ShopKeeper.Recycled -= AskSoon;
            if (_socket != null) _socket.Leaving -= OnLeaving;
        }

        private void OnDestroy() => SetCurrentSeed(null);

        private void AskSoon() => _nextAsk = 0f;

        private void Update()
        {
            if (_socket == null) return;
            if (!_hasId && !ResolveId()) return;

            Seed stock = ShopKeeper.StockAt(_id);
            if (stock != _currentSeed) Adopt(stock);

            if (_currentSeed != null || (_override == null && ShopKeeper.IsTaken(_id))) return;
            if (Time.time < _nextAsk) return;
            _nextAsk = Time.time + ASK_SECONDS;

            ShopKeeper.RequestStock(_id, _socket.transform.position, _socket.transform.rotation, _rarityMultiplier,
                                    _override == null ? null : _override.seedId);
        }

        /// <summary>
        /// Works out this stall's id from where it is: the island cell it stands on, else the
        /// Garden in its sector; and its socket's place on that island, or its place among the
        /// Garden's stalls.
        /// </summary>
        private bool ResolveId()
        {
            if (!SectorManager.IsReady) return false;

            SpawnableIsland island = GetComponentInParent<SpawnableIsland>();
            StallSocket socket = GetComponentInParent<StallSocket>();
            Garden garden = island == null ? GetComponentInParent<Garden>() : null;
            int index;
            if (island != null && socket != null)
                index = island.SocketIndex(socket);
            else
                index = System.Array.IndexOf((garden != null ? garden.transform : transform.root).GetComponentsInChildren<BuyPoint>(true), this);
            if (index < 0 || index > byte.MaxValue) return false;

            // A Garden's stalls are named by where the Garden stands, to the metre: several Gardens share a sector.
            Vector3 at = garden != null ? garden.transform.position : transform.root.position;
            _id = island != null && island.HasCell
                ? StallId.Island(island.Cell, (byte)index)
                : StallId.Garden(new Vector2Int(Mathf.RoundToInt(at.x), Mathf.RoundToInt(at.z)), (byte)index);
            _hasId = true;
            Logger.Info($"ResolveId() '{gameObject.name}' — stall {_id}, rarity x{_rarityMultiplier:0}");
            return true;
        }

        /// <summary>Takes the stall's current stock onto the shelf, letting go of anything it held before.</summary>
        private void Adopt(Seed stock)
        {
            if (_currentSeed != null)
            {
                SetCurrentSeed(null);
                _socket.Release();
            }

            if (stock == null)
            {
                AskSoon();
                return;
            }

            SetCurrentSeed(stock);
            _socket.Hold(stock.gameObject);
        }

        /// <summary>
        /// The stock has genuinely left the socket. The holder's client asks to buy it, because only
        /// that machine knows its own hand is on the seed. A seed shoved out with nobody holding it
        /// is not a sale: the socket's auto-recall brings it back on whichever client simulates it.
        /// </summary>
        private void OnLeaving(GameObject leaving)
        {
            if (leaving == null || !leaving.TryGetComponent(out Seed seed)) return;
            // By stall, not by _currentSeed: a peer's stall can lag a restock, and a seed it has not adopted yet must still sell.
            if (!seed.HasStall || !_hasId || !seed.Stall.Equals(_id) || seed.IsBought || !seed.InShop) return;
            if (seed.IsHeld) seed.RequestPurchase();
        }

        private void SetCurrentSeed(Seed seed)
        {
            if (_currentSeed == seed) return;
            if (_currentSeed != null) _currentSeed.LifecycleChanged -= OnCurrentSeedLifecycleChanged;

            _currentSeed = seed;
            if (_currentSeed == null) return;

            _currentSeed.LifecycleChanged += OnCurrentSeedLifecycleChanged;
            if (_display != null) _display.Show(_currentSeed.seedDefinition);
        }

        /// <summary>A refused sale puts the seed back into shop state on every client; this brings it home.</summary>
        private void OnCurrentSeedLifecycleChanged()
        {
            Seed seed = _currentSeed;
            if (seed == null || !seed.InShop || seed.IsHeld || !string.IsNullOrEmpty(seed.HolderId)) return;
            if (_socket.HasLeft(seed.gameObject)) _socket.RecallHere();
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
