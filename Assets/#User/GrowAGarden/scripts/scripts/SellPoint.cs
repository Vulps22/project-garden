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


        // Master-only bookkeeping for the sale in progress. Captured up front because announcing
        // the sale takes the plant out of the seller's hands, which clears the holder — read it
        // afterwards and there is nobody left to pay.
        private SellableEntity _pending;
        private string _pendingSellerId;

        /// <summary>
        /// A sale completed: who made it and what they earned. Raised on every client, because the
        /// master announces it to everyone.
        ///
        /// This used to be a local guess. SaleAccepted carried only the item's NetworkId, so each
        /// client matched it against a set of ids it had offered itself — a client inferring
        /// something it should have been told, which is the exact shape CLAUDE.md's governing rule
        /// warns about, and it silently never matched. The seller is now in the payload, so every
        /// peer is told the whole fact and nobody has to work it out.
        ///
        /// Anything that wants "did *I* just sell something" compares the id against
        /// PlayerManager.LocalPlayerId. Anything that wants "did anyone" ignores it.
        /// </summary>
        public static event System.Action<string, int> SaleCompleted;

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

            // Statics outlive a scene when domain reload is off, and a subscriber from the last
            // session is a destroyed object waiting to be called.
            SaleCompleted = null;
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

            PlayerIdentity seller = PlayerManager.GetLocalPlayer();
            if (!seller.Exists)
            {
                Logger.Warn($"OnTriggerEnter() '{gameObject.name}' — local player unavailable, cannot offer '{sellable.name}' for sale");
                return;
            }

            // The request carries the seller's id rather than leaving the shop to read the
            // replicated holder. Hiding the plant locally ends the grab, which clears the holder
            // on every client, and the two messages travel on different bridges — so their
            // arrival order is not guaranteed and the shop could be handed a plant nobody owns.
            // Naming the seller in the payload makes the whole thing order-independent.
            string id = seller.Id;
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
                    ApplySaleAccepted(data);
                    break;
                default:
                    Logger.Warn($"OnMessageToAll() '{gameObject.name}' — unknown message id={id}");
                    break;
            }
        }

        /// <summary>Master client only — the shop is the only one who accepts a sale.</summary>
        private void OnSellRequested(byte[] data)
        {
            if (!PlayerManager.IsMaster) return;

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

            bool granted = false;
            yield return WorldManager.TakeAuthority(obj, r => granted = r);

            if (!granted)
            {
                Reject(sellable, $"could not be taken into shop ownership within {WorldManager.AuthorityTimeout}s");
                yield break;
            }

            EconomyManager.Instance.AddBalance(_pendingSellerId, _pendingValue);

            // Announced rather than simply despawned, because what a sale *does* to a thing is the
            // thing's own business — a crop disappears, something else might go behind the counter.
            // Every client runs Sell(), which raises the object's Sold event locally before it ends
            // itself; only its owner, which is now the shop, can actually despawn it.
            Logger.Info($"TakeOwnershipAndComplete() '{gameObject.name}' — sold '{sellable.name}' for {_pendingValue} to '{_pendingSellerId}'");
            AnnounceSale(sellable, _pendingSellerId, _pendingValue);

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

        /// <summary>
        /// SaleAccepted carries more than the other two: the item, who sold it, and for how much.
        ///
        /// A separate writer rather than widening <see cref="Announce"/>, because SalePending and
        /// SaleRejected genuinely only need the item — and BytesWriter is pre-sized, so one shared
        /// size calculation covering a field two of the three messages never send is how a payload
        /// silently overflows later.
        /// </summary>
        private void AnnounceSale(SellableEntity sellable, string sellerId, int value)
        {
            sellerId ??= string.Empty;
            int size = BytesWriter.IntSize                                     // item id
                     + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(sellerId)
                     + BytesWriter.IntSize;                                    // value

            var writer = new BytesWriter(size);
            writer.AddInt((int)NetworkIdOf(sellable));
            writer.AddString(sellerId);
            writer.AddInt(value);
            _networkBridge.RPC_SendMessageToAll((byte)SellMessageType.SaleAccepted, writer.Data);
        }

        /// <summary>Applies an accepted sale on every client, the sender included.</summary>
        private void ApplySaleAccepted(byte[] data)
        {
            var reader = new BytesReader(data);
            if (!reader.IsValid) return;

            uint itemId = (uint)reader.NextInt();
            string sellerId = reader.NextString();
            int value = reader.NextInt();

            // Every client ends the object its own way; only its owner can actually despawn it.
            FindSellable(itemId)?.Sell();

            // Raised after Sell(), so a listener sees a world where the sale has already happened
            // rather than one mid-transaction.
            Logger.Info($"ApplySaleAccepted() '{gameObject.name}' — item={itemId} seller='{sellerId}' value={value}");
            SaleCompleted?.Invoke(sellerId, value);
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
