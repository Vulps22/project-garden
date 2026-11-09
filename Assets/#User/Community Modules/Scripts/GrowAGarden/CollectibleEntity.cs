using Fusion;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Vulps.GrowAGarden.Interaction;
using Vulps.GrowAGarden.Motion;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden
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

        [SerializeField] public NetworkMessenger networkBridge;
        [SerializeField] protected XRGrabInteractable _grabInteractable;

        [Tooltip("Placed in the scene as dispenser stock (a Plot's deed): every client loads it in its dispenser, so no " +
                 "message is needed to say so.")]
        [SerializeField] private bool _startsInDispenser;

        /// <summary>Out of the game but still in the world: a claimed Plot's deed. Unseen, ungrabbable, colliderless and
        /// held still, put straight back in its socket.</summary>
        public bool IsHidden { get; private set; }
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private bool[] _wasEnabled;

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

        /// <summary>Raised on every client whenever one learns a collectible is dispenser stock: its socket's owner adopts it.
        /// The socket's own trigger fires as the item spawns, before that state arrives, so it cannot be relied on.</summary>
        public static event System.Action<CollectibleEntity> AnyInDispenser;

        /// <summary>Raised on every client when the holder asks to take it. Only the master acts.</summary>
        public event System.Action<CollectibleEntity> TakeRequested;

        /// <summary>A collectible is a physical object wherever it is, unless hidden: then nothing should move it.</summary>
        public bool ShouldBeKinematic => IsHidden;

        /// <summary>Dispenser stock hangs at its socket, so the tether holds position instead of
        /// fighting a constant downward pull. A taken one loose in the world has weight.</summary>
        public bool ShouldUseGravity => !InDispenser;

        /// <summary>Stays with a hand while held; otherwise a player in its sector simulates it.</summary>
        public AuthorityRule Authority => string.IsNullOrEmpty(HolderId) ? AuthorityRule.Sector : AuthorityRule.Holder;

        private HoveringEntity _hovering;
        /// <summary>
        /// Who has hold of this, as Somnium knows them. Identity, not economy — this used to be a
        /// PlayerBalance, which meant an id the balance table had not heard of read back as
        /// "nobody is holding it" and handed the object to whoever grabbed next.
        /// </summary>
        private PlayerIdentity _grabber;

        private bool IsLooseInWorld => !InDispenser && !IsHeld;

        public string HolderId => _grabber.Id;
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
            if (_startsInDispenser) { InDispenser = true; _stateKnown = true; }   // the scene is the same on every client
            _hovering = GetComponent<HoveringEntity>();
            // A recall that suspended the grab hands it back on arrival: not to a hidden one.
            var returnable = GetComponent<ReturnableEntity>();
            if (returnable != null) returnable.Arrived += () => { if (IsHidden && _grabInteractable != null) _grabInteractable.enabled = false; };
            LifecycleChanged += UpdateHovering;

            networkBridge.OnSpawned += OnSpawned;
            networkBridge.OnStateAuthorityChanged += OnStateAuthorityChanged;
            networkBridge.MessageToAll += OnMessageToAll;
            networkBridge.MessageToProxies += OnMessageToProxies;
            PlayerManager.OtherPlayerJoined += OnOtherPlayerJoined;
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
                networkBridge.MessageToAll -= OnMessageToAll;
                networkBridge.MessageToProxies -= OnMessageToProxies;
            }
            PlayerManager.OtherPlayerJoined -= OnOtherPlayerJoined;
            PlayerManager.PlayerLeft -= OnPlayerLeft;
            _grabInteractable.selectEntered.RemoveListener(OnGrabSelected);
            _grabInteractable.selectExited.RemoveListener(OnGrabDeselected);
        }

        /// <summary>A player who has left is holding nothing — see Seed.OnPlayerLeft.</summary>
        private void OnPlayerLeft(string playerId)
        {
            if (!_grabber.Exists || _grabber.Id != playerId) return;
            _grabber = PlayerIdentity.None;
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
            if (hasAuthority && PlayerManager.IsMaster) broadcastState();
        }

        // This client holds the real state: it spawned this, or a state sync has arrived. A blank copy is never broadcast.
        private bool _stateKnown;

        private void OnSpawned()
        {
            if (networkBridge.Object.HasStateAuthority) { _stateKnown = true; LifecycleChanged?.Invoke(); }
        }

        private void OnOtherPlayerJoined() => broadcastState();

        // ── Dispenser ─────────────────────────────────────────────────────────────

        /// <summary>Asks the master to hand this to whoever is holding it — see
        /// Seed.RequestPurchase for why the holder is the one asking.</summary>
        public void RequestTake()
        {
            networkBridge.SendToAll((byte)CollectibleMessageType.takeRequest, new byte[0]);
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
            networkBridge.SendToAll((byte)CollectibleMessageType.restoredToDispenser, new byte[0]);
        }

        private void ApplyRestoreToDispenser()
        {
            _stateKnown = true;   // this message sets the whole state
            InDispenser = true;
            IsTaken = false;
            TakenBy = null;

            _grabInteractable.enabled = false;

            var ret = GetComponent<ReturnableEntity>();
            if (!IsHidden && (ret == null || !ret.KeepGrabUntilArrival())) _grabInteractable.enabled = true;

            Logger.Info($"ApplyRestoreToDispenser() '{gameObject.name}' — InDispenser={InDispenser} grab={_grabInteractable.enabled} authority={HasLocalAuthority}");

            LifecycleChanged?.Invoke();
            AnyInDispenser?.Invoke(this);
            broadcastState();
        }

        /// <summary>Given to the named player. Announced by RPC rather than broadcastState() —
        /// see Seed.Purchase, same authority-timing reason.</summary>
        public void Take(string takerId)
        {
            int size = sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(takerId ?? string.Empty);
            var writer = new BytesWriter(size);
            writer.AddAutoString(takerId ?? string.Empty);
            networkBridge.SendToAll((byte)CollectibleMessageType.taken, writer.Data);
        }

        private void ApplyTaken(byte[] data)
        {
            var reader = new BytesReader(data);
            _stateKnown = true;   // this message sets the whole state
            TakenBy = reader.IsValid ? reader.NextAutoString() : null;
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

        /// <summary>
        /// Master only: ends this wherever it is. Announced, because despawning needs state authority and the player
        /// holding it (a deed being claimed, a scroll being accepted) has it; the same shape as Seed.RequestDiscard.
        /// </summary>
        public bool Discard()
        {
            if (!PlayerManager.IsMaster || !CanSendRpc) return false;
            ForceRelease();
            networkBridge.SendToAll((byte)CollectibleMessageType.discard, new byte[0]);
            return true;
        }

        private void ApplyDiscard()
        {
            ForceRelease();
            NetworkObject obj = networkBridge == null ? null : networkBridge.Object;
            if (WorldManager.DespawnIfStateAuthority(obj)) return;
            if (PlayerManager.IsMaster && !HasKnownAuthority) StartCoroutine(TakeAuthorityAndDiscard());
        }

        /// <summary>Nobody simulates this, so nobody would despawn it: the master takes it and does.</summary>
        private System.Collections.IEnumerator TakeAuthorityAndDiscard()
        {
            NetworkObject obj = networkBridge == null ? null : networkBridge.Object;
            bool granted = false;
            yield return WorldManager.TakeAuthority(obj, r => granted = r);
            if (granted) WorldManager.DespawnIfStateAuthority(obj);
            else Logger.Warn($"TakeAuthorityAndDiscard() '{gameObject.name}' — no authority within {WorldManager.AuthorityTimeout}s; left standing");
        }

        // ── State ─────────────────────────────────────────────────────────────────

        public void broadcastState()
        {
            if (networkBridge.Object == null) return;
            if (!networkBridge.Object.HasStateAuthority || !_stateKnown) return;

            string takenBy = TakenBy ?? string.Empty;
            int size = BytesWriter.ByteSize * 2 + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(takenBy);
            BytesWriter writer = new BytesWriter(size);
            writer.AddByte(InDispenser ? (byte)1 : (byte)0);
            writer.AddByte(IsTaken ? (byte)1 : (byte)0);
            writer.AddAutoString(takenBy);
            networkBridge.SendToProxies((byte)CollectibleMessageType.stateSync, writer.Data);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((CollectibleMessageType)id)
            {
                case CollectibleMessageType.grabber:
                    BytesReader grabReader = new BytesReader(data);
                    bool hasGrabber = grabReader.NextByte() == 1;
                    // Temporary instrumentation, mirroring Seed's. Seed and Produce have logged
                    // this since the holder rework; CollectibleEntity never did, so for deeds and
                    // scrolls — the objects the plot-ownership work is actually about — whether
                    // the master ever learned who was holding one was invisible in the client log.
                    // Two rounds of diagnosis were spent reasoning from the call graph because of
                    // it. Delete alongside Seed's.
                    string grabberId = hasGrabber ? grabReader.NextAutoString() : null;
                    _grabber = hasGrabber ? PlayerManager.GetPlayer(grabberId) : PlayerIdentity.None;
                    Logger.Info($"OnMessageToAll() '{gameObject.name}' — grabber id='{grabberId ?? "<none>"}' resolved={_grabber.Exists} HolderId='{HolderId ?? "<null>"}' authority={HasLocalAuthority}");
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
                case CollectibleMessageType.discard:
                    ApplyDiscard();
                    break;
                default:
                    break;   // associateRequest/associated/acceptRequest are PlotApplication's and puff is SmokeableEntity's, on the same bridge
            }
        }

        private void OnMessageToProxies(byte id, byte[] data)
        {
            if ((CollectibleMessageType)id != CollectibleMessageType.stateSync) return;
            _stateKnown = true;

            BytesReader reader = new BytesReader(data);
            InDispenser = reader.NextByte() == 1;
            IsTaken = reader.NextByte() == 1;
            string takenBy = reader.NextAutoString();
            TakenBy = string.IsNullOrEmpty(takenBy) ? null : takenBy;

            _grabInteractable.enabled = !IsHidden;
            LifecycleChanged?.Invoke();
            if (InDispenser) AnyInDispenser?.Invoke(this);
        }

        /// <summary>Hides or shows this on this client only: each client calls it from the same replicated fact.</summary>
        public void SetHidden(bool hidden)
        {
            if (IsHidden == hidden) return;
            IsHidden = hidden;
            if (_grabInteractable != null) _grabInteractable.enabled = !hidden;   // first: a hand holding it lets go
            _renderers ??= GetComponentsInChildren<Renderer>(true);
            if (_colliders == null)
            {
                _colliders = GetComponentsInChildren<Collider>(true);
                _wasEnabled = new bool[_colliders.Length];
                for (int i = 0; i < _colliders.Length; i++) _wasEnabled[i] = _colliders[i].enabled;
            }
            foreach (Renderer r in _renderers) if (r != null) r.enabled = !hidden;
            for (int i = 0; i < _colliders.Length; i++) if (_colliders[i] != null) _colliders[i].enabled = !hidden && _wasEnabled[i];
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

        public PlayerIdentity GetGrabber() => _grabber;

        public void OnGrabSelected(SelectEnterEventArgs args)
        {
            _grabber = PlayerManager.GetLocalPlayer();
            if (!_grabber.Exists)
            {
                Logger.Warn($"OnGrabSelected() '{gameObject.name}' — local player unavailable, grabber not broadcast");
                return;
            }

            if (!CanSendRpc) return;

            string id = _grabber.Id;
            int size = BytesWriter.ByteSize + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(id);
            var writer = new BytesWriter(size);
            writer.AddByte(1);
            writer.AddAutoString(id);
            networkBridge.SendToAll((byte)CollectibleMessageType.grabber, writer.Data);
        }

        public void OnGrabDeselected(SelectExitEventArgs args)
        {
            if (!CanSendRpc) return;

            var writer = new BytesWriter(BytesWriter.ByteSize);
            writer.AddByte(0);
            networkBridge.SendToAll((byte)CollectibleMessageType.grabber, writer.Data);
        }

        private void OnValidate()
        {
            if (networkBridge == null) networkBridge = GetComponent<NetworkMessenger>();
            if (_grabInteractable == null) _grabInteractable = GetComponent<XRGrabInteractable>();
        }
    }

    /// <summary>Message ids are scoped per NetworkMessenger — see SeedMessageType. Append new values,
    /// never insert. 5-7 belong to PlotApplication, a sibling component on the same bridge for
    /// prefabs that need "which Plot is this application for" on top of the base take/holder
    /// shape — kept in this shared enum rather than a second one, since two independently-numbered
    /// enums on one bridge is exactly the collision class of bug this project has already hit.</summary>
    enum CollectibleMessageType : byte
    {
        stateSync = 0,
        grabber = 1,
        takeRequest = 2,
        taken = 3,
        restoredToDispenser = 4,
        associateRequest = 5,
        associated = 6,
        acceptRequest = 7,
        puff = 8,              // SmokeableEntity's, on the same bridge
        discard = 9            // the master ends it; whoever has authority despawns it
    }
}
