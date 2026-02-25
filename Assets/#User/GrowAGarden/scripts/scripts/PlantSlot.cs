using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    public class PlantSlot : MonoBehaviour
    {
        [SerializeField] NetworkBridge _networkBridge;
        private Plantable _currentPlant = null;
        private string _currentSeedId = null;
        private const float TIMEOUT = 2f;

        private void Start()
        {
            _networkBridge.OnMessageToAll += OnMessageToAll;
        }

        public bool IsOccupied = false;

        public void Plant(SeedDefinition seed, float scaleOverride = 0f)
        {
            if (IsOccupied) return;
            if (!SceneNetworking.IsMasterClient) return;

            IsOccupied = true; // Set immediately, don't wait for RPC

            Plantable plant = PoolManager.Instance.ClaimPlant(seed.seedId);
            if (plant == null)
            {
                IsOccupied = false; // Reset if failed
                return;
            }

            _currentPlant = plant;
            _currentSeedId = seed.seedId;

            plant.transform.position = transform.position;
            plant.transform.SetParent(transform);
            plant.growDurationSeconds = seed.growDurationSeconds;
            plant.maxScale = scaleOverride > 0f ? scaleOverride : seed.maxScale;
            plant.scaleMultiplier = seed.scaleMultiplier;
            plant.OnPlanted();
            _networkBridge.RPC_SendMessageToAll((byte)PlantSlotMessageType.Planted, new byte[0]);
        }

        public void Load(SeedDefinition seed, long savedTimestamp, float scaleOverride = 0f)
        {
            if (IsOccupied) return;

            _currentPlant = PoolManager.Instance.ClaimPlant(seed.seedId);
            if (_currentPlant == null) return;

            _currentSeedId = seed.seedId;
            _currentPlant.transform.position = transform.position;
            _currentPlant.growDurationSeconds = seed.growDurationSeconds;
            _currentPlant.maxScale = scaleOverride > 0f ? scaleOverride : seed.maxScale;
            _currentPlant.scaleMultiplier = seed.scaleMultiplier;
            _currentPlant.OnLoaded(savedTimestamp);
        }

        public void Harvest()
        {
            if (!IsOccupied) return;
            _currentPlant.OnHarvested();
            PoolManager.Instance.ReturnPlant(_currentSeedId, _currentPlant);
            _currentPlant = null;
            _currentSeedId = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsOccupied) return;

            SeedObject seed = other.GetComponent<SeedObject>();
            if (seed == null) return;

            var seedNetworkObj = seed.GetComponent<NetworkObject>();
            bool hasSeedAuthority = seedNetworkObj != null && seedNetworkObj.HasStateAuthority;

            // Only master or seed holder processes this
            if (!SceneNetworking.IsMasterClient && !hasSeedAuthority) return;

            // If I'm holding the seed, disable grab
            if (hasSeedAuthority)
            {
                var grab = seed.GetComponent<XRGrabInteractable>();
                if (grab != null) grab.enabled = false;
            }

            // Both master and authority return seed (different purposes)
            PoolManager.Instance.ReturnSeed(seed.seedDefinition.seedId, seed);

            // Master plants
            Plant(seed.seedDefinition, seed.scaleOverride);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((PlantSlotMessageType)id)
            {
                case PlantSlotMessageType.Planted:
                    {
                        IsOccupied = true;
                        break;
                    }
                case PlantSlotMessageType.Harvested:
                    {
                        IsOccupied = false;
                        break;
                    }
                default:
                    break;
            }
        }


        /// <summary>
        /// Message IDs that could be recieved.
        /// </summary>
        enum PlantSlotMessageType : byte
        {
            Planted = 0,
            Harvested = 1,
        }
    }
}