using Fusion;
using SomniumSpace.Network.Bridge;
using System.Collections;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The counter. A player puts something down on it and the shop handles the rest: it takes the
    /// thing out of their hands, pays them, takes ownership of it, and ends it.
    ///
    /// It deals in SellableEntity, not in crops. Whether a thing is sellable, what it is worth and
    /// what being sold does to it are all the object's own business — so a produce, a bucket of
    /// milk or a sword all pass through here unchanged, and this file has no idea which it just
    /// bought.
    ///
    /// The seller *offers*; the shop *accepts*. It used to be the other way round — the master
    /// watched its own copy of a plant it did not own cross this trigger and inferred a sale from
    /// that. Its copy is an interpolated shadow driven by the carrier, so at any real ping it
    /// could miss the volume entirely and the sale simply would not happen. Now the only client
    /// whose copy is authoritative — the one holding it — says so, and the shop decides.
    ///
    /// This has its own NetworkBridge rather than riding the plant's, because the seller is
    /// addressing the shop, not the plant. The plant is named by its NetworkId inside the
    /// payload, so the message survives whatever is happening to the plant itself.
    /// </summary>
    public class SellPoint : MonoBehaviour
    {
        [SerializeField] private NetworkBridge _networkBridge;

        [Tooltip("How long the shop waits to be given ownership of a plant before abandoning the " +
                 "sale and handing it back. A safety net, not a normal path.")]
        [SerializeField] private float _authorityTimeout = 2f;

        // Master-only bookkeeping for the sale in progress. Captured up front because announcing
        // the sale takes the plant out of the seller's hands, which clears the holder — read it
        // afterwards and there is nobody left to pay.
        private SellableEntity _pending;
        private string _pendingSellerId;
        private int _pendingValue;

        private void Start()
        {
            if (_networkBridge == null)
            {
                Logger.Error($"Start() '{gameObject.name}' — _networkBridge is NULL! Nothing can be sold here.");
                return;
            }
            _networkBridge.OnMessageToAll += OnMessageToAll;
        }

        private void OnDestroy()
        {
            if (_networkBridge != null) _networkBridge.OnMessageToAll -= OnMessageToAll;
        }

        // ── Seller's side ─────────────────────────────────────────────────────────

        /// <summary>
        /// Something crossed the counter. Only the client actually holding it offers it up.
        ///
        /// isSelected is read here deliberately, and this is the one place it means something:
        /// it is true only on the machine whose hand is on the plant, which is exactly the
        /// machine entitled to say "I am putting this down". Every other client sees the same
        /// trigger fire and correctly does nothing.
        ///
        /// A phantom trigger enter is safe to act on — Unity raises those for volumes the body is
        /// genuinely inside, so a spurious enter states something true. It is exits that lie.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            // InParent, because the collider that crosses the counter may be a child mesh rather
            // than the root that carries the trait.
            SellableEntity sellable = other.GetComponentInParent<SellableEntity>();
            if (sellable == null) return;
            if (!sellable.CanBeSold || sellable.IsPending) return;
            if (!sellable.IsHeld) return;

            PlayerBalance seller = EconomyManager.Instance == null
                ? null
                : EconomyManager.Instance.GetLocalPlayer();
            if (seller == null)
            {
                Logger.Warn($"OnTriggerEnter() '{gameObject.name}' — local balance unavailable, cannot offer '{sellable.name}' for sale");
                return;
            }

            // The request carries the seller's id rather than leaving the shop to read the
            // replicated holder. Hiding the plant locally ends the grab, which clears the holder
            // on every client, and the two messages travel on different bridges — so their
            // arrival order is not guaranteed and the shop could be handed a plant nobody owns.
            // Naming the seller in the payload makes the whole thing order-independent.
            string id = seller.GetID();
            int size = BytesWriter.IntSize + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(id);
            var writer = new BytesWriter(size);
            writer.AddInt((int)NetworkIdOf(sellable));
            writer.AddString(id);
            _networkBridge.RPC_SendMessageToAll((byte)SellMessageType.SellRequest, writer.Data);

            // Optimistic, and only ever local: it is out of my hands the moment I put it down.
            // If the shop refuses, SaleRejected puts it back.
            sellable.SetPending(true);
        }

        // ── Shop's side ───────────────────────────────────────────────────────────

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((SellMessageType)id)
            {
                case SellMessageType.SellRequest:
                    OnSellRequested(data);
                    break;
                case SellMessageType.SalePending:
                    SetPendingFromPayload(data, true);
                    break;
                case SellMessageType.SaleRejected:
                    SetPendingFromPayload(data, false);
                    break;
                case SellMessageType.SaleAccepted:
                    // Every client ends the object its own way; only its owner can despawn it.
                    FindSellable(ReadId(data))?.Sell();
                    break;
                default:
                    Logger.Warn($"OnMessageToAll() '{gameObject.name}' — unknown message id={id}");
                    break;
            }
        }

        /// <summary>Master client only — the shop is the only one who accepts a sale.</summary>
        private void OnSellRequested(byte[] data)
        {
            if (!SceneNetworking.IsMasterClient) return;

            var reader = new BytesReader(data);
            if (!reader.IsValid) return;
            uint itemId = (uint)reader.NextInt();
            string sellerId = reader.NextString();

            SellableEntity sellable = FindSellable(itemId);
            if (sellable == null)
            {
                // Nothing to hand back to — the named object cannot be resolved on this client.
                Logger.Warn($"OnSellRequested() '{gameObject.name}' — nothing sellable with NetworkId={itemId}");
                return;
            }

            // One at a time, but never by silently dropping the request: the seller has already
            // hidden it on their own screen, so refusing without saying so leaves them staring at
            // a plant that no longer exists anywhere.
            if (_pending != null)
            {
                Reject(sellable, "arrived while the counter was busy with another sale");
                return;
            }

            if (!sellable.CanBeSold)
            {
                Reject(sellable, "is not something the shop will take");
                return;
            }

            PlayerBalance seller = EconomyManager.Instance == null
                ? null
                : EconomyManager.Instance.GetPlayer(sellerId);
            if (seller == null)
            {
                Reject(sellable, $"names a seller this client does not know ('{sellerId}')");
                return;
            }

            _pending = sellable;
            _pendingSellerId = sellerId;
            _pendingValue = sellable.SellValue;

            Announce(SellMessageType.SalePending, sellable);
            sellable.SetPending(true);

            StartCoroutine(TakeOwnershipAndComplete(sellable));
        }

        /// <summary>
        /// Takes the plant off the seller and completes the sale.
        ///
        /// Ownership is not optional here: Runner.Despawn silently does nothing unless the caller
        /// holds state authority, so a shop that has not taken the plant off the seller would pay
        /// out and leave the plant standing there. So the shop takes ownership first and acts
        /// second.
        ///
        /// Latency is free here — the plant is already out of the seller's hands and invisible, so
        /// nothing anybody is looking at is waiting on this.
        /// </summary>
        private IEnumerator TakeOwnershipAndComplete(SellableEntity sellable)
        {
            NetworkObject obj = ObjectOf(sellable);
            if (obj == null)
            {
                Reject(sellable, "has no spawned network object");
                yield break;
            }

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
                Reject(sellable, $"could not be taken into shop ownership within {_authorityTimeout}s");
                yield break;
            }

            EconomyManager.Instance.AddBalance(_pendingSellerId, _pendingValue);

            // Announced rather than simply despawned, because what a sale *does* to a thing is the
            // thing's own business — a crop disappears, something else might go behind the counter.
            // Every client runs Sell(), which raises the object's Sold event locally before it ends
            // itself; only its owner, which is now the shop, can actually despawn it.
            Logger.Info($"TakeOwnershipAndComplete() '{gameObject.name}' — sold '{sellable.name}' for {_pendingValue} to '{_pendingSellerId}'");
            Announce(SellMessageType.SaleAccepted, sellable);

            ClearPending();
        }

        /// <summary>Hands it back: the shop is not taking it, so it must reappear.</summary>
        private void Reject(SellableEntity sellable, string why)
        {
            Logger.Warn($"Reject() '{gameObject.name}' — '{sellable.name}' {why}; sale abandoned");
            Announce(SellMessageType.SaleRejected, sellable);
            sellable.SetPending(false);
            ClearPending();
        }

        private void ClearPending()
        {
            _pending = null;
            _pendingSellerId = null;
            _pendingValue = 0;
        }

        private void Announce(SellMessageType type, SellableEntity sellable)
        {
            var writer = new BytesWriter(BytesWriter.IntSize);
            writer.AddInt((int)NetworkIdOf(sellable));
            _networkBridge.RPC_SendMessageToAll((byte)type, writer.Data);
        }

        /// <summary>Applies a pending/rejected announcement on every client.</summary>
        private void SetPendingFromPayload(byte[] data, bool pending)
        {
            FindSellable(ReadId(data))?.SetPending(pending);
        }

        private uint ReadId(byte[] data)
        {
            var reader = new BytesReader(data);
            return reader.IsValid ? (uint)reader.NextInt() : 0u;
        }

        private static NetworkObject ObjectOf(SellableEntity sellable) =>
            sellable == null ? null : sellable.GetComponent<NetworkObject>();

        private static uint NetworkIdOf(SellableEntity sellable)
        {
            NetworkObject obj = ObjectOf(sellable);
            return obj == null ? 0u : obj.Id.Raw;
        }

        /// <summary>
        /// Resolves what the seller named. Fusion's own registry rather than a list this component
        /// would have to be given and keep in step with the scene.
        /// </summary>
        private SellableEntity FindSellable(uint rawId)
        {
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (runner == null || rawId == 0) return null;
            return runner.TryFindObject(new NetworkId { Raw = rawId }, out NetworkObject obj) && obj != null
                ? obj.GetComponent<SellableEntity>()
                : null;
        }

        private void OnValidate()
        {
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkBridge>();
        }

        private enum SellMessageType : byte
        {
            SellRequest = 0,    // seller -> shop: item NetworkId + seller id
            SalePending = 1,    // shop -> all: item NetworkId
            SaleRejected = 2,   // shop -> all: item NetworkId
            SaleAccepted = 3    // shop -> all: item NetworkId
        }
    }
}
