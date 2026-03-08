using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace GrowAGarden
{

    public abstract class PlantSeed : MonoBehaviour, IXRSelectFilter
    {

        [SerializeField] protected MeshRenderer _SeedModel;
        [SerializeField] protected Collider _SeedCollider;
        [SerializeField] public SeedDefinition seedDefinition;
        [SerializeField] public NetworkBridge networkBridge;
        [SerializeField] protected XRGrabInteractable _grabInteractable;

        public bool IsSeed = false;
        public bool InShop = false;
        public bool IsBought = false;
        public bool IsInPool = false;
        protected long _plantedTimestamp;
        protected PlayerBalance _grabber;
        protected PlantSlot _occupiedSlot;
        protected int _growthPhase = 0;

        public bool canProcess => true;

        /// <summary>
        /// IXRSelectFilter — called before a grab is committed. Returning false prevents the grab entirely.
        /// Used to block steal attempts: if _grabber is already set, someone else is holding this object.
        /// And to prevent users buying seeds they cannot afford
        /// </summary>
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (IsBought) return true;
            PlayerBalance localPlayer = EconomyManager.Instance.GetLocalPlayer();
            bool isHolder = _grabber != null && _grabber.GetID() == localPlayer.GetID();
            bool grabberFree = _grabber == null || isHolder;
            bool canAfford = !InShop || localPlayer.GetBalance() >= seedDefinition.buyPrice;
            bool result = grabberFree && canAfford;
            Logger.Info($"Process() '{gameObject.name}' — grabber={(_grabber != null ? _grabber.GetID() : "null")}, isHolder={isHolder}, InShop={InShop}, balance={localPlayer.GetBalance()}, buyPrice={seedDefinition.buyPrice}, canAfford={canAfford}, result={result}");
            return result;
        }

        /// <summary>
        /// Subscribes to network and interaction events.
        /// </summary>
        protected virtual void Start()
        {
            networkBridge.OnSpawned += OnSpawned;
            networkBridge.OnMessageToAll += OnMessageToAll;
            networkBridge.OnMessageToProxies += OnMessageToProxies;
            SceneNetworking.OnOtherPlayerJoined += OnOtherPlayerJoined;
            _grabInteractable.selectEntered.AddListener(OnGrabSelected);
            _grabInteractable.selectExited.AddListener(OnGrabDeselected);
            _grabInteractable.selectFilters.Add(this);
        }

        /// <summary>
        /// Unsubscribes from all events to prevent memory leaks.
        /// </summary>
        protected virtual void OnDestroy()
        {
            SceneNetworking.OnOtherPlayerJoined -= OnOtherPlayerJoined;
            _grabInteractable.selectEntered.RemoveListener(OnGrabSelected);
            _grabInteractable.selectExited.RemoveListener(OnGrabDeselected);
            _grabInteractable.selectFilters.Remove(this);
        }

        /// <summary>
        /// Called by NetworkBridge once the Fusion NetworkObject has spawned. Authority client sets initial seed state.
        /// </summary>
        private void OnSpawned()
        {
            Logger.Info($"OnSpawned() '{gameObject.name}' — network object spawned, initializing plant seed");
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
            if (!networkBridge.Object.HasStateAuthority) return;

            BytesWriter writer = new BytesWriter(BytesWriter.ByteSize * 4 + BytesWriter.IntSize + BytesWriter.IntSize);
            writer.AddByte(IsSeed ? (byte)1 : (byte)0);
            writer.AddByte(InShop ? (byte)1 : (byte)0);
            writer.AddByte(IsBought ? (byte)1 : (byte)0);
            writer.AddByte(IsInPool ? (byte)1 : (byte)0);
            writer.AddInt((int)(_plantedTimestamp >> 32));
            writer.AddInt((int)(_plantedTimestamp & 0xFFFFFFFFL));
            networkBridge.RPC_SendMessageToProxies((byte)PlantMessageType.stateSync, writer.Data);
        }

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
        /// Snaps to slot position, switches to plant state, starts growth timer, disables grab.
        /// </summary>
        private void OnTriggerEnterSeed(Collider other)
        {
            if (!networkBridge.Object.HasStateAuthority) return;

            PlantSlot slot = other.GetComponent<PlantSlot>();
            if (slot == null || slot.IsOccupied) return;

            _occupiedSlot = slot;
            gameObject.transform.position = slot.transform.position;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.zero;
            SetState(false);
            _plantedTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            slot.SetOccupied(true);

            var grab = GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = false;
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.disable, new byte[0]);
        }

        /// <summary>
        /// Frees the PlantSlot when a fully grown plant leaves it.
        /// </summary>
        void OnTriggerExit(Collider other)
        {
            if (IsSeed || GetGrowthCompletion() < 1f) return;
            PlantSlot slot = other.GetComponent<PlantSlot>();
            if (slot == null) return;
            slot.SetOccupied(false);
        }

        /// <summary>
        /// Broadcast a sold RPC to all clients. The sold handler on each client applies
        /// visual/state changes; the authority also teleports and returns to pool.
        /// </summary>
        public void ToBeSold()
        {
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.sold, new byte[0]);
        }

        /// <summary>
        /// Drives growth scaling each frame. Authority only. Calls OnGrowthUpdated for subclass scale logic,
        /// and OnFullyGrown when the current phase completes.
        /// </summary>
        private void Update()
        {
            OnWillUpdate();

            if (IsSeed) { Logger.Log($"Update() '{gameObject.name}' — GUARD: IsSeed=true, returning"); return; }
            if (networkBridge.Object == null) { Logger.Log($"Update() '{gameObject.name}' — GUARD: networkBridge.Object==null, returning"); return; }
            if (!networkBridge.Object.HasStateAuthority) { Logger.Log($"Update() '{gameObject.name}' — GUARD: no state authority, returning"); return; }
            Logger.Log($"Update() '{gameObject.name}' — past guards, calling GetGrowthCompletion");

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
            Logger.Info($"Update() '{gameObject.name}' — fully grown, grab interactable enabled");
        }

        /// <summary>
        /// Returns growth completion [0,1] for the current phase based on elapsed time since _plantedTimestamp.
        /// </summary>
        public float GetGrowthCompletion()
        {
            Logger.Log($"GetGrowthCompletion() '{gameObject.name}' — seedDefinition={seedDefinition != null}, phases={seedDefinition?.phases != null}, phaseCount={seedDefinition?.phases?.Count}, _growthPhase={_growthPhase}");
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
            UpdateVisuals(isSeed);
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
                    Logger.Info($"OnMessageToAll() '{gameObject.name}' — received enable message, setting grab on");
                    _grabInteractable.enabled = true;
                    break;
                case PlantMessageType.disable:
                    Logger.Info($"OnMessageToAll() '{gameObject.name}' — received disable message, setting grab off");
                    SetState(false);
                    _grabInteractable.enabled = false;
                    break;
                case PlantMessageType.grabber:
                    BytesReader grabReader = new BytesReader(data);
                    bool hasGrabber = grabReader.NextByte() == 1;
                    _grabber = hasGrabber ? EconomyManager.Instance.GetPlayer(grabReader.NextString()) : null;
                    Logger.Info($"OnMessageToAll() '{gameObject.name}' — grabber updated: {(_grabber != null ? _grabber.GetID() : "null")}");
                    break;
                case PlantMessageType.sold:
                    Logger.Info($"OnMessageToAll() '{gameObject.name}' — received sold message, returning to seed state");
                    _grabber = null;
                    _occupiedSlot = null;
                    SetState(true);
                    _grabInteractable.enabled = false;
                    if (networkBridge.Object.HasStateAuthority)
                    {
                        transform.position = new Vector3(transform.position.x, 3f, transform.position.z);
                        PoolManager.Instance.ReturnPlantSeed(seedDefinition.seedId, this);
                    }
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
            if (isSeed)
                _grabInteractable.enabled = true;
            else
                _grabInteractable.enabled = GetGrowthCompletion() >= 1f;

            Logger.Info($"OnMessageToProxies() '{gameObject.name}' — stateSync received, isSeed={isSeed}, InShop={InShop}, IsBought={IsBought}, IsInPool={IsInPool}, timestamp={_plantedTimestamp}");
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
            Logger.Log($"OnGrabSelected() '{gameObject.name}' — interactorObject='{args.interactorObject}', EconomyManager.Instance={EconomyManager.Instance != null}");
            _grabber = EconomyManager.Instance.GetLocalPlayer();
            Logger.Log($"OnGrabSelected() '{gameObject.name}' — _grabber set to '{_grabber?.GetID() ?? "null"}'");
            string id = _grabber.GetID();
            int size = BytesWriter.ByteSize + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(id);
            var writer = new BytesWriter(size);
            writer.AddByte(1);
            writer.AddString(id);
            Logger.Log($"OnGrabSelected() '{gameObject.name}' — sending grabber RPC with id='{id}'");
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.grabber, writer.Data);
            Logger.Log($"OnGrabSelected() '{gameObject.name}' — done");
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
        vineDecayStart
    }
}
