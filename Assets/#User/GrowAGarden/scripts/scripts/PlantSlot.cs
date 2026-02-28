using System;
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

        // Ordering cache: whichever arrives first (Plantable trigger or RPC) waits for the other
        private bool _hasPendingConfig = false;
        private string _pendingSeedId;
        private long _pendingTimestamp;
        private float _pendingGrowDuration;
        private float _pendingMaxScale;
        private float _pendingScaleMultiplier;

        public bool IsOccupied = false;

        private void Start()
        {
            Logger.Info($"Start() '{gameObject.name}' — networkBridge={(_networkBridge != null ? "found" : "NULL")}");
            if (_networkBridge == null)
            {
                Logger.Error($"Start() '{gameObject.name}' — _networkBridge is NULL! RPC messages won't work!");
                return;
            }
            _networkBridge.OnMessageToAll += OnMessageToAll;
            Logger.Log($"Start() '{gameObject.name}' — subscribed to OnMessageToAll");
        }


        public void SetOccupied(bool occupied)
        {
            Logger.Info($"SetOccupied({occupied}) called on '{gameObject.name}'");
            IsOccupied = occupied;
        }
        public void Plant(SeedDefinition seed, float scaleOverride = 0f)
        {
            Logger.Info($"Plant() '{gameObject.name}' — seedId='{seed?.seedId}', IsOccupied={IsOccupied}, IsMasterClient={SceneNetworking.IsMasterClient}");

            if (IsOccupied)
            {
                Logger.Warn($"Plant() '{gameObject.name}' — already occupied, ignoring");
                return;
            }
            if (!SceneNetworking.IsMasterClient)
            {
                Logger.Warn($"Plant() '{gameObject.name}' — not master client, ignoring");
                return;
            }

            IsOccupied = true;

            Plantable plant = PoolManager.Instance.ClaimPlant(seed.seedId);
            if (plant == null)
            {
                Logger.Error($"Plant() '{gameObject.name}' — ClaimPlant returned NULL for '{seed.seedId}', aborting");
                IsOccupied = false;
                return;
            }

            Logger.Info($"Plant() '{gameObject.name}' — claimed '{plant.name}', moving to {transform.position}");
            _currentPlant = plant;
            _currentSeedId = seed.seedId;

            // Move plant to slot — Fusion syncs this to all clients, firing their OnTriggerEnter
            plant.transform.position = transform.position;

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            float effectiveMaxScale = scaleOverride > 0f ? scaleOverride : seed.maxScale;

            Logger.Info($"Plant() '{gameObject.name}' — sending RPC Planted: seedId='{seed.seedId}', timestamp={timestamp}, growDuration={seed.growDurationSeconds}, maxScale={effectiveMaxScale}, scaleMultiplier={seed.scaleMultiplier}");

            _networkBridge.RPC_SendMessageToAll(
                (byte)PlantSlotMessageType.Planted,
                BuildPlantedPayload(seed.seedId, timestamp, seed.growDurationSeconds, effectiveMaxScale, seed.scaleMultiplier)
            );
        }

        public void Load(SeedDefinition seed, long savedTimestamp, float scaleOverride = 0f)
        {
            Logger.Info($"Load() '{gameObject.name}' — seedId='{seed?.seedId}', savedTimestamp={savedTimestamp}");
            if (IsOccupied)
            {
                Logger.Warn($"Load() '{gameObject.name}' — already occupied, ignoring");
                return;
            }

            _currentPlant = PoolManager.Instance.ClaimPlant(seed.seedId);
            if (_currentPlant == null)
            {
                Logger.Error($"Load() '{gameObject.name}' — ClaimPlant returned NULL for '{seed.seedId}'");
                return;
            }

            _currentSeedId = seed.seedId;
            IsOccupied = true;
            ConfigurePlant(_currentPlant, seed.seedId, savedTimestamp, seed.growDurationSeconds,
                scaleOverride > 0f ? scaleOverride : seed.maxScale, seed.scaleMultiplier);
        }

        public void Harvest()
        {
            Logger.Info($"Harvest() '{gameObject.name}' — IsOccupied={IsOccupied}, currentPlant='{(_currentPlant != null ? _currentPlant.name : "NULL")}', seedId='{_currentSeedId}'");
            if (!IsOccupied)
            {
                Logger.Warn($"Harvest() '{gameObject.name}' — not occupied, ignoring");
                return;
            }

            _currentPlant.OnHarvested();
            PoolManager.Instance.ReturnPlant(_currentSeedId, _currentPlant);
            _currentPlant = null;
            _currentSeedId = null;
            IsOccupied = false;
            Logger.Info($"Harvest() '{gameObject.name}' — complete, slot now free");
        }

        private void OnTriggerEnter(Collider other)
        {
            return; // Disable trigger handling for now, relying on RPCs and Fusion transform sync to manage plant state. Re-enable if we want to support physical seed dropping again.
            // --- Seed dropped into slot ---
            SeedObject seed = other.GetComponent<SeedObject>();
            if (seed != null)
            {
                Logger.Info($"OnTriggerEnter() '{gameObject.name}' — detected SeedObject '{seed.name}', IsOccupied={IsOccupied}, IsMasterClient={SceneNetworking.IsMasterClient}");

                if (IsOccupied)
                {
                    Logger.Warn($"OnTriggerEnter() '{gameObject.name}' — slot occupied, ignoring seed '{seed.name}'");
                    return;
                }

                var seedNetworkObj = seed.GetComponent<NetworkObject>();
                bool hasSeedAuthority = seedNetworkObj != null && seedNetworkObj.HasStateAuthority;

                Logger.Info($"OnTriggerEnter() '{gameObject.name}' — hasSeedAuthority={hasSeedAuthority}");

                if (!SceneNetworking.IsMasterClient && !hasSeedAuthority)
                {
                    Logger.Log($"OnTriggerEnter() '{gameObject.name}' — not master and no seed authority, ignoring");
                    return;
                }

                if (hasSeedAuthority)
                {
                    var grab = seed.GetComponent<XRGrabInteractable>();
                    if (grab != null)
                    {
                        grab.enabled = false;
                        Logger.Log($"OnTriggerEnter() '{gameObject.name}' — disabled grab on seed '{seed.name}'");
                    }
                }

                Logger.Info($"OnTriggerEnter() '{gameObject.name}' — returning seed '{seed.name}' to pool and planting");
                PoolManager.Instance.ReturnSeed(seed.seedDefinition.seedId, seed);
                Plant(seed.seedDefinition, seed.scaleOverride);
                return;
            }

            // --- Plant arrived at slot (all clients, via Fusion transform sync) ---
            Plantable plant = other.GetComponent<Plantable>();
            if (plant != null)
            {
                Logger.Info($"OnTriggerEnter() '{gameObject.name}' — detected Plantable '{plant.name}', _currentPlant={(_currentPlant != null ? _currentPlant.name : "null")}, _hasPendingConfig={_hasPendingConfig}");

                if (_currentPlant != null)
                {
                    Logger.Warn($"OnTriggerEnter() '{gameObject.name}' — _currentPlant already set to '{_currentPlant.name}', ignoring Plantable trigger");
                    return;
                }

                _currentPlant = plant;

                if (_hasPendingConfig)
                {
                    Logger.Info($"OnTriggerEnter() '{gameObject.name}' — applying pending config: seedId='{_pendingSeedId}', timestamp={_pendingTimestamp}");
                    ConfigurePlant(_currentPlant, _pendingSeedId, _pendingTimestamp,
                        _pendingGrowDuration, _pendingMaxScale, _pendingScaleMultiplier);
                    _hasPendingConfig = false;
                }
                else
                {
                    Logger.Log($"OnTriggerEnter() '{gameObject.name}' — no pending config yet, waiting for RPC");
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if(!_networkBridge.HasStateAuthority)
            {
                return;
            }
            
            if (other.TryGetComponent(out UnifiedPlantSeed plant))
            {
                if (!plant.IsSeed)
                {
                    Logger.Info($"OnTriggerExit() '{gameObject.name}' — Plantable '{plant.name}' exited trigger, clearing _currentPlant");
                    SetOccupied(false);
                    _networkBridge.RPC_SendMessageToAll((byte)PlantSlotMessageType.Harvested, new byte[0]);
                }
            }
        }

        private void ConfigurePlant(Plantable plant, string seedId, long timestamp,
            float growDuration, float maxScale, float scaleMultiplier)
        {
            Logger.Info($"ConfigurePlant() '{gameObject.name}' — plant='{plant?.name}', seedId='{seedId}', timestamp={timestamp}, growDuration={growDuration}, maxScale={maxScale}, scaleMultiplier={scaleMultiplier}");

            _currentSeedId = seedId;
            plant.transform.position = transform.position;
            plant.transform.SetParent(transform);
            plant.growDurationSeconds = growDuration;
            plant.maxScale = maxScale;
            plant.scaleMultiplier = scaleMultiplier;
            plant.OnLoaded(timestamp);

            Logger.Info($"ConfigurePlant() '{gameObject.name}' — done, plant scale={plant.transform.localScale}");
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            Logger.Info($"OnMessageToAll() '{gameObject.name}' — messageId={id} ({(PlantSlotMessageType)id}), dataLength={data?.Length}");

            switch ((PlantSlotMessageType)id)
            {
                case PlantSlotMessageType.Planted:
                {
                    ParsePlantedPayload(data, out string seedId, out long timestamp,
                        out float growDuration, out float maxScale, out float scaleMultiplier);

                    Logger.Info($"OnMessageToAll() '{gameObject.name}' — Planted: seedId='{seedId}', timestamp={timestamp}, growDuration={growDuration}, maxScale={maxScale}, scaleMultiplier={scaleMultiplier}");

                    IsOccupied = true;

                    if (_currentPlant != null)
                    {
                        Logger.Info($"OnMessageToAll() '{gameObject.name}' — plant '{_currentPlant.name}' already present (trigger fired first), configuring now");
                        ConfigurePlant(_currentPlant, seedId, timestamp, growDuration, maxScale, scaleMultiplier);
                    }
                    else
                    {
                        Logger.Info($"OnMessageToAll() '{gameObject.name}' — plant not yet arrived (RPC first), caching config for trigger");
                        _pendingSeedId = seedId;
                        _pendingTimestamp = timestamp;
                        _pendingGrowDuration = growDuration;
                        _pendingMaxScale = maxScale;
                        _pendingScaleMultiplier = scaleMultiplier;
                        _hasPendingConfig = true;
                    }
                    break;
                }
                case PlantSlotMessageType.Harvested:
                {
                    Logger.Info($"OnMessageToAll() '{gameObject.name}' — Harvested RPC received");
                    IsOccupied = false;
                    break;
                }
                default:
                    Logger.Warn($"OnMessageToAll() '{gameObject.name}' — unknown messageId={id}");
                    break;
            }
        }

        // Payload layout:
        //   [2 bytes]  seedId string length (ushort, little-endian)
        //   [N bytes]  seedId UTF8
        //   [8 bytes]  plantedTimestamp (long, little-endian)
        //   [4 bytes]  growDurationSeconds (float)
        //   [4 bytes]  effectiveMaxScale (float)
        //   [4 bytes]  scaleMultiplier (float)
        private static byte[] BuildPlantedPayload(string seedId, long timestamp,
            float growDuration, float maxScale, float scaleMultiplier)
        {
            byte[] seedBytes = System.Text.Encoding.UTF8.GetBytes(seedId);
            byte[] payload = new byte[2 + seedBytes.Length + 8 + 4 + 4 + 4];
            int offset = 0;

            payload[offset++] = (byte)(seedBytes.Length & 0xFF);
            payload[offset++] = (byte)((seedBytes.Length >> 8) & 0xFF);

            Array.Copy(seedBytes, 0, payload, offset, seedBytes.Length);
            offset += seedBytes.Length;

            Array.Copy(BitConverter.GetBytes(timestamp), 0, payload, offset, 8);
            offset += 8;

            Array.Copy(BitConverter.GetBytes(growDuration), 0, payload, offset, 4);
            offset += 4;

            Array.Copy(BitConverter.GetBytes(maxScale), 0, payload, offset, 4);
            offset += 4;

            Array.Copy(BitConverter.GetBytes(scaleMultiplier), 0, payload, offset, 4);

            return payload;
        }

        private static void ParsePlantedPayload(byte[] data, out string seedId, out long timestamp,
            out float growDuration, out float maxScale, out float scaleMultiplier)
        {
            int offset = 0;

            int seedLen = data[offset] | (data[offset + 1] << 8);
            offset += 2;

            seedId = System.Text.Encoding.UTF8.GetString(data, offset, seedLen);
            offset += seedLen;

            timestamp = BitConverter.ToInt64(data, offset);
            offset += 8;

            growDuration = BitConverter.ToSingle(data, offset);
            offset += 4;

            maxScale = BitConverter.ToSingle(data, offset);
            offset += 4;

            scaleMultiplier = BitConverter.ToSingle(data, offset);
        }

        enum PlantSlotMessageType : byte
        {
            Planted = 0,
            Harvested = 1,
        }
    }
}
