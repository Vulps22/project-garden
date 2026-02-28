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
        }

        private void OnSpawned()
        {
            Logger.Info($"OnSpawned() '{gameObject.name}' � network object spawned, initializing plant seed");
            SetState(true);
        }

        /// <summary>
        /// Handle collision with PlantSlot to trigger planting logic.
        /// </summary>
        /// <param name="collision"></param>
         void OnTriggerEnter(Collider other)
        {
            if(!IsSeed)
                return;

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
            networkBridge.RPC_SendMessageToAll((byte)plantMessageType.disable, new byte[0]);

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
                networkBridge.RPC_SendMessageToAll((byte)plantMessageType.enable, new byte[0]);
                Logger.Info($"Update() '{gameObject.name}' � fully grown, grab interactable enabled");
            }
        }

        public float GetGrowthCompletion()
        {
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            float raw = (now - _plantedTimestamp) / seedDefinition.growDurationSeconds;
            return Mathf.Clamp01(raw);
        }

        /// <summary>
        /// Set the state of this object to either seed or plant, enabling/disabling the appropriate model.
        /// </summary>
        /// <param name="isSeed">True to show the seed model, False to show the plant model</param>
        public void SetState(bool isSeed)
        {
            IsSeed = isSeed;
            if (IsSeed)
            {
                Logger.Info($"SetState(true) '{gameObject.name}' � showing seed model");
                _PlantModel.enabled = false;
                _SeedModel.enabled = true;
                _PlantCollider.enabled = false;
                _SeedCollider.enabled = true;
                networkBridge.RPC_SendMessageToAll((byte)plantMessageType.enable, new byte[0]);
            }
            else
            {
                Logger.Info($"SetState(false) '{gameObject.name}' � showing plant model");
                _PlantModel.enabled = true;
                _SeedModel.enabled = false;
                _PlantCollider.enabled = true;
                _SeedCollider.enabled = false;
            }
            
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((plantMessageType)id)
            {
                case plantMessageType.enable:
                    Logger.Info($"OnMessageToAll() '{gameObject.name}' � received enable message, setting grab on");
                    _grabInteractable.enabled = true;
                    break;
                case plantMessageType.disable:
                    Logger.Info($"OnMessageToAll() '{gameObject.name}' � received disable message, setting grab off");
                    SetState(false);
                    _grabInteractable.enabled = false;
                    break;
            }

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
    enum plantMessageType
    {
        enable = 11,
        disable = 12
    }
}
