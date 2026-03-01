using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{

    public class UnifiedPlantSeed : MonoBehaviour
    {

        [SerializeField] private MeshRenderer _PlantModel;
        [SerializeField] private MeshRenderer _SeedModel;
        [SerializeField] private Collider _PlantCollider;
        [SerializeField] private Collider _SeedCollider;
        [SerializeField] public SeedDefinition seedDefinition;
        [SerializeField] public NetworkBridge networkBridge;
        [SerializeField] private XRGrabInteractable _grabInteractable;

        public bool IsSeed = false;
        private long _plantedTimestamp;

        private void Start()
        {
            networkBridge.OnSpawned += OnSpawned;
            networkBridge.OnMessageToAll += OnMessageToAll;
            networkBridge.OnMessageToProxies += OnMessageToProxies;
            SceneNetworking.OnOtherPlayerJoined += OnOtherPlayerJoined;
        }

        private void OnDestroy()
        {
            SceneNetworking.OnOtherPlayerJoined -= OnOtherPlayerJoined;
        }

        private void OnSpawned()
        {
            Logger.Info($"OnSpawned() '{gameObject.name}' — network object spawned, initializing plant seed");
            if (networkBridge.Object.HasStateAuthority)
                SetState(true);
        }

        private void OnOtherPlayerJoined(PlayerRef player)
        {
            if (!networkBridge.Object.HasStateAuthority) return;

            BytesWriter writer = new BytesWriter(BytesWriter.ByteSize + BytesWriter.IntSize + BytesWriter.IntSize);
            writer.AddByte(IsSeed ? (byte)1 : (byte)0);
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
                case PlantMessageType.sold:
                    Logger.Info($"OnMessageToAll() '{gameObject.name}' — received sold message, returning to seed state");
                    SetState(true);
                    _grabInteractable.enabled = false;
                    if (networkBridge.Object.HasStateAuthority)
                    {
                        transform.position = new Vector3(transform.position.x, 3f, transform.position.z);
                        PoolManager.Instance.returnUnifiedPlantSeed(seedDefinition.seedId, this);
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
            long high = reader.NextInt();
            long low = (uint)reader.NextInt();
            _plantedTimestamp = (high << 32) | low;

            SetState(isSeed);
            if (isSeed)
                _grabInteractable.enabled = true;
            else
                _grabInteractable.enabled = GetGrowthCompletion() >= 1f;

            Logger.Info($"OnMessageToProxies() '{gameObject.name}' — stateSync received, isSeed={isSeed}, timestamp={_plantedTimestamp}");
        }

        private void OnValidate()
        {
            if (networkBridge == null)
                networkBridge = GetComponent<NetworkBridge>();
            if (_grabInteractable == null)
                _grabInteractable = GetComponent<XRGrabInteractable>();
        }
    }

    /// <summary>
    /// All UnifiedPlantSeed RPC messages start with the number 1 to avoid conflicts with any future messages added to SeedObject or Plantable.
    /// </summary>
    enum PlantMessageType
    {
        enable,
        disable,
        sold,
        stateSync
    }
}
