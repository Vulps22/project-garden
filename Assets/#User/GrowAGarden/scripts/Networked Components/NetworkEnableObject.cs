using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;
using UnityEngine.Events;

namespace GrowAGarden
{
    public class NetworkEnableObject : MonoBehaviour
    {
        [Header("Version [2025, 10, 23]")]
        [SerializeField] private NetworkBridge _networkBridge;
        [SerializeField] private NetworkObject _networkObject;
        [Space]
        [SerializeField] private bool _defaultEnable = true;
        [SerializeField] private GameObject[] _controledGameObjects;
        [SerializeField] private UnityEvent<bool> _onChange;

        void Start()
        {
            // 1. Connect events
            _networkBridge.OnSpawned += OnSpawn;
            _networkBridge.OnMessageToAll += OnMessageToAll;
        }

        public void SetTargetEnabled(bool isEnabled)
        {
            // 3. Send message to all
            _networkBridge.RPC_SendMessageToAll(0, new byte[] { (byte)(isEnabled ? 1 : 0) });
        }

        private void OnSpawn()
        {
            // 2. On object spawn logic:
            // Controller Write initial variable and setup state
            if (_networkBridge.Object.HasStateAuthority)
            {
                SetEnable(_defaultEnable);
                SetVariable(_defaultEnable);
            }
            // Proxies Read variable and setup state (Late joiner will sync with everyone)
            else
            {
                SetEnable(GetVariable());
            }
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            // 4. Receive message from caller
            bool enable = data[0] != 0;
            // Controller Write changed variable
            if (_networkBridge.Object.HasStateAuthority)
            {
                SetVariable(enable);
            }
            SetEnable(enable);
        }

        private void SetEnable(bool enable)
        {
            if (_controledGameObjects != null)
            {
                foreach (var go in _controledGameObjects)
                {
                    if (go != null)
                        go.SetActive(enable);
                }
            }
            _onChange?.Invoke(enable);
        }

        private bool GetVariable()
        {
            return _networkBridge.SyncByteArray.Get(0) != 0;
        }

        private void SetVariable(bool enabled)
        {
            _networkBridge.SyncByteArray.Set(0, (byte)(enabled ? 1 : 0));
        }
    }
}
