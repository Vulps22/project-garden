using Fusion;
using SomniumSpace.Network.Bridge;
using System.Collections;
using UnityEngine;

namespace GrowAGarden
{
    public class ShopSlot : MonoBehaviour
    {
        [SerializeField] private SeedDefinition _seedDefinition;
        private UnifiedPlantSeed _currentSeed;
        private const float TIMEOUT = 2f;

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

        private void SpawnSeed()
        {
            Logger.Info($"SpawnSeed() called on '{gameObject.name}' — IsMasterClient={SceneNetworking.IsMasterClient}, currentSeed={(_currentSeed != null ? _currentSeed.name : "null")}");

            if (!SceneNetworking.IsMasterClient)
            {
                Logger.Log($"SpawnSeed() '{gameObject.name}' — not master, skipping");
                return;
            }

            _currentSeed = PoolManager.Instance.claimUnifiedPlantSeed(_seedDefinition.seedId);
            if (_currentSeed == null)
            {
                Logger.Warn($"'{gameObject.name}' — pool empty for '{_seedDefinition.seedId}', slot is empty");
                return;
            }

            Logger.Info($"'{gameObject.name}' — claimed seed '{_currentSeed.name}', positioning at {transform.position}");

            _currentSeed.transform.position = transform.position;
            _currentSeed.transform.rotation = transform.rotation;

            _currentSeed.SetState(true);
            Logger.Info($"SpawnSeed() complete — '{_currentSeed.name}' placed at {_currentSeed.transform.position}");
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out UnifiedPlantSeed seed))
            {
                Logger.Info($"OnTriggerExit() '{gameObject.name}' — seed '{seed.name}' exited, IsMasterClient={SceneNetworking.IsMasterClient}");
                if (SceneNetworking.IsMasterClient && seed == _currentSeed)
                    SpawnSeed();
            }
        }
    }
}
