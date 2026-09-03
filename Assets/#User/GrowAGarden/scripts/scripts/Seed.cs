using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    /// <summary>
    /// A seed: shop stock, then a bought thing a player carries, then something they put in the
    /// ground. That is its whole life — it never grows, is never sold, and stops existing the
    /// moment it is planted.
    ///
    /// This is the half of the old PlantSeed that happened above ground. The other half became
    /// <see cref="Plant"/>. Splitting them deleted IsSeed, SetState(bool) and UpdateVisuals(bool)
    /// outright: those existed only because one pooled object had to be a seed and a crop by
    /// turns, and nothing has to do that any more.
    /// </summary>
    public class Seed : MonoBehaviour, IHeldObject, IKinematicSource, IAuthoritySource,
                        ILifecycleNotifier, IPlantable
    {
        [SerializeField] public SeedDefinition seedDefinition;
        [SerializeField] public NetworkBridge networkBridge;
        [SerializeField] protected XRGrabInteractable _grabInteractable;

        [Tooltip("What grows when this is planted. Must also be listed on SceneNetworking.")]
        [SerializeField] private NetworkObject _plantPrefab;

        public bool InShop { get; private set; }
        public bool IsBought { get; private set; }

        /// <summary>
        /// Who paid for this. Set by the master at the moment of purchase and replicated, because
        /// ownership is a fact and facts come from the master.
        ///
        /// Deliberately not Fusion state authority. Authority transfers on *hover* — see
        /// NetworkGrabbable.OnHover — so a player reaching towards a dropped seed would become its
        /// owner without ever grabbing it. Authority answers "who is simulating this"; this answers
        /// "whose is it", and those have different lifetimes.
        /// </summary>
        public string OwnerId { get; private set; }

        public uint NetworkId => networkBridge?.Object == null ? 0u : networkBridge.Object.Id.Raw;
        public NetworkObject PlantPrefab => _plantPrefab;
        public bool CanBePlanted => IsBought && !InShop;

        public event System.Action LifecycleChanged;

        /// <summary>Raised on every client when the holder asks to buy. Only the master acts.</summary>
        public event System.Action<Seed> PurchaseRequested;

        /// <summary>A seed is a physical object wherever it is; nothing about it is anchored.</summary>
        public bool ShouldBeKinematic => false;

        /// <summary>
        /// Shop stock hangs at its slot, so the tether holds position instead of fighting a
        /// constant downward pull. A bought seed loose in the world has weight.
        /// </summary>
        public bool ShouldUseGravity => !InShop;

        /// <summary>
        /// Stock belongs to the world, not to whoever last touched it. The holder check is what
        /// stops the master claiming a seed out of a player's hand. Rarely needed now that the
        /// master spawns stock already owning it — this is what recovers one that was carried and
        /// put back.
        /// </summary>
        public bool ShouldMasterOwn => InShop && string.IsNullOrEmpty(HolderId);

        private HoveringEntity _hovering;
        private PlayerBalance _grabber;

        /// <summary>Bought, on the ground, and in nobody's hand. The one condition that floats.</summary>
        private bool IsLooseInWorld => !InShop && !IsHeld;

        public string HolderId => _grabber?.GetID();
        public bool IsHeld => _grabInteractable != null && _grabInteractable.isSelected;

        /// <summary>
        /// True while there is a live NetworkObject to send on. Fusion throws
        /// "Behaviour not initialized: Object not set." from an RPC on a detached object, and an
        /// XRI teardown deselect arrives *after* Fusion has detached this one: despawning a seed
        /// runs OnDisable -> UnregisterWithInteractionManager -> SelectCancel -> OnGrabDeselected.
        /// The throw escaped UnregisterInteractable before it dropped this interactable from the
        /// manager's list, so the destroyed component stayed registered and XRInteractionManager
        /// threw a NullReferenceException every frame for the rest of the session.
        /// </summary>
        private bool CanSendRpc => networkBridge != null && networkBridge.Object != null;

        public bool HasLocalAuthority =>
            networkBridge != null && networkBridge.Object != null && networkBridge.Object.HasStateAuthority;

        public bool HasKnownAuthority =>
            networkBridge != null && networkBridge.Object != null && !networkBridge.Object.StateAuthority.IsNone;

        /// <summary>
        /// Awake, not Start: Fusion raises Spawned() while instantiating a networked prefab, which
        /// for a runtime spawn is the frame it is created — before Unity reaches Start. A seed that
        /// subscribed in Start missed its own spawn callback. See runtime-spawn.md.
        /// </summary>
        private void Awake()
        {
            _hovering = GetComponent<HoveringEntity>();
            LifecycleChanged += UpdateHovering;

            networkBridge.OnSpawned += OnSpawned;
            networkBridge.OnStateAuthorityChanged += OnStateAuthorityChanged;
            networkBridge.OnMessageToAll += OnMessageToAll;
            networkBridge.OnMessageToProxies += OnMessageToProxies;
            SceneNetworking.OnOtherPlayerJoined += OnOtherPlayerJoined;
            GardenLease.PlayerGone += OnPlayerGone;
            _grabInteractable.selectEntered.AddListener(OnGrabSelected);
            _grabInteractable.selectExited.AddListener(OnGrabDeselected);
        }

        private void OnDestroy()
        {
            LifecycleChanged -= UpdateHovering;
            if (networkBridge != null)
            {
                networkBridge.OnSpawned -= OnSpawned;
                networkBridge.OnStateAuthorityChanged -= OnStateAuthorityChanged;
                networkBridge.OnMessageToAll -= OnMessageToAll;
                networkBridge.OnMessageToProxies -= OnMessageToProxies;
            }
            SceneNetworking.OnOtherPlayerJoined -= OnOtherPlayerJoined;
            GardenLease.PlayerGone -= OnPlayerGone;
            _grabInteractable.selectEntered.RemoveListener(OnGrabSelected);
            _grabInteractable.selectExited.RemoveListener(OnGrabDeselected);
        }

        /// <summary>
        /// A player who has left is holding nothing. Clearing this at once is a correction, not
        /// cleanup — leaving a departed player's id in place makes SingleHolderFilter refuse this
        /// seed to everyone for the rest of the session. Every client learns of the departure
        /// locally, so no message is needed.
        /// </summary>
        private void OnPlayerGone(string playerId)
        {
            if (_grabber == null || _grabber.GetID() != playerId) return;
            _grabber = null;
            LifecycleChanged?.Invoke();
        }

        private void UpdateHovering()
        {
            if (_hovering == null) return;

            if (IsLooseInWorld)
            {
                if (!_hovering.IsHovering) _hovering.BeginHovering(shouldFallFirst: true);
            }
            else if (_hovering.IsHovering)
            {
                _hovering.StopHovering();
            }
        }

        /// <summary>
        /// Re-sends state once a transfer lands, but only on the master.
        ///
        /// A client that has just gained authority knows least about the object — it was a proxy a
        /// moment ago, and with hover-transfer it may have gained authority by accident. The master
        /// is the one client whose view is authoritative, so it is the only one allowed to assert.
        /// </summary>
        private void OnStateAuthorityChanged(bool hasAuthority)
        {
            if (hasAuthority && SceneNetworking.IsMasterClient) broadcastState();
        }

        private void OnSpawned()
        {
            if (networkBridge.Object.HasStateAuthority) LifecycleChanged?.Invoke();
        }

        private void OnOtherPlayerJoined(PlayerRef player) => broadcastState();

        // ── Shop ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Asks the master to sell this seed to whoever is holding it.
        ///
        /// Called by the holder's own client, because the holder is the only one that knows the
        /// seed was deliberately taken rather than knocked loose. Sent to all rather than to the
        /// controller: the controller of a held seed is the holder, and it is the master that has
        /// to decide. Everyone else ignores it.
        /// </summary>
        public void RequestPurchase()
        {
            networkBridge.RPC_SendMessageToAll((byte)SeedMessageType.purchaseRequest, new byte[0]);
        }

        public void PlaceInShop(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
            RestoreToShop();
        }

        /// <summary>
        /// Puts this back into shop-stock state where it currently stands, without moving it —
        /// teleporting it would defeat the point of gliding it back.
        /// </summary>
        public void RestoreToShop()
        {
            networkBridge.RPC_SendMessageToAll((byte)SeedMessageType.restoredToShop, new byte[0]);
        }

        private void ApplyRestoreToShop()
        {
            InShop = true;
            IsBought = false;
            OwnerId = null;

            // A seed that is stock again is in nobody's hand. Disabling the interactable cancels
            // any select in progress, and because this runs from an RPC it runs on the holder's
            // machine too — which is what actually takes it back.
            _grabInteractable.enabled = false;

            var ret = GetComponent<ReturnableEntity>();
            if (ret == null || !ret.IsReturning) _grabInteractable.enabled = true;

            Logger.Info($"ApplyRestoreToShop() '{gameObject.name}' — InShop={InShop} grab={_grabInteractable.enabled} authority={HasLocalAuthority}");

            LifecycleChanged?.Invoke();
            broadcastState();
        }

        /// <summary>
        /// Sold to the named player. Announced by RPC rather than written locally and pushed with
        /// broadcastState(), because the master is almost never this object's state authority when
        /// it decides a sale — NetworkGrabbable takes authority as the buyer's hand approaches, so
        /// by the time the seed leaves the slot the buyer owns it. broadcastState() self-gates on
        /// HasStateAuthority, so the master's writes would stay on the master, silently.
        /// </summary>
        public void Purchase(string buyerId)
        {
            int size = sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(buyerId ?? string.Empty);
            var writer = new BytesWriter(size);
            writer.AddString(buyerId ?? string.Empty);
            networkBridge.RPC_SendMessageToAll((byte)SeedMessageType.purchased, writer.Data);
        }

        private void ApplyPurchase(byte[] data)
        {
            var reader = new BytesReader(data);
            OwnerId = reader.IsValid ? reader.NextString() : null;
            InShop = false;
            IsBought = true;
            ClearShopClaim();
            LifecycleChanged?.Invoke();
            broadcastState();
        }

        /// <summary>
        /// Lets go of any shop slot's claim on this seed.
        ///
        /// ReturnableEntity and AlignableEntity are armed by ShopSlot on the master alone, and only
        /// the successful-purchase path used to clear them. Every other way a seed stops being
        /// stock left a live return target pointing at the shop, so the slot would drag the seed
        /// back out of the plot it had just been planted in, and keep doing it. Runs on every
        /// client because each undoes its own local components, and is idempotent.
        /// </summary>
        private void ClearShopClaim()
        {
            var ret = GetComponent<ReturnableEntity>();
            if (ret != null)
            {
                ret.SetAutoRecall(false);
                ret.ClearReturnTarget();
            }

            var align = GetComponent<AlignableEntity>();
            if (align != null)
            {
                align.SetAutoRealign(false);
                align.ClearAlignTarget();
            }
        }

        // ── Planting ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Offers this seed to a plot as the holder carries it in.
        ///
        /// The holder asks and the master decides, for the same reason a purchase works that way:
        /// only this machine knows its own hand is on the seed. Every other client sees the same
        /// trigger and correctly does nothing.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (!IsHeld || !CanBePlanted) return;
            if (!other.TryGetComponent(out PlantSlot slot)) return;
            slot.RequestPlant(this);
        }

        /// <summary>
        /// The master said yes. Runs on every client, so the seed leaves the player's hand
        /// everywhere at once; the master despawns it a moment later once it has taken ownership.
        /// </summary>
        public void ApplyPlanted()
        {
            ClearShopClaim();
            _grabInteractable.enabled = false;

            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (r != null) r.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) if (c != null) c.enabled = false;

            _grabber = null;
            LifecycleChanged?.Invoke();
        }

        /// <summary>
        /// Removed because its owner's garden lease expired. Master only — despawning needs state
        /// authority, and by this point nobody is holding it to have taken authority away.
        /// </summary>
        public void Discard()
        {
            if (!SceneNetworking.IsMasterClient) return;

            NetworkObject obj = networkBridge == null ? null : networkBridge.Object;
            if (obj != null && obj.HasStateAuthority) SceneNetworking.NetworkRunnerRef.Despawn(obj);
        }

        // ── State ─────────────────────────────────────────────────────────────────

        public void broadcastState()
        {
            if (networkBridge.Object == null) return;
            if (!networkBridge.Object.HasStateAuthority) return;

            string owner = OwnerId ?? string.Empty;
            int size = BytesWriter.ByteSize * 2 + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(owner);
            BytesWriter writer = new BytesWriter(size);
            writer.AddByte(InShop ? (byte)1 : (byte)0);
            writer.AddByte(IsBought ? (byte)1 : (byte)0);
            writer.AddString(owner);
            networkBridge.RPC_SendMessageToProxies((byte)SeedMessageType.stateSync, writer.Data);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((SeedMessageType)id)
            {
                case SeedMessageType.grabber:
                    BytesReader grabReader = new BytesReader(data);
                    bool hasGrabber = grabReader.NextByte() == 1;
                    _grabber = hasGrabber ? EconomyManager.Instance.GetPlayer(grabReader.NextString()) : null;
                    // Who holds it is lifecycle. This is the one place _grabber changes on every
                    // client, so raising here lets AuthorityController reclaim a shop seed the
                    // moment it leaves a player's hand.
                    LifecycleChanged?.Invoke();
                    break;
                case SeedMessageType.purchaseRequest:
                    PurchaseRequested?.Invoke(this);
                    break;
                case SeedMessageType.purchased:
                    ApplyPurchase(data);
                    break;
                case SeedMessageType.restoredToShop:
                    ApplyRestoreToShop();
                    break;
                default:
                    Logger.Warn($"OnMessageToAll() '{gameObject.name}' — unknown message id={id}");
                    break;
            }
        }

        private void OnMessageToProxies(byte id, byte[] data)
        {
            if ((SeedMessageType)id != SeedMessageType.stateSync) return;

            BytesReader reader = new BytesReader(data);
            InShop = reader.NextByte() == 1;
            IsBought = reader.NextByte() == 1;
            string owner = reader.NextString();
            OwnerId = string.IsNullOrEmpty(owner) ? null : owner;

            _grabInteractable.enabled = true;
            LifecycleChanged?.Invoke();
        }

        public void SetGrabEnabled(bool enabled)
        {
            if (_grabInteractable != null) _grabInteractable.enabled = enabled;
        }

        public void ForceRelease()
        {
            if (_grabInteractable != null) _grabInteractable.enabled = false;
        }

        public PlayerBalance GetGrabber() => _grabber;

        public void OnGrabSelected(SelectEnterEventArgs args)
        {
            _grabber = EconomyManager.Instance == null ? null : EconomyManager.Instance.GetLocalPlayer();
            if (_grabber == null)
            {
                Logger.Warn($"OnGrabSelected() '{gameObject.name}' — local balance unavailable, grabber not broadcast");
                return;
            }

            if (!CanSendRpc) return;

            string id = _grabber.GetID();
            int size = BytesWriter.ByteSize + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(id);
            var writer = new BytesWriter(size);
            writer.AddByte(1);
            writer.AddString(id);
            networkBridge.RPC_SendMessageToAll((byte)SeedMessageType.grabber, writer.Data);
        }

        public void OnGrabDeselected(SelectExitEventArgs args)
        {
            // A teardown deselect is not a player letting go, and there is nothing to announce:
            // the despawn already tells every peer this seed is gone. See CanSendRpc.
            if (!CanSendRpc) return;

            var writer = new BytesWriter(BytesWriter.ByteSize);
            writer.AddByte(0);
            networkBridge.RPC_SendMessageToAll((byte)SeedMessageType.grabber, writer.Data);
        }

        private void OnValidate()
        {
            if (networkBridge == null) networkBridge = GetComponent<NetworkBridge>();
            if (_grabInteractable == null) _grabInteractable = GetComponent<XRGrabInteractable>();
        }
    }

    /// <summary>
    /// Message ids are scoped per NetworkBridge, so this starts at 0 without colliding with
    /// PlantMessageType or ProduceMessageType. Append new values, never insert — these are wire
    /// ids, and renumbering makes two builds disagree about what a message means.
    /// </summary>
    enum SeedMessageType : byte
    {
        stateSync = 0,
        grabber = 1,
        purchaseRequest = 2,
        purchased = 3,
        restoredToShop = 4
    }
}
