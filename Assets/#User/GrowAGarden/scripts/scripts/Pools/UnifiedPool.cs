using Fusion;
using SomniumSpace.Network.Bridge;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    public class UnifiedPool : MonoBehaviour
    {

        [SerializeField] private SeedDefinition _seedDefinition;
        [SerializeField] private NetworkObject _networkObject;
        [SerializeField] private NetworkBridge _networkBridge;
        [SerializeField] public PlantSeed[] PlantsInPool;
        public string SeedId => _seedDefinition.seedId;
        public int Available => System.Array.FindAll(PlantsInPool, p => p.IsInPool).Length;

        private readonly List<PlantSeed> _pendingRestore = new List<PlantSeed>();

        private void Awake()
        {
            foreach (PlantSeed plant in PlantsInPool)
            {
                if (!Restore(plant))
                    _pendingRestore.Add(plant);
            }

            _networkBridge.OnMessageToController += OnMessageReceived;
        }

        private void Update()
        {
            if (_pendingRestore.Count == 0) return;
            for (int i = _pendingRestore.Count - 1; i >= 0; i--)
            {
                if (Restore(_pendingRestore[i]))
                {
                    _pendingRestore.RemoveAt(i);
                }
            }
        }

        public PlantSeed Claim()
        {
            PlantSeed plant = System.Array.Find(PlantsInPool, p => p.IsInPool);
            if (plant == null)
            {
                return null;
            }
            plant.IsInPool = false;
            return plant;
        }

        public void Return(PlantSeed plant)
        {
            if (!plant.networkBridge.Object.HasStateAuthority)
                StartCoroutine(RequestAuthorityAndReturn(plant));
            else
            {
                Restore(plant);
            }
        }

        private IEnumerator RequestAuthorityAndReturn(PlantSeed plant)
        {
            if (plant.networkBridge == null || plant.networkBridge.Object == null)
            {
                Logger.Error($"RequestAuthorityAndReturn() '{SeedId}' � plant '{plant.name}' has no network bridge or object, cannot request authority");
                yield break;
            }
            NetworkObject plantNetObj = plant.networkBridge.Object;
            if (!plantNetObj.HasStateAuthority)
            {
                plantNetObj.RequestStateAuthority();
                float timeout = 20f;
                float timer = 0f;
                while (!plantNetObj.HasStateAuthority && timer < timeout)
                {
                    yield return null;
                    timer += Time.deltaTime;
                }
                if (!plantNetObj.HasStateAuthority)
                {
                    Logger.Error($"RequestAuthorityAndReturn() '{SeedId}' � failed to gain authority on '{plant.name}' after {timeout}s, cannot return to pool");
                    yield break;
                }
            }
            Return(plant);
        }

        private bool Restore(PlantSeed plant)
        {
            if (!plant.networkBridge.HasStateAuthority)
            {
                return false;
            }

            plant.IsInPool = true;
            plant.transform.position = transform.position;
            plant.transform.rotation = transform.rotation;
            plant.transform.localScale = Vector3.one;

            var grab = plant.GetComponent<XRGrabInteractable>();
            var rb = plant.GetComponent<Rigidbody>();
            if (grab != null) grab.enabled = true;
            if (rb != null) rb.isKinematic = true;

            plant.SetState(true);

            plant.broadcastState();

            return true;
        }

        private void OnMessageReceived(byte id, byte[] data)
        {

            switch ((PoolMessageType)id)
            {
                case PoolMessageType.Claim:
                    {
                        Claim();
                        break;
                    }
                case PoolMessageType.Return:
                    {
                        OnReturnRequested(data);
                        break;
                    }
                default:
                    break;
            }
        }

        private void OnReturnRequested(byte[] data)
        {
            BytesReader reader = new BytesReader(data);
            if (!reader.IsValid)
            {
                return;
            }

            int networkId = reader.NextInt();

            PlantSeed target = null;
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
                Return(target);
            }
            else
            {
                Logger.Error($"OnMessageReceived() '{gameObject.name}' � Return: no plant found with NetworkId={networkId}");
            }
            
        }

    }

    enum PoolMessageType : byte
    {
        Claim = 0,
        Return = 1 // data is plant NetworkId
    }
}
