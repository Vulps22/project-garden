using Fusion;
using SomniumSpace.Network.Bridge;
using System.Collections;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The counter. A player puts a grown plant down on it and the shop handles everything from
    /// there: it takes the plant out of their hands, pays them, takes ownership of it, and passes
    /// it to storage to be cleaned up for resale.
    ///
    /// The seller *offers*; the shop *accepts*. It used to be the other way round — the master
    /// watched its own copy of a plant it did not own cross this trigger and inferred a sale from
    /// that. Its copy is an interpolated shadow driven by the carrier, so at any real ping it
    /// could miss the volume entirely and the sale simply would not happen. Now the only client
    /// whose copy is authoritative — the one holding it — says so, and the shop decides.
    ///
    /// This has its own NetworkBridge rather than riding the plant's, because the seller is
    /// addressing the shop, not the plant. The plant is named by its NetworkId inside the
    /// payload, the way UnifiedPool already addresses a plant it is told to take back.
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
        private PlantSeed _pending;
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
            if (!other.TryGetComponent(out PlantSeed plant)) return;
            if (plant.IsSeed || plant.SalePending) return;
            if (!plant.IsHeld) return;
            if (plant.GetGrowthCompletion() < 1f) return;

            PlayerBalance seller = EconomyManager.Instance == null
                ? null
                : EconomyManager.Instance.GetLocalPlayer();
            if (seller == null)
            {
                Logger.Warn($"OnTriggerEnter() '{gameObject.name}' — local balance unavailable, cannot offer '{plant.name}' for sale");
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
            writer.AddInt((int)plant.NetworkId);
            writer.AddString(id);
            _networkBridge.RPC_SendMessageToAll((byte)SellMessageType.SellRequest, writer.Data);

            // Optimistic, and only ever local: it is out of my hands the moment I put it down.
            // If the shop refuses, SaleRejected puts it back.
            plant.SetSalePending(true);
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
            uint plantId = (uint)reader.NextInt();
            string sellerId = reader.NextString();

            PlantSeed plant = FindPlant(plantId);
            if (plant == null)
            {
                // Nothing to hand back to — the named plant cannot be resolved on this client.
                Logger.Warn($"OnSellRequested() '{gameObject.name}' — no plant with NetworkId={plantId}");
                return;
            }

            // One at a time, but never by silently dropping the request: the seller has already
            // hidden it on their own screen, so refusing without saying so leaves them staring at
            // a plant that no longer exists anywhere.
            if (_pending != null)
            {
                Reject(plant, "arrived while the counter was busy with another sale");
                return;
            }

            if (plant.IsSeed || plant.IsInPool || plant.GetGrowthCompletion() < 1f)
            {
                Reject(plant, "is not a grown plant");
                return;
            }

            PlayerBalance seller = EconomyManager.Instance == null
                ? null
                : EconomyManager.Instance.GetPlayer(sellerId);
            if (seller == null)
            {
                Reject(plant, $"names a seller this client does not know ('{sellerId}')");
                return;
            }

            _pending = plant;
            _pendingSellerId = sellerId;
            _pendingValue = plant.seedDefinition.sellValue;

            Announce(SellMessageType.SalePending, plant);
            plant.SetSalePending(true);

            StartCoroutine(TakeOwnershipAndComplete(plant));
        }

        /// <summary>
        /// Takes the plant off the seller and completes the sale.
        ///
        /// Ownership is not optional here: paying and pooling both write state, and pooling moves
        /// the transform — which a client that does not hold Fusion authority cannot do, because
        /// NetworkRigidbody3D overwrites a proxy's position on the next tick. Announcing the sale
        /// by RPC is enough to change flags everywhere, but it cannot carry the plant into
        /// storage. So the shop takes ownership first and acts second.
        ///
        /// Latency is free here — the plant is already out of the seller's hands and invisible, so
        /// nothing anybody is looking at is waiting on this.
        /// </summary>
        private IEnumerator TakeOwnershipAndComplete(PlantSeed plant)
        {
            NetworkObject obj = plant.networkBridge == null ? null : plant.networkBridge.Object;
            if (obj == null)
            {
                Reject(plant, "has no spawned network object");
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
                Reject(plant, $"could not be taken into shop ownership within {_authorityTimeout}s");
                yield break;
            }

            EconomyManager.Instance.AddBalance(_pendingSellerId, _pendingValue);

            // Storage first, because only storage knows whether this instance is one of its own.
            if (PoolManager.Instance.ReturnPlantSeed(plant.seedDefinition.seedId, plant))
            {
                plant.Sell();   // tell everyone it is sold; the pooled instance lives on
                Logger.Info($"TakeOwnershipAndComplete() '{gameObject.name}' — sold pooled '{plant.name}' for {_pendingValue} to '{_pendingSellerId}'");
            }
            else
            {
                // Spawned stock. Despawning *is* the announcement — it destroys the object on
                // every client, so there is no state left for a sold RPC to describe, and
                // sending one into the same frame as the despawn only risks a message arriving
                // for an object that no longer exists. The shop already owns it by this point,
                // which is what Despawn requires.
                Logger.Info($"TakeOwnershipAndComplete() '{gameObject.name}' — sold spawned '{plant.name}' for {_pendingValue} to '{_pendingSellerId}'; despawning");
                SceneNetworking.NetworkRunnerRef.Despawn(obj);
            }

            ClearPending();
        }

        /// <summary>Hands the plant back: the shop is not taking it, so it must reappear.</summary>
        private void Reject(PlantSeed plant, string why)
        {
            Logger.Warn($"Reject() '{gameObject.name}' — plant '{plant.name}' {why}; sale abandoned");
            Announce(SellMessageType.SaleRejected, plant);
            plant.SetSalePending(false);
            ClearPending();
        }

        private void ClearPending()
        {
            _pending = null;
            _pendingSellerId = null;
            _pendingValue = 0;
        }

        private void Announce(SellMessageType type, PlantSeed plant)
        {
            var writer = new BytesWriter(BytesWriter.IntSize);
            writer.AddInt((int)plant.NetworkId);
            _networkBridge.RPC_SendMessageToAll((byte)type, writer.Data);
        }

        /// <summary>Applies a pending/rejected announcement on every client.</summary>
        private void SetPendingFromPayload(byte[] data, bool pending)
        {
            var reader = new BytesReader(data);
            if (!reader.IsValid) return;
            PlantSeed plant = FindPlant((uint)reader.NextInt());
            if (plant != null) plant.SetSalePending(pending);
        }

        /// <summary>
        /// Resolves the plant the seller named. Fusion's own registry rather than a list this
        /// component would have to be given and keep in step with the scene.
        /// </summary>
        private PlantSeed FindPlant(uint rawId)
        {
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (runner == null || rawId == 0) return null;
            return runner.TryFindObject(new NetworkId { Raw = rawId }, out NetworkObject obj) && obj != null
                ? obj.GetComponent<PlantSeed>()
                : null;
        }

        private void OnValidate()
        {
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkBridge>();
        }

        private enum SellMessageType : byte
        {
            SellRequest = 0,   // seller -> shop: plant NetworkId + seller id
            SalePending = 1,   // shop -> all: plant NetworkId
            SaleRejected = 2   // shop -> all: plant NetworkId
        }
    }
}
