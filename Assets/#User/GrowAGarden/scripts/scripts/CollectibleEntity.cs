using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    /// <summary>
    /// A free, non-seed thing a player takes from a DispensingEntity: dispenser stock, then
    /// something they carry. It never grows and is never sold — that is the whole difference from
    /// Seed, which this otherwise mirrors closely (same holder/take-request/master-decides shape,
    /// same wire-message pattern) because a dispensed item is stock in exactly the sense a seed on
    /// a shelf is stock, just free and never planted.
    ///
    /// One concrete component rather than a definition-plus-instance split the way Seed and
    /// SeedDefinition are: a seed's definition is shared across three prefabs (seed, plant,
    /// produce), which is the only reason splitting them paid for itself. A dispensed collectible
    /// has no such second prefab to share identity with, so this is definition and instance in one
    /// place, and the identity fields below are set once in the Editor rather than hardcoded in
    /// code — DispensingEntity always offers one predefined item, never rolls one.
    /// </summary>
    public class CollectibleEntity : MonoBehaviour, IHeldObject, IKinematicSource, IAuthoritySource, ILifecycleNotifier
    {
        [Tooltip("Stable id for this kind of collectible — logging and future lookups, not " +
                 "shown to players.")]
        public string collectibleId;

        public string displayName;

        [SerializeField] public NetworkBridge networkBridge;
        [SerializeField] protected XRGrabInteractable _grabInteractable;

        public bool InDispenser { get; private set; }
        public bool IsTaken { get; private set; }

        /// <summary>
        /// Who took this. Set by the master the moment it is taken and replicated, same as
        /// Seed.OwnerId — a fact the master decides, not Fusion state authority (which transfers on
        /// hover and answers a different question: who is simulating this right now).
        /// </summary>
        public string TakenBy { get; private set; }

        public uint NetworkId => networkBridge?.Object == null ? 0u : networkBridge.Object.Id.Raw;

        public event System.Action LifecycleChanged;

        /// <summary>Raised on every client when the holder asks to take it. Only the master acts.</summary>
        public event System.Action<CollectibleEntity> TakeRequested;

        /// <summary>A collectible is a physical object wherever it is; nothing about it is anchored.</summary>
        public bool ShouldBeKinematic => false;

        /// <summary>Dispenser stock hangs at its socket, so the tether holds position instead of
        /// fighting a constant downward pull. A taken one loose in the world has weight.</summary>
        public bool ShouldUseGravity => !InDispenser;

        /// <summary>Stock belongs to the world, not to whoever last touched it — see
        /// Seed.ShouldMasterOwn, same reasoning.</summary>
        public bool ShouldMasterOwn => InDispenser && string.IsNullOrEmpty(HolderId);

        private HoveringEntity _hovering;
        private PlayerBalance _grabber;

        private bool IsLooseInWorld => !InDispenser && !IsHeld;

        public string HolderId => _grabber?.GetID();
        public bool IsHeld => _grabInteractable != null && _grabInteractable.isSelected;

        /// <summary>See Seed.CanSendRpc for why this guard exists — same Fusion teardown-order
        /// trap applies to any networked grabbable.</summary>
        private bool CanSendRpc => networkBridge != null && networkBridge.Object != null;

        public bool HasLocalAuthority =>
            networkBridge != null && networkBridge.Object != null && networkBridge.Object.HasStateAuthority;

        public bool HasKnownAuthority =>
            networkBridge != null && networkBridge.Object != null && !networkBridge.Object.StateAuthority.IsNone;

        /// <summary>Awake, not Start — see Seed.Awake for why (Fusion's Spawned() runs before Start
        /// for a runtime-spawned networked prefab).</summary>
        private void Awake()
        {
            _hovering = GetComponent<HoveringEntity>();
            LifecycleChanged += UpdateHovering;

            networkBridge.OnSpawned += OnSpawned;
            networkBridge.OnStateAuthorityChanged += OnStateAuthorityChanged;
            networkBridge.OnMessageToAll += OnMessageToAll;
            networkBridge.OnMessageToProxies += OnMessageToProxies;
            SceneNetworking.OnOtherPlayerJoined += OnOtherPlayerJoined;
            PlayerManager.PlayerLeft += OnPlayerLeft;
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
            PlayerManager.PlayerLeft -= OnPlayerLeft;
            _grabInteractable.selectEntered.RemoveListener(OnGrabSelected);
            _grabInteractable.selectExited.RemoveListener(OnGrabDeselected);
        }

        /// <summary>A player who has left is holding nothing — see Seed.OnPlayerLeft.</summary>
        private void OnPlayerLeft(string playerId)
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

        private void OnStateAuthorityChanged(bool hasAuthority)
        {
            if (hasAuthority && SceneNetworking.IsMasterClient) broadcastState();
        }

        private void OnSpawned()
        {
            if (networkBridge.Object.HasStateAuthority) LifecycleChanged?.Invoke();
        }

        private void OnOtherPlayerJoined(PlayerRef player) => broadcastState();

        // ── Dispenser ─────────────────────────────────────────────────────────────

        /// <summary>Asks the master to hand this to whoever is holding it — see
        /// Seed.RequestPurchase for why the holder is the one asking.</summary>
        public void RequestTake()
        {
            networkBridge.RPC_SendMessageToAll((byte)CollectibleMessageType.takeRequest, new byte[0]);
        }

        public void PlaceInDispenser(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
            RestoreToDispenser();
        }

        /// <summary>Puts this back into dispenser-stock state where it currently stands — not
        /// teleported, so a glide-back reads as a glide-back.</summary>
        public void RestoreToDispenser()
        {
            networkBridge.RPC_SendMessageToAll((byte)CollectibleMessageType.restoredToDispenser, new byte[0]);
        }

        private void ApplyRestoreToDispenser()
        {
            InDispenser = true;
            IsTaken = false;
            TakenBy = null;

            _grabInteractable.enabled = false;

            var ret = GetComponent<ReturnableEntity>();
            if (ret == null || !ret.IsReturning) _grabInteractable.enabled = true;

            Logger.Info($"ApplyRestoreToDispenser() '{gameObject.name}' — InDispenser={InDispenser} grab={_grabInteractable.enabled} authority={HasLocalAuthority}");

            LifecycleChanged?.Invoke();
            broadcastState();
        }

        /// <summary>Given to the named player. Announced by RPC rather than broadcastState() —
        /// see Seed.Purchase, same authority-timing reason.</summary>
        public void Take(string takerId)
        {
            int size = sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(takerId ?? string.Empty);
            var writer = new BytesWriter(size);
            writer.AddString(takerId ?? string.Empty);
            networkBridge.RPC_SendMessageToAll((byte)CollectibleMessageType.taken, writer.Data);
        }

        private void ApplyTaken(byte[] data)
        {
            var reader = new BytesReader(data);
            TakenBy = reader.IsValid ? reader.NextString() : null;
            InDispenser = false;
            IsTaken = true;
            ClearDispenserClaim();
            LifecycleChanged?.Invoke();
            broadcastState();
        }

        /// <summary>Lets go of the dispenser's claim on this — see Seed.ClearShopClaim, same
        /// reasoning: every way of leaving dispenser state has to clear it, or the socket drags a
        /// taken item back.</summary>
        private void ClearDispenserClaim()
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

        /// <summary>Master only — see Seed.Discard, same Fusion-authority caveat.</summary>
        public bool Discard()
        {
            if (!SceneNetworking.IsMasterClient) return false;

            NetworkObject obj = networkBridge == null ? null : networkBridge.Object;
            if (obj == null)
            {
                Logger.Warn($"Discard() '{gameObject.name}' — no NetworkObject; nothing despawned");
                return false;
            }
            if (!obj.HasStateAuthority)
            {
                Logger.Warn($"Discard() '{gameObject.name}' — no state authority (authority known={HasKnownAuthority}); NOT despawned");
                return false;
            }

            SceneNetworking.NetworkRunnerRef.Despawn(obj);
            return true;
        }

        // ── State ─────────────────────────────────────────────────────────────────

        public void broadcastState()
        {
            if (networkBridge.Object == null) return;
            if (!networkBridge.Object.HasStateAuthority) return;

            string takenBy = TakenBy ?? string.Empty;
            int size = BytesWriter.ByteSize * 2 + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(takenBy);
            BytesWriter writer = new BytesWriter(size);
            writer.AddByte(InDispenser ? (byte)1 : (byte)0);
            writer.AddByte(IsTaken ? (byte)1 : (byte)0);
            writer.AddString(takenBy);
            networkBridge.RPC_SendMessageToProxies((byte)CollectibleMessageType.stateSync, writer.Data);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((CollectibleMessageType)id)
            {
                case CollectibleMessageType.grabber:
                    BytesReader grabReader = new BytesReader(data);
                    bool hasGrabber = grabReader.NextByte() == 1;
                    string grabberId = hasGrabber ? grabReader.NextString() : null;
                    _grabber = hasGrabber ? EconomyManager.Instance.GetPlayer(grabberId) : null;
                    LifecycleChanged?.Invoke();
                    break;
                case CollectibleMessageType.takeRequest:
                    TakeRequested?.Invoke(this);
                    break;
                case CollectibleMessageType.taken:
                    ApplyTaken(data);
                    break;
                case CollectibleMessageType.restoredToDispenser:
                    ApplyRestoreToDispenser();
                    break;
                default:
                    Logger.Warn($"OnMessageToAll() '{gameObject.name}' — unknown message id={id}");
                    break;
            }
        }

        private void OnMessageToProxies(byte id, byte[] data)
        {
            if ((CollectibleMessageType)id != CollectibleMessageType.stateSync) return;

            BytesReader reader = new BytesReader(data);
            InDispenser = reader.NextByte() == 1;
            IsTaken = reader.NextByte() == 1;
            string takenBy = reader.NextString();
            TakenBy = string.IsNullOrEmpty(takenBy) ? null : takenBy;

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
            networkBridge.RPC_SendMessageToAll((byte)CollectibleMessageType.grabber, writer.Data);
        }

        public void OnGrabDeselected(SelectExitEventArgs args)
        {
            if (!CanSendRpc) return;

            var writer = new BytesWriter(BytesWriter.ByteSize);
            writer.AddByte(0);
            networkBridge.RPC_SendMessageToAll((byte)CollectibleMessageType.grabber, writer.Data);
        }

        private void OnValidate()
        {
            if (networkBridge == null) networkBridge = GetComponent<NetworkBridge>();
            if (_grabInteractable == null) _grabInteractable = GetComponent<XRGrabInteractable>();
        }
    }

    /// <summary>Message ids are scoped per NetworkBridge — see SeedMessageType. Append new values,
    /// never insert.</summary>
    enum CollectibleMessageType : byte
    {
        stateSync = 0,
        grabber = 1,
        takeRequest = 2,
        taken = 3,
        restoredToDispenser = 4
    }
}
