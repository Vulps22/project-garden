using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace GrowAGarden
{

    public class UnifiedPlantSeed : MonoBehaviour, IXRSelectFilter
    {

        [SerializeField] private MeshRenderer _PlantModel;
        [SerializeField] private MeshRenderer _SeedModel;
        [SerializeField] private Collider _PlantCollider;
        [SerializeField] private Collider _SeedCollider;
        [SerializeField] public SeedDefinition seedDefinition;
        [SerializeField] public NetworkBridge networkBridge;
        [SerializeField] private XRGrabInteractable _grabInteractable;

        public bool IsSeed = false;
        public bool InShop = false;
        public bool IsBought = false;
        public bool IsInPool = false;
        private long _plantedTimestamp;
        private PlayerBalance _grabber;

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

        private void Start()
        {
            networkBridge.OnSpawned += OnSpawned;
            networkBridge.OnMessageToAll += OnMessageToAll;
            networkBridge.OnMessageToProxies += OnMessageToProxies;
            SceneNetworking.OnOtherPlayerJoined += OnOtherPlayerJoined;
            _grabInteractable.selectEntered.AddListener(OnGrabSelected);
            _grabInteractable.selectExited.AddListener(OnGrabDeselected);
            _grabInteractable.selectFilters.Add(this);
        }

        private void OnDestroy()
        {
            SceneNetworking.OnOtherPlayerJoined -= OnOtherPlayerJoined;
            _grabInteractable.selectEntered.RemoveListener(OnGrabSelected);
            _grabInteractable.selectExited.RemoveListener(OnGrabDeselected);
            _grabInteractable.selectFilters.Remove(this);
        }

        private void OnSpawned()
        {
            Logger.Info($"OnSpawned() '{gameObject.name}' — network object spawned, initializing plant seed");
            if (networkBridge.Object.HasStateAuthority)
                SetState(true);
        }

        private void OnOtherPlayerJoined(PlayerRef player)
        {
            broadcastState();   
        }

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

        void OnTriggerEnter(Collider other)
        {
            if (IsSeed)
                OnTriggerEnterSeed(other);
        }

        private void OnTriggerEnterSeed(Collider other)
        {
            if (!networkBridge.Object.HasStateAuthority) return;

            PlantSlot slot = other.GetComponent<PlantSlot>();
            if (slot == null || slot.IsOccupied) return;

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

        void OnTriggerExit(Collider other)
        {
            if (IsSeed) return;
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

        private void Update()
        {
            if (IsSeed) return;
            if (networkBridge.Object == null) return;
            if (!networkBridge.Object.HasStateAuthority) return;

            float completion = GetGrowthCompletion();
            float targetScale = completion * seedDefinition.maxScale * seedDefinition.scaleMultiplier;
            transform.localScale = Vector3.one * targetScale;

            if (completion >= 1f && _grabInteractable != null && !_grabInteractable.enabled)
            {
                _grabInteractable.enabled = true;
                networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.enable, new byte[0]);
                Logger.Info($"Update() '{gameObject.name}' — fully grown, grab interactable enabled");
            }
        }

        public float GetGrowthCompletion()
        {
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            float raw = (now - _plantedTimestamp) / seedDefinition.growDurationSeconds;
            return Mathf.Clamp01(raw);
        }

        /// <summary>
        /// Set the visual/collider state of this object. Pure local operation — no broadcast.
        /// </summary>
        public void SetState(bool isSeed)
        {
            IsSeed = isSeed;
            if (IsSeed)
            {
                Logger.Info($"SetState(true) '{gameObject.name}' — showing seed model");
                _PlantModel.enabled = false;
                _SeedModel.enabled = true;
                _PlantCollider.enabled = false;
                _SeedCollider.enabled = true;
            }
            else
            {
                Logger.Info($"SetState(false) '{gameObject.name}' — showing plant model");
                _PlantModel.enabled = true;
                _SeedModel.enabled = false;
                _PlantCollider.enabled = true;
                _SeedCollider.enabled = false;
            }
        }

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
                    SetState(true);
                    _grabInteractable.enabled = false;
                    if (networkBridge.Object.HasStateAuthority)
                    {
                        transform.position = new Vector3(transform.position.x, 3f, transform.position.z);
                        PoolManager.Instance.ReturnUnifiedPlantSeed(seedDefinition.seedId, this);
                    }
                    break;
                default:
                    Logger.Warn($"OnMessageToAll() '{gameObject.name}' — received unknown message id={id}");
                    break;
            }
        }

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

        public void OnGrabSelected(SelectEnterEventArgs args)
        {
            string id = EconomyManager.Instance.GetLocalPlayer().GetID();
            int size = BytesWriter.ByteSize + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(id);
            var writer = new BytesWriter(size);
            writer.AddByte(1);
            writer.AddString(id);
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.grabber, writer.Data);
        }

        public void OnGrabDeselected(SelectExitEventArgs args)
        {
            var writer = new BytesWriter(BytesWriter.ByteSize);
            writer.AddByte(0);
            networkBridge.RPC_SendMessageToAll((byte)PlantMessageType.grabber, writer.Data);
        }

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
        grabber
    }
}
