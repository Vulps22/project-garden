using Fusion;
using System.Collections;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// A free dispenser. Decides nothing about price or rarity because there is none — it always
    /// offers the one CollectibleEntity prefab set on it in the Editor, never rolls, and is not
    /// part of WorldManager's stock cycle. The Socket beneath it holds the stock and knows none of
    /// that, same split as BuyPoint/Socket.
    ///
    /// This is BuyPoint with the money and the catalogue removed, not a new design — the
    /// holder-requests/master-decides shape, the restock-retry loop and the "shoved out with
    /// nobody holding it" guard all exist for reasons that have nothing to do with price, and
    /// apply just as much to a free item. Only Roll(), WeightOf(), the recycle-cycle subscription
    /// and the balance check are gone.
    /// </summary>
    public class DispensingEntity : MonoBehaviour
    {
        [Tooltip("The socket this dispenser stocks. It holds the item; this component decides " +
                 "when to spawn a fresh one.")]
        [SerializeField] private Socket _socket;

        [Tooltip("What this dispenser gives out. Always this one prefab — a dispenser never " +
                 "rolls and is not part of the world's stock cycle.")]
        [SerializeField] private NetworkObject _itemPrefab;

        private CollectibleEntity _currentItem;

        /// <summary>See BuyPoint.RESTOCK_RETRY_SECONDS — same reasoning, a rate-limited retry
        /// rather than a per-frame spawn attempt.</summary>
        private const float RESTOCK_RETRY_SECONDS = 0.5f;
        private bool _wantsStock;
        private float _lastStockAttempt = float.NegativeInfinity;

        private void OnEnable()
        {
            _wantsStock = true;
            if (_socket == null) return;
            _socket.Entered += OnEntered;
            _socket.Leaving += OnLeaving;
            _socket.Left    += OnLeft;
        }

        private void OnDisable()
        {
            if (_socket == null) return;
            _socket.Entered -= OnEntered;
            _socket.Leaving -= OnLeaving;
            _socket.Left    -= OnLeft;
        }

        private void OnDestroy() => SetCurrentItem(null);   // drops the subscriptions

        private void Update()
        {
            if (!_wantsStock) return;
            if (!SceneNetworking.IsMasterClient) return;
            if (_currentItem != null) { _wantsStock = false; return; }

            if (Time.time - _lastStockAttempt < RESTOCK_RETRY_SECONDS) return;
            _lastStockAttempt = Time.time;
            SpawnStock();
        }

        // ── The socket's three facts ──────────────────────────────────────────────

        private void OnEntered(GameObject candidate)
        {
            if (_currentItem != null) return;
            if (candidate == null || !candidate.TryGetComponent(out CollectibleEntity item)) return;
            if (item.IsTaken || !item.InDispenser) return;

            SetCurrentItem(item);
            _socket.Hold(item.gameObject);
        }

        /// <summary>See BuyPoint.OnLeaving — same two-client split: the holder asks to take it,
        /// because only that machine knows its own hand is on it; the master handles everything
        /// else, off the replicated holder id rather than a local interactable.</summary>
        private void OnLeaving(GameObject leaving)
        {
            if (leaving == null || !leaving.TryGetComponent(out CollectibleEntity item)) return;
            if (item != _currentItem) return;
            if (item.IsTaken || !item.InDispenser) return;

            if (item.IsHeld)
            {
                item.RequestTake();
                return;
            }

            if (!SceneNetworking.IsMasterClient) return;
            if (!string.IsNullOrEmpty(item.HolderId)) return;   // someone else has it; their client will ask

            ReturnToSocket(item, "left the socket with nobody holding it");
        }

        private void OnLeft(GameObject _) => _wantsStock = true;

        // ── Deciding a take ───────────────────────────────────────────────────────

        private void SetCurrentItem(CollectibleEntity item)
        {
            if (_currentItem == item) return;

            if (_currentItem != null)
            {
                _currentItem.TakeRequested -= OnTakeRequested;
                _currentItem.LifecycleChanged -= OnCurrentItemLifecycleChanged;
            }

            _currentItem = item;

            if (_currentItem != null)
            {
                _currentItem.TakeRequested += OnTakeRequested;
                _currentItem.LifecycleChanged += OnCurrentItemLifecycleChanged;
            }
        }

        /// <summary>See BuyPoint.OnCurrentSeedLifecycleChanged — driven by lifecycle rather than
        /// the take path, since releasing the socket is a local physics setting every client has
        /// to do for itself.</summary>
        private void OnCurrentItemLifecycleChanged()
        {
            if (_currentItem == null || _currentItem.InDispenser) return;

            SetCurrentItem(null);
            _socket.Release();
        }

        /// <summary>The holder has asked to take it. Master only, same as a purchase — deciding
        /// this in more than one place is what let a sale commit on the buyer's machine while the
        /// seed was sent home on everyone else's; a free take is no different a decision.</summary>
        private void OnTakeRequested(CollectibleEntity item)
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (item != _currentItem) return;
            if (item.IsTaken || !item.InDispenser) return;

            PlayerBalance taker = item.GetGrabber();

            if (taker == null)
            {
                ReturnToSocket(item, "was requested by a taker this client does not know yet");
                return;
            }

            if (!item.HasKnownAuthority)
            {
                ReturnToSocket(item, "has no state authority");
                return;
            }

            // No price to check — the only thing a purchase's balance gate was ever protecting.
            item.Take(taker.GetID());
        }

        private void ReturnToSocket(CollectibleEntity item, string why)
        {
            Logger.Warn($"ReturnToSocket() '{gameObject.name}' — item '{item.name}' {why}; not a take");

            item.RestoreToDispenser();      // dispenser state, but leave it where it stands
            _socket.Recall();
        }

        // ── Spawning ──────────────────────────────────────────────────────────────

        private void SpawnStock()
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (!CanSpawn()) return;

            CollectibleEntity spawned = SpawnFreshItem();
            if (spawned == null) return;   // SpawnFreshItem has already said why; Update retries

            Logger.Info($"SpawnStock() '{gameObject.name}' — stocked '{spawned.name}' authority={spawned.HasLocalAuthority} at {_socket.transform.position}");

            _wantsStock = false;
            SetCurrentItem(spawned);
            spawned.PlaceInDispenser(_socket.transform.position, _socket.transform.rotation);
            _socket.Hold(spawned.gameObject);
            StartCoroutine(AnnounceStock(spawned));
        }

        /// <summary>See BuyPoint.CanSpawn — same Fusion readiness check, same reason.</summary>
        private bool CanSpawn()
        {
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            return runner != null
                && runner.IsRunning
                && runner.LocalPlayer.IsRealPlayer
                && SceneNetworking.IsNetworkReady;
        }

        private CollectibleEntity SpawnFreshItem()
        {
            SceneNetworking net = SceneNetworking.Instance;
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (net == null || runner == null) return null;

            if (_itemPrefab == null)
            {
                Logger.Error($"SpawnFreshItem() '{gameObject.name}' — no item prefab set");
                return null;
            }

            if (!net.NetworkPrefabs.TryGetValue(_itemPrefab, out NetworkPrefabId prefabId))
            {
                Logger.Warn($"SpawnFreshItem() '{gameObject.name}' — '{_itemPrefab.name}' is not registered on SceneNetworking yet");
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
                // See BuyPoint.SpawnFreshSeed's catch — same orphan risk, same reason it is caught
                // rather than left to propagate.
                Logger.Error($"SpawnFreshItem() '{gameObject.name}' — Fusion threw spawning '{_itemPrefab.name}': {e.Message}");
                return null;
            }

            if (spawned == null)
            {
                Logger.Error($"SpawnFreshItem() '{gameObject.name}' — Fusion refused to spawn '{_itemPrefab.name}'");
                return null;
            }

            CollectibleEntity item = spawned.GetComponent<CollectibleEntity>();
            if (item == null)
                Logger.Error($"SpawnFreshItem() '{gameObject.name}' — spawned '{spawned.name}' has no CollectibleEntity component");

            return item;
        }

        /// <summary>See BuyPoint.AnnounceStock — same late-registration problem, same crude fix.</summary>
        private IEnumerator AnnounceStock(CollectibleEntity item)
        {
            float[] gaps = { 0.25f, 0.75f, 1f };
            foreach (float gap in gaps)
            {
                yield return new WaitForSeconds(gap);

                if (item == null || item != _currentItem || !item.InDispenser) yield break;

                item.broadcastState();
            }
        }

        private void OnValidate()
        {
            if (_socket == null) _socket = GetComponentInChildren<Socket>(true);
        }
    }
}
