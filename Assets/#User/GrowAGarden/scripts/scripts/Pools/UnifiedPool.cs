using Fusion;
using SomniumSpace.Network.Bridge;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    public class UnifiedPool : MonoBehaviour
    {
        
        [SerializeField] private SeedDefinition _seedDefinition;
        [SerializeField] private NetworkObject _networkObject;
        [SerializeField] private NetworkBridge _networkBridge;
        [SerializeField] public UnifiedPlantSeed[] PlantsInPool;
        public string SeedId => _seedDefinition.seedId;
        public int Available => GetComponentsInChildren<UnifiedPlantSeed>().Length;

        private void Awake()
        {
            var plants = GetComponentsInChildren<UnifiedPlantSeed>();
            Logger.Info($"Awake() '{gameObject.name}' — seedId='{SeedId}', found {plants.Length} plant(s) to restore");
            foreach (UnifiedPlantSeed plant in plants)
                Restore(plant);

            _networkBridge.OnMessageToController += OnMessageReceived;
        }

        public UnifiedPlantSeed Claim()
        {
            UnifiedPlantSeed plant = GetComponentInChildren<UnifiedPlantSeed>();
            if (plant == null)
            {
                Logger.Warn($"Claim() — pool empty for '{SeedId}'!");
                return null;
            }
            Logger.Log($"Claim() '{SeedId}' — claiming '{plant.name}', remaining after={Available - 1}");
            plant.transform.SetParent(null);
            return plant;
        }

        public void Return(UnifiedPlantSeed plant)
        {
            if(!plant.networkBridge.Object.HasStateAuthority)
                StartCoroutine(RequestAuthorityAndReturn(plant));

            Logger.Log($"Return() '{SeedId}' — returning '{plant?.name}'");
            Restore(plant);
        }

        private IEnumerator RequestAuthorityAndReturn(UnifiedPlantSeed plant)
        {
            if (plant.networkBridge == null || plant.networkBridge.Object == null)
            {
                Logger.Error($"RequestAuthorityAndReturn() '{SeedId}' — plant '{plant.name}' has no network bridge or object, cannot request authority");
                yield break;
            }
            NetworkObject plantNetObj = plant.networkBridge.Object;
            if (!plantNetObj.HasStateAuthority)
            {
                Logger.Info($"RequestAuthorityAndReturn() '{SeedId}' — requesting authority on '{plant.name}', will wait up to 5s");
                plantNetObj.RequestStateAuthority();
                float timeout = 5f;
                float timer = 0f;
                while (!plantNetObj.HasStateAuthority && timer < timeout)
                {
                    yield return null;
                    timer += Time.deltaTime;
                }
                if (!plantNetObj.HasStateAuthority)
                {
                    Logger.Error($"RequestAuthorityAndReturn() '{SeedId}' — failed to gain authority on '{plant.name}' after {timeout}s, cannot return to pool");
                    yield break;
                }
            }
            Logger.Info($"RequestAuthorityAndReturn() '{SeedId}' — gained authority on '{plant.name}', returning to pool");
            Return(plant);
        }

        private void Restore(UnifiedPlantSeed plant)
        {
            if (!_networkObject.HasStateAuthority) return;
            Logger.Log($"Restore() '{SeedId}' — restoring '{plant?.name}' to pool position {transform.position}");
            plant.transform.SetParent(transform);
            plant.transform.position = transform.position;
            plant.transform.rotation = transform.rotation;

            var grab = plant.GetComponent<XRGrabInteractable>();
            var rb = plant.GetComponent<Rigidbody>();
            if (grab != null) grab.enabled = true;
            if (rb != null) rb.isKinematic = true;

            //TODO: REFACTOR HARVEST LOGIC
            //var harvest = plant.GetComponent<HarvestInteractable>();
            //if (harvest != null) harvest.ResetState();

            Logger.Log($"Restore() '{SeedId}' — '{plant?.name}' restore complete");
        }

        private void OnMessageReceived(byte id, byte[] data)
        {
            Logger.Info($"OnMessageReceived() '{gameObject.name}' — id={id} ({(PoolMessageType)id}), dataLength={data?.Length}");

            switch ((PoolMessageType)id)
            {
                case PoolMessageType.Claim:
                    {
                        Logger.Info($"OnMessageReceived() '{gameObject.name}' — Claim request received");
                        Claim();
                        break;
                    }
                case PoolMessageType.Return:
                    {
                        OnReturnRequested(data);
                        break;
                    }
                default:
                    Logger.Warn($"OnMessageReceived() '{gameObject.name}' — unknown messageId={id}");
                    break;
            }
        }

        private void OnReturnRequested(byte[] data)
        {
            BytesReader reader = new BytesReader(data);
            if (!reader.IsValid)
            {
                Logger.Error($"OnMessageReceived() '{gameObject.name}' — Return: invalid data");
                return;
            }

            int networkId = reader.NextInt();
            Logger.Info($"OnMessageReceived() '{gameObject.name}' — Return: looking for plant with NetworkId={networkId}");

            UnifiedPlantSeed target = null;
            foreach (var plant in PlantsInPool)
            {
                var netObj = plant.networkBridge?.Object;
                if (netObj != null && netObj.Id.Raw == networkId)
                {
                    target = plant;
                    break;
                }
            }

            if (target != null)
            {
                Logger.Info($"OnMessageReceived() '{gameObject.name}' — Return: found '{target.name}', restoring");
                Return(target);
            }
            else
            {
                Logger.Error($"OnMessageReceived() '{gameObject.name}' — Return: no plant found with NetworkId={networkId}");
            }
            
        }

    }

    enum PoolMessageType : byte
    {
        Claim = 0,
        Return = 1 // data is plant NetworkId
    }
}
