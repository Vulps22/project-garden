using Fusion;
using SomniumSpace.Network.Bridge;
using System.Collections;
using UnityEngine;

namespace GrowAGarden
{
    public class ShopSlot : MonoBehaviour
    {
        [SerializeField] private SeedDefinition _seedDefinition;
        private SeedObject _currentSeed;
        private const float TIMEOUT = 2f;

        private void Start()
        {
            Debug.Log($"[ShopSlot] Start() on '{gameObject.name}' — seedDefinition={((_seedDefinition != null) ? _seedDefinition.seedId : "NULL")}");

            if (_seedDefinition == null)
            {
                Debug.LogError($"[ShopSlot] _seedDefinition is NULL on '{gameObject.name}' — slot will not function!");
                return;
            }

            if (SceneNetworking.IsNetworkReady)
            {
                Debug.Log("Spawned Seed as Master Client at Start()");
                SpawnSeed();
            }
            else
            {
                SceneNetworking.OnLocalPlayerJoined += SpawnSeed;
                Debug.Log($"[ShopSlot] Subscribed to OnBecomeWorldMaster on '{gameObject.name}'");
            }
        }

        private void OnDestroy()
        {
            Debug.Log($"[ShopSlot] OnDestroy() on '{gameObject.name}'");
            SceneNetworking.OnBecomeWorldMaster -= SpawnSeed;
        }

        private void SpawnSeed()
        {
            Debug.Log($"[ShopSlot] SpawnSeed() called on '{gameObject.name}' — seedId='{_seedDefinition.seedId}', currentSeed={((_currentSeed != null) ? _currentSeed.name : "null")}");

            if (!SceneNetworking.IsMasterClient) return;

            _currentSeed = PoolManager.Instance.ClaimSeed(_seedDefinition.seedId);
            if (_currentSeed == null)
            {
                Debug.LogWarning($"[ShopSlot] Pool empty for '{_seedDefinition.seedId}', slot is empty.");
                return;
            }

            Debug.Log($"[ShopSlot] Claimed seed '{_currentSeed.name}' for slot '{gameObject.name}' — positioning at {transform.position}");

            _currentSeed.transform.position = transform.position;
            _currentSeed.transform.rotation = transform.rotation;

            // Re-enable interaction components that the pool disables on Restore
            var rb = _currentSeed.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                Debug.Log($"[ShopSlot] Set Rigidbody kinematic on '{_currentSeed.name}'");
            }
            else
            {
                Debug.LogWarning($"[ShopSlot] No Rigidbody found on seed '{_currentSeed.name}'");
            }

            Debug.Log($"[ShopSlot] SpawnSeed() complete on '{gameObject.name}' — seed '{_currentSeed.name}' placed at {_currentSeed.transform.position}");
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out SeedObject seed))
            {
                Debug.Log($"[ShopSlot] OnTriggerExit() on '{gameObject.name}' — seed '{seed.name}' exited, IsMasterClient={SceneNetworking.IsMasterClient}");
                if (SceneNetworking.IsMasterClient)
                {
                    Debug.Log($"[ShopSlot] Master client respawning seed for slot '{gameObject.name}'");
                    SpawnSeed();
                }
            }
        }
    }
}