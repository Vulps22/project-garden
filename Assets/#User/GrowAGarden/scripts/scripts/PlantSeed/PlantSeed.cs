using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace GrowAGarden
{

    public abstract class PlantSeed : MonoBehaviour, IHeldObject, IKinematicSource, IAuthoritySource, ILifecycleNotifier
    {

        [SerializeField] protected MeshRenderer _SeedModel;
        [SerializeField] protected Collider _SeedCollider;
        [SerializeField] public SeedDefinition seedDefinition;
        [SerializeField] public NetworkBridge networkBridge;
        [SerializeField] protected XRGrabInteractable _grabInteractable;

        public bool IsSeed { get; private set; }
        public bool InShop { get; private set; }
        public bool IsBought { get; private set; }
        public bool IsInPool { get; private set; }

        /// <summary>
        /// True from the moment this is offered over a sell point counter until the sale
        /// completes or is refused. It is not a lifecycle state of its own — it only suppresses
        /// rendering, so the plant leaves the world the instant it is handed over instead of
        /// dangling in mid-air for the round trips the sale takes.
        ///
        /// Deliberately kept out of the stateSync payload: it lives for well under a second, and
        /// adding it would change the wire size of every seed broadcast for a window a late
        /// joiner would have to be extraordinarily unlucky to land in.
        /// </summary>
        public bool SalePending { get; private set; }

        /// <summary>Fusion's id for this object, for addressing it in another object's RPC.</summary>
        public uint NetworkId => networkBridge?.Object == null ? 0u : networkBridge.Object.Id.Raw;
        /// <summary>Raised after any lifecycle transition so behaviours can re-evaluate.</summary>
        public event System.Action LifecycleChanged;

        /// <summary>
        /// Raised on every client when the holder asks to buy this seed. Only the master acts
        /// on it; see <see cref="RequestPurchase"/>.
        /// </summary>
        public event System.Action<PlantSeed> PurchaseRequested;

        /// <summary>
        /// The seed's physical mode, derived from its lifecycle rather than remembered.
        /// Every state is kinematic today, so this changes no behaviour yet — the shop and
        /// free-seed cases are what later phases flip, and they flip here, once.
        /// </summary>
        public bool ShouldBeKinematic
        {
            get
            {
                if (IsInPool) return true;   // parked out of the world
                if (!IsSeed) return true;    // planted, growing or grown — anchored to its slot
                if (InShop) return false;    // physical; recalled to the slot when asked
                return false;                // free seed — falls, lands and floats
            }
        }

        /// <summary>
        /// Only a bought seed loose in the world has weight. Shop stock hangs at its slot, so
        /// the tether holds position instead of fighting a constant downward pull; everything
        /// else is kinematic, where gravity is ignored anyway.
        ///
        /// </summary>
        public bool ShouldUseGravity => IsSeed && !IsInPool && !InShop;

        /// <summary>
        /// Stock belongs to the world, not to whoever last touched it. A seed nobody is
        /// holding, sitting in the pool or in a shop slot, should be owned by the master.
        /// The holder check is what stops the master claiming a seed out of a player's hand.
        /// </summary>
        public bool ShouldMasterOwn => (IsInPool || InShop) && string.IsNullOrEmpty(HolderId);

        private HoveringEntity _hovering;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        protected long _plantedTimestamp;
        protected PlayerBalance _grabber;
        protected PlantSlot _occupiedSlot;
        protected int _growthPhase = 0;

        /// <summary>
        /// A bought seed loose in the world and nobody holding it. The one condition that
        /// decides whether it floats.
        /// </summary>
        private bool IsLooseInWorld => IsSeed && !IsInPool && !InShop && !IsHeld;

        /// <summary>
        /// Drives the hovering component from lifecycle changes.
        ///
        /// PlantSeed decides because PlantSeed is the only thing that knows: it owns the
        /// lifecycle flags and the grab callbacks. Doing it in one handler rather than sprinkling
        /// Begin/Stop through Plant, Sell, ReturnToPool and the grab callbacks means there is a
        /// single place that can be wrong, and no exit path that can forget.
        /// </summary>
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
        /// Who is holding this, for <see cref="SingleHolderFilter"/>. Null when free.
        /// </summary>
        public string HolderId => _grabber?.GetID();

        /// <summary>
        /// True while an interactor actually has hold of this. A seed that merely got knocked
        /// out of a slot is not held, and must not be treated as a purchase.
        /// </summary>
        public bool IsHeld => _grabInteractable != null && _grabInteractable.isSelected;

        /// <summary>
        /// True when some client owns this object. Authority is None between the owner leaving
        /// and SceneNetworking reassigning it, and state changes made in that window go
        /// nowhere, so a sale decided there would not be broadcast to anyone.
        /// </summary>
        /// <summary>
        /// True when THIS client is the object's state authority. Distinct from
        /// <see cref="HasKnownAuthority"/>, which only says somebody owns it.
        /// </summary>
        public bool HasLocalAuthority =>
            networkBridge != null
            && networkBridge.Object != null
            && networkBridge.Object.HasStateAuthority;

        public bool HasKnownAuthority =>
            networkBridge != null
            && networkBridge.Object != null
            && !networkBridge.Object.StateAuthority.IsNone;

        /// <summary>
        /// Cancels an in-progress grab. Disabling the interactable makes XRI end the select,
        /// which raises selectExited and so clears and re-broadcasts the grabber through the
        /// normal path. Whoever calls this is responsible for re-enabling the grab.
        /// </summary>
        /// <summary>
        /// Asks the master to sell this seed to whoever is holding it.
        ///
        /// Called by the holder's own client, because the holder is the only one that *knows*
        /// the seed was deliberately taken rather than knocked loose. The master used to infer
        /// that from replicated state and got it wrong whenever the grab had not replicated
        /// yet — an explicit request has no such race.
        ///
        /// Sent to all rather than to the controller: the controller of a held seed is the
        /// holder itself, and it is the master that has to decide. Everyone else ignores it.
        /// </summary>
        public void RequestPurchase()
        {
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.purchaseRequest, new byte[0]);
        }

        public void ForceRelease()
        {
            if (_grabInteractable != null) _grabInteractable.enabled = false;
        }

        /// <summary>
        /// Subscribes to network and interaction events.
        /// </summary>
        protected virtual void Start()
        {
            _hovering = GetComponent<HoveringEntity>();
            LifecycleChanged += UpdateHovering;

            networkBridge.OnSpawned += OnSpawned;
            networkBridge.OnStateAuthorityChanged += OnStateAuthorityChanged;
            networkBridge.OnMessageToAll += OnMessageToAll;
            networkBridge.OnMessageToProxies += OnMessageToProxies;
            SceneNetworking.OnOtherPlayerJoined += OnOtherPlayerJoined;
            _grabInteractable.selectEntered.AddListener(OnGrabSelected);
            _grabInteractable.selectExited.AddListener(OnGrabDeselected);
        }

        /// <summary>
        /// Unsubscribes from all events to prevent memory leaks.
        /// </summary>
        /// <summary>
        /// Re-sends state once a transfer lands, but only on the master.
        ///
        /// The client that has just *gained* authority is the client that knows least about the
        /// object: it was a proxy a moment ago, and its lifecycle flags are whatever last
        /// reached it. Letting it broadcast made it assert that stale copy over everyone else's
        /// — a buyer taking authority on grab pushed InShop=true back to the master and undid
        /// the sale before it was even requested, and OnOtherPlayerJoined re-ran that for every
        /// carried seed on every join.
        ///
        /// The master is the one client whose lifecycle view is authoritative, so it is the only
        /// one allowed to re-assert. Everyone else adopts what it is given; growth state needs no
        /// re-send, because it is derived from a timestamp the new authority already received.
        /// </summary>
        private void OnStateAuthorityChanged(bool hasAuthority)
        {
            if (hasAuthority && SceneNetworking.IsMasterClient) broadcastState();
        }

        protected virtual void OnDestroy()
        {
            LifecycleChanged -= UpdateHovering;
            networkBridge.OnStateAuthorityChanged -= OnStateAuthorityChanged;
            SceneNetworking.OnOtherPlayerJoined -= OnOtherPlayerJoined;
            _grabInteractable.selectEntered.RemoveListener(OnGrabSelected);
            _grabInteractable.selectExited.RemoveListener(OnGrabDeselected);
        }

        /// <summary>
        /// Called by NetworkBridge once the Fusion NetworkObject has spawned. Authority client sets initial seed state.
        /// </summary>
        private void OnSpawned()
        {
            if (networkBridge.Object.HasStateAuthority)
                SetState(true);
        }

        /// <summary>
        /// When a new player joins, authority client broadcasts current state so the new client is in sync.
        /// </summary>
        private void OnOtherPlayerJoined(PlayerRef player)
        {
            broadcastState();   
        }

        /// <summary>
        /// Sends full state (seed/plant flags, timestamp) to all proxy clients via RPC. Authority only.
        /// </summary>
        public void broadcastState()
        {
            if (networkBridge.Object == null) return;
            if (!networkBridge.Object.HasStateAuthority) return;

            BytesWriter writer = new BytesWriter(BytesWriter.ByteSize * 4 + BytesWriter.IntSize * 2 + GetExtraBroadcastStateSize());
            writer.AddByte(IsSeed ? (byte)1 : (byte)0);
            writer.AddByte(InShop ? (byte)1 : (byte)0);
            writer.AddByte(IsBought ? (byte)1 : (byte)0);
            writer.AddByte(IsInPool ? (byte)1 : (byte)0);
            writer.AddInt((int)(_plantedTimestamp >> 32));
            writer.AddInt((int)(_plantedTimestamp & 0xFFFFFFFFL));
            OnWriteBroadcastState(writer);
            networkBridge.RPC_SendMessageToProxies((byte)PlantMessageType.stateSync, writer.Data);
        }

        protected virtual int GetExtraBroadcastStateSize() => 0;
        protected virtual void OnWriteBroadcastState(BytesWriter writer) { }
        protected virtual void OnReadBroadcastState(BytesReader reader) { }

        /// <summary>
        /// Routes trigger enter events — only handles planting when in seed state.
        /// </summary>
        void OnTriggerEnter(Collider other)
        {
            if (IsSeed)
                OnTriggerEnterSeed(other);
        }

        /// <summary>
        /// Handles planting when the seed enters a PlantSlot trigger. Authority only.
        /// </summary>
        private void OnTriggerEnterSeed(Collider other)
        {
            if (!networkBridge.Object.HasStateAuthority) return;

            PlantSlot slot = other.GetComponent<PlantSlot>();
            if (slot == null || slot.IsOccupied) return;

            Plant(slot);
        }

        /// <summary>
        /// Frees the PlantSlot when a fully grown plant leaves it.
        /// </summary>
        void OnTriggerExit(Collider other)
        {
            if (IsSeed || GetGrowthCompletion() < 1f) return;

            // Authority only, to match planting. SetOccupied broadcasts to everyone, so without
            // this any client whose local physics raised an exit could free a slot for the whole
            // world — and Unity raises phantom exits whenever isKinematic, detectCollisions or a
            // collider's enabled state changes, with the plant standing still.
            var obj = networkBridge == null ? null : networkBridge.Object;
            if (obj == null || !obj.HasStateAuthority) return;

            PlantSlot slot = other.GetComponent<PlantSlot>();
            if (slot == null) return;

            // Free the slot this plant is actually in, not whichever one it was carried past.
            // _occupiedSlot is unknown after an authority transfer, so fall back to trusting the
            // trigger rather than risk a slot that can never be freed again.
            if (_occupiedSlot != null && slot != _occupiedSlot) return;

            slot.SetOccupied(false);
        }

        // ── Lifecycle action methods ──────────────────────────────────────────────
        // Each method owns its state changes and any required broadcast.
        // External callers should use these instead of mutating fields directly.

        /// <summary>
        /// Puts this on, or takes it off, the sell point's counter. Every client runs this so the
        /// plant disappears everywhere at once; SellPoint drives it from its own RPCs.
        /// </summary>
        public void SetSalePending(bool pending)
        {
            if (SalePending == pending) return;
            SalePending = pending;
            SetState(IsSeed);          // re-derives visibility through the rule in SetState

            // HideForPool() turned the grab off on the way in and UpdateVisuals() does not turn it
            // back on, so without this a refused sale hands back a plant that is visible and
            // completely untouchable. Derived rather than remembered: grabbable is exactly
            // "present in the world, and either a seed or a finished plant".
            if (!pending && _grabInteractable != null)
                _grabInteractable.enabled = !IsInPool && (IsSeed || GetGrowthCompletion() >= 1f);

            LifecycleChanged?.Invoke();
        }

        /// <summary>
        /// Marks this seed as claimed from the pool. Call before PlaceInShop().
        /// </summary>
        public void Claim()
        {
            IsInPool = false;
            LifecycleChanged?.Invoke();
        }

        /// <summary>
        /// Positions this seed in a shop display and broadcasts the state to all clients.
        /// Master client only.
        /// </summary>
        public void PlaceInShop(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
            RestoreToShop();
        }

        /// <summary>
        /// Puts this back into shop-stock state where it currently stands, without moving it.
        /// Used when a seed is being returned to its slot under its own steam — teleporting it
        /// would defeat the point of gliding it back.
        /// </summary>
        public void RestoreToShop()
        {
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.restoredToShop, new byte[0]);
        }

        /// <summary>
        /// Applies shop-stock state locally. Runs on every client from the RPC above, including
        /// the sender — see <see cref="Purchase"/> for why this is not done at the call site.
        /// </summary>
        private void ApplyRestoreToShop()
        {
            // Flags first. SetState() raises LifecycleChanged itself, so setting them
            // afterwards published one event describing a seed that was neither in the shop
            // nor bought — a free seed, as far as every listener could tell. KinematicController
            // and AuthorityController both act on that, and ShopSlot now does too.
            InShop = true;
            IsBought = false;
            SetState(true);

            // A seed that is stock again is, by definition, in nobody's hand. Disabling the
            // interactable cancels any select in progress, and because this runs from an RPC it
            // runs on the holder's machine too — which is what actually takes it back. Doing it
            // master-side only moved the master's own copy and left the buyer still carrying it.
            _grabInteractable.enabled = false;

            // Then re-derive: grabbable unless an enforced recall is carrying it home, in which
            // case ReturnableEntity owns the interactable until it arrives.
            var ret = GetComponent<ReturnableEntity>();
            if (ret == null || !ret.IsReturning) _grabInteractable.enabled = true;

            Logger.Info($"ApplyRestoreToShop() '{gameObject.name}' — InShop={InShop} IsSeed={IsSeed} IsInPool={IsInPool} grab={_grabInteractable.enabled} authority={HasLocalAuthority}");

            LifecycleChanged?.Invoke();
            broadcastState();   // resync for whoever owns it; a no-op everywhere else
        }

        /// <summary>
        /// Marks this seed as sold to whoever is holding it. Master client only.
        ///
        /// Announced by RPC rather than written locally and pushed with broadcastState(), because
        /// the master is almost never this object's state authority when it decides a sale:
        /// NetworkGrabbable requests authority the moment a player grabs, so by the time the seed
        /// leaves the slot the buyer owns it. broadcastState() self-gates on HasStateAuthority,
        /// so the master's three flag writes stayed on the master — silently, with no log and no
        /// throw. The seed went on reading InShop=true, IsBought=false everywhere, which is what
        /// let a paid-for seed be re-latched as shop stock and dragged back out of a plot.
        ///
        /// RPC_SendMessageToAll is RpcSources.All, so any client may send it regardless of
        /// authority. Sell() has always worked this way; this is the same pattern.
        /// </summary>
        public void Purchase()
        {
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.purchased, new byte[0]);
        }

        /// <summary>Applies purchased state locally. Runs on every client from the RPC above.</summary>
        private void ApplyPurchase()
        {
            InShop = false;
            IsBought = true;
            ClearShopClaim();
            LifecycleChanged?.Invoke();
            broadcastState();   // resync for whoever owns it; a no-op everywhere else
        }

        /// <summary>
        /// Lets go of any shop slot's claim on this seed.
        ///
        /// ReturnableEntity and AlignableEntity are armed by ShopSlot.AssignSlot on the master
        /// alone, and ShopSlot.ReleaseSlot was the only thing that ever cleared them — on the
        /// successful-purchase path only. Every other way a seed stops being stock (planting,
        /// above all) left _autoRecall true and a live return target pointing at the shop, so
        /// the slot would drag the seed back out of the plot it had just been planted in, and
        /// keep doing it.
        ///
        /// Runs on every client because each one has to undo its own local components, and it is
        /// idempotent so the paths that overlap can both call it.
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

        /// <summary>
        /// Plants this seed into a slot — transitions to plant state, starts growth timer,
        /// disables grab, and notifies all clients. State authority only.
        /// </summary>
        public void Plant(PlantSlot slot)
        {
            _occupiedSlot = slot;
            transform.position = slot.transform.position;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.zero;
            SetState(false);
            _plantedTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            slot.SetOccupied(true);
            _grabInteractable.enabled = false;
            ClearShopClaim();
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.disable, new byte[0]);
            broadcastState();
        }

        /// <summary>
        /// Sells this plant — broadcasts to all clients. Each client resets local state;
        /// the authority additionally returns it to the pool. Master client only.
        /// </summary>
        public void Sell()
        {
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.sold, new byte[0]);
        }

        /// <summary>
        /// Resets this plant back into the pool at the given position and broadcasts the reset.
        /// State authority only.
        /// </summary>
        public void ReturnToPool(Vector3 position, Quaternion rotation)
        {
            IsInPool = true;
            transform.position = position;
            transform.rotation = rotation;
            transform.localScale = Vector3.one;
            OnReturnedToPool();                 // before SetState: it may change what visuals see
            ClearShopClaim();
            SalePending = false;                // the counter is clear; IsInPool hides it from here
            SetState(true);                     // KinematicController owns isKinematic now
            broadcastState();
        }

        /// <summary>
        /// Last chance to reset anything a subclass carries that outlives one life.
        /// Called before the pooled state is applied, so what is written here is what the next
        /// claim of this instance starts from.
        /// </summary>
        protected virtual void OnReturnedToPool() { }

        /// <summary>
        /// Drives growth scaling each frame. Authority only. Calls OnGrowthUpdated for subclass scale logic,
        /// and OnFullyGrown when the current phase completes.
        /// </summary>
        private void Update()
        {
            OnWillUpdate();

            if (IsSeed) { return; }
            if (networkBridge.Object == null) { return; }
            if (!networkBridge.Object.HasStateAuthority) { return; }

            float completion = GetGrowthCompletion();
            float progress = seedDefinition.phases[_growthPhase].isDecay ? (1f - completion) : completion;
            float targetScale = progress * seedDefinition.phases[_growthPhase].maxScale * seedDefinition.phases[_growthPhase].scaleMultiplier;
            OnGrowthUpdated(completion, targetScale);
            if (completion >= 1f && _grabInteractable != null && !_grabInteractable.enabled)
            {
                OnFullyGrown();
            }
        }

        protected virtual void OnWillUpdate() { }

        /// <summary>
        /// Called each frame while growing. Default applies targetScale to the root transform.
        /// Override in subclasses to redirect scale to a specific child (e.g. vine or fruit).
        /// </summary>
        protected virtual void OnGrowthUpdated(float completion, float targetScale)
        {
            transform.localScale = Vector3.one * targetScale;
        }

        /// <summary>
        /// Called when the current growth phase completes. Default enables grab and notifies all clients.
        /// Override in subclasses to handle multi-phase transitions (e.g. vine → fruit).
        /// </summary>
        protected virtual void OnFullyGrown()
        {
            _grabInteractable.enabled = true;
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.enable, new byte[0]);
        }

        /// <summary>
        /// Returns growth completion [0,1] for the current phase based on elapsed time since _plantedTimestamp.
        /// </summary>
        public float GetGrowthCompletion()
        {
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            float raw = (now - _plantedTimestamp) / seedDefinition.phases[_growthPhase].duration;
            return Mathf.Clamp01(raw);
        }

        /// <summary>
        /// Set the visual/collider state of this object. Pure local operation — no broadcast.
        /// </summary>
        public void SetState(bool isSeed)
        {
            IsSeed = isSeed;

            // UpdateVisuals only distinguishes seed from plant — it has no notion of a seed
            // that should not be in the world at all. Hooking the pool case in here catches
            // every path that changes visual state (authority, proxy sync, spawn, sale)
            // rather than relying on each of them to remember.
            // Pooled and sale-pending are both "not present in the world", and both have to beat
            // UpdateVisuals. Without the SalePending term the sold RPC's SetState(true) would run
            // UpdateVisuals and pop the plant back into view as a seed for the couple of hundred
            // milliseconds until it reaches the pool.
            if (IsInPool || SalePending) HideForPool();
            else UpdateVisuals(isSeed);

            LifecycleChanged?.Invoke();
        }

        /// <summary>
        /// A pooled seed is not present in the world: no visuals, no colliders, no grab.
        /// Without this the pools are visible piles of grabbable seeds sitting at the pool
        /// transforms, which breaks the invariant that a seed outside a shop slot has been
        /// bought — and would leave them free-falling once seeds get gravity.
        /// Leaving the pool goes back through UpdateVisuals(), which re-derives the correct
        /// renderer and collider set for the subclass.
        /// </summary>
        private void HideForPool()
        {
            _renderers ??= GetComponentsInChildren<Renderer>(true);
            _colliders ??= GetComponentsInChildren<Collider>(true);

            foreach (Renderer r in _renderers) if (r != null) r.enabled = false;
            foreach (Collider c in _colliders) if (c != null) c.enabled = false;
            if (_grabInteractable != null) _grabInteractable.enabled = false;
        }

        /// <summary>
        /// Show or hide the appropriate models and colliders for seed vs plant state. Called when SetState is called;
        /// </summary>
        protected abstract void UpdateVisuals(bool IsSeed);

        /// <summary>
        /// Handles broadcast RPCs: enable/disable grab, grabber sync, sold state.
        /// </summary>
        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((PlantMessageType)id)
            {
                case PlantMessageType.enable:
                    // Never re-arm a pooled seed. A proxy whose state briefly disagrees with
                    // the authority could otherwise be handed a grabbable invisible object.
                    _grabInteractable.enabled = !IsInPool;
                    break;
                case PlantMessageType.disable:
                    SetState(false);
                    _grabInteractable.enabled = false;
                    // The shop's recall is armed on the master, and Plant() runs on the state
                    // authority — usually the buyer. Clearing it here is what actually reaches
                    // the machine holding the claim.
                    ClearShopClaim();
                    break;
                case PlantMessageType.grabber:
                    BytesReader grabReader = new BytesReader(data);
                    bool hasGrabber = grabReader.NextByte() == 1;
                    _grabber = hasGrabber ? EconomyManager.Instance.GetPlayer(grabReader.NextString()) : null;
                    // Who holds it is lifecycle. This is the one place _grabber changes on
                    // every client, so raising here lets AuthorityController reclaim a shop
                    // seed the moment it leaves a player's hand.
                    LifecycleChanged?.Invoke();
                    break;
                case PlantMessageType.purchaseRequest:
                    // Raised everywhere; ShopSlot only listens on the master.
                    PurchaseRequested?.Invoke(this);
                    break;
                case PlantMessageType.purchased:
                    ApplyPurchase();
                    break;
                case PlantMessageType.restoredToShop:
                    ApplyRestoreToShop();
                    break;
                case PlantMessageType.sold:
                    // Resets local state only. Getting the plant into storage is the sell point's
                    // job now — it is the client that owns the object by this point, and a plant
                    // walking itself into the back room was how the authority split got hidden.
                    _grabber = null;
                    _occupiedSlot = null;
                    SetState(true);
                    _grabInteractable.enabled = false;
                    break;
                default:
                    Logger.Warn($"OnMessageToAll() '{gameObject.name}' — received unknown message id={id}");
                    break;
            }
        }

        /// <summary>
        /// Handles stateSync RPC on proxy clients — restores all flags, timestamp, and visual state.
        /// </summary>
        private void OnMessageToProxies(byte id, byte[] data)
        {
            if ((PlantMessageType)id != PlantMessageType.stateSync) return;

            BytesReader reader = new BytesReader(data);
            bool isSeed = reader.NextByte() == 1;
            InShop = reader.NextByte() == 1;
            IsBought = reader.NextByte() == 1;
            IsInPool = reader.NextByte() == 1;
            long high = reader.NextInt();
            long low = (uint)reader.NextInt();
            _plantedTimestamp = (high << 32) | low;

            SetState(isSeed);
            OnReadBroadcastState(reader);
            if (IsInPool)
                _grabInteractable.enabled = false;   // SetState() already hid it; do not undo that
            else if (isSeed)
                _grabInteractable.enabled = true;
            else
                _grabInteractable.enabled = GetGrowthCompletion() >= 1f;
        }

        /// <summary>
        /// Enables or disables the grab interactable. Used by ShopSlot to gate purchase based on player balance.
        /// </summary>
        public void SetGrabEnabled(bool enabled)
        {
            if (_grabInteractable != null)
                _grabInteractable.enabled = enabled;
        }

        public PlayerBalance GetGrabber() => _grabber;

        /// <summary>
        /// Broadcasts to all clients that this player is now holding the object, used to block steal attempts.
        /// </summary>
        public void OnGrabSelected(SelectEnterEventArgs args)
        {
            // Reachable even when Process() would have refused: bought seeds short-circuit
            // the affordability check. Without a known local balance there is no id to
            // broadcast, so leave _grabber null — the other call sites now tolerate that.
            _grabber = EconomyManager.Instance == null
                ? null
                : EconomyManager.Instance.GetLocalPlayer();
            if (_grabber == null)
            {
                Logger.Warn($"OnGrabSelected() '{gameObject.name}' — local balance unavailable, grabber not broadcast");
                return;
            }

            string id = _grabber.GetID();
            int size = BytesWriter.ByteSize + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(id);
            var writer = new BytesWriter(size);
            writer.AddByte(1);
            writer.AddString(id);
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.grabber, writer.Data);
        }

        /// <summary>
        /// Broadcasts to all clients that this object is no longer held.
        /// </summary>
        public void OnGrabDeselected(SelectExitEventArgs args)
        {
            var writer = new BytesWriter(BytesWriter.ByteSize);
            writer.AddByte(0);
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.grabber, writer.Data);
        }

        /// <summary>
        /// Auto-populates NetworkBridge and XRGrabInteractable references in the Inspector if not set.
        /// </summary>
        private void OnValidate()
        {
            if (networkBridge == null)
                networkBridge = GetComponent<NetworkBridge>();
            if (_grabInteractable == null)
                _grabInteractable = GetComponent<XRGrabInteractable>();
        }
    }

    enum PlantMessageType
    {
        enable,
        disable,
        sold,
        stateSync,
        grabber,
        vineAnchor,
        vineDecayStart,
        // Appended rather than inserted: these are wire ids, so renumbering an existing
        // value would make two builds disagree about what a message means.
        purchaseRequest,
        purchased,
        restoredToShop
    }
}
