using Fusion;
using SomniumSpace.Network.Bridge;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        /// <summary>
        /// Finishes stocking the pool once Fusion has actually spawned the scene objects.
        ///
        /// Awake runs before that, so nothing has state authority yet and every seed fails its
        /// first Restore() and lands in _pendingRestore. On the master that list drains a moment
        /// later, which is the whole point of the retry.
        ///
        /// On a peer it never drained, because a peer is not supposed to own these at all — and
        /// the retry then sat waiting for the one thing that *does* hand a peer authority over a
        /// seed: picking it up. The first time a player touched a carrot, a turnip or a pumpkin,
        /// the next frame's Restore() succeeded and teleported it out of the shop and into the
        /// pool. Once per seed type, on first contact, on every peer.
        ///
        /// Stocking the pool is the world owner's job. Peers are told what is pooled by the
        /// master's state sync and have no business deciding it for themselves.
        /// </summary>
        private void Update()
        {
            if (_pendingRestore.Count == 0) return;
            if (!SceneNetworking.IsNetworkReady) return;

            if (!SceneNetworking.IsMasterClient)
            {
                _pendingRestore.Clear();
                return;
            }

            for (int i = _pendingRestore.Count - 1; i >= 0; i--)
            {
                PlantSeed plant = _pendingRestore[i];

                // It reached the world while we were waiting to park it — stocked into a shop
                // slot, or in somebody's hands. Either way it is no longer the pool's to place.
                if (plant == null || plant.InShop || !string.IsNullOrEmpty(plant.HolderId))
                {
                    _pendingRestore.RemoveAt(i);
                    continue;
                }

                if (Restore(plant))
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
            plant.Claim();
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

            plant.ReturnToPool(transform.position, transform.rotation);

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
