using Fusion;
using SomniumSpace.Network.Bridge;
using System.Collections;
using UnityEngine;

namespace GrowAGarden
{
    public class ShopSlot : MonoBehaviour
    {
        [SerializeField] private SeedDefinition _seedDefinition;
        private PlantSeed _currentSeed;

        private void Start()
        {
            Logger.Info($"Start() on '{gameObject.name}' — seedDefinition={(_seedDefinition != null ? _seedDefinition.seedId : "NULL")}, IsNetworkReady={SceneNetworking.IsNetworkReady}, IsMasterClient={SceneNetworking.IsMasterClient}");

            if (_seedDefinition == null)
            {
                Logger.Error($"_seedDefinition is NULL on '{gameObject.name}' — slot will not function!");
                return;
            }

            if (SceneNetworking.IsNetworkReady)
            {
                Logger.Info($"'{gameObject.name}' — network already ready at Start(), calling SpawnSeed()");
                SpawnSeed();
            }
            else
            {
                SceneNetworking.OnLocalPlayerJoined += SpawnSeed;
                Logger.Info($"'{gameObject.name}' — network not ready, subscribed to OnLocalPlayerJoined");
            }
        }

        private void OnDestroy()
        {
            Logger.Log($"OnDestroy() '{gameObject.name}'");
            SceneNetworking.OnLocalPlayerJoined -= SpawnSeed;
        }

        private void Update()
        {
            if (!SceneNetworking.IsMasterClient) return;

            bool seedMissing = _currentSeed == null
                            || _currentSeed.IsInPool   // planted, grown, sold — returned to pool
                            || !_currentSeed.IsSeed;   // planted but not yet returned to pool

            if (seedMissing)
            {
                if (_currentSeed == null)
                    Logger.Warn($"'{gameObject.name}' — no seed present, attempting to respawn");
                else
                    Logger.Info($"'{gameObject.name}' — seed '{_currentSeed.name}' no longer in shop (IsInPool={_currentSeed.IsInPool}, IsSeed={_currentSeed.IsSeed}), respawning");
                _currentSeed = null;
                SpawnSeed();
            }
        }

        private void SpawnSeed()
        {
            Logger.Info($"SpawnSeed() called on '{gameObject.name}' — IsMasterClient={SceneNetworking.IsMasterClient}, currentSeed={(_currentSeed != null ? _currentSeed.name : "null")}");

            if (!SceneNetworking.IsMasterClient)
            {
                Logger.Log($"SpawnSeed() '{gameObject.name}' — not master, skipping");
                return;
            }

            _currentSeed = PoolManager.Instance.ClaimPlantSeed(_seedDefinition.seedId);
            if (_currentSeed == null)
            {
                Logger.Warn($"'{gameObject.name}' — pool empty for '{_seedDefinition.seedId}', slot is empty");
                return;
            }

            Logger.Info($"'{gameObject.name}' — claimed seed '{_currentSeed.name}', positioning at {transform.position}");

            _currentSeed.transform.position = transform.position;
            _currentSeed.transform.rotation = transform.rotation;

            _currentSeed.SetState(true);
            _currentSeed.InShop = true;
            _currentSeed.IsBought = false;
            Logger.Info($"SpawnSeed() complete — '{_currentSeed.name}' placed at {_currentSeed.transform.position}");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out PlantSeed seed) && seed.IsSeed)
            {
                Logger.Info($"OnTriggerEnter() '{gameObject.name}' — seed '{seed.name}' entered shop");
                if(!seed.IsBought) _currentSeed = seed;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out PlantSeed seed) && seed == _currentSeed)
            {
                if (!seed.IsBought && seed.InShop)
                {
                    Logger.Info($"OnTriggerExit() '{gameObject.name}' — seed '{seed.name}' exited, IsMasterClient={SceneNetworking.IsMasterClient}");
                    _currentSeed.InShop = false;
                    _currentSeed.IsBought = true;
                    _currentSeed.broadcastState();
                    EconomyManager.Instance.RemoveBalance(seed.GetGrabber().GetID(), _seedDefinition.buyPrice);
                    _currentSeed = null;
                    if (SceneNetworking.IsMasterClient)
                        SpawnSeed();
                }
            }
        }
    }
}
