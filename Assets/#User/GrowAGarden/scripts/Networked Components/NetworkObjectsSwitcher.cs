using Fusion;
using SomniumSpace.Network.Bridge;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GrowAGarden
{
    public class NetworkObjectsSwitcher : MonoBehaviour
    {
        [Header("Version [2025, 10, 23]")]
        [SerializeField] private NetworkBridge _networkBridge;
        [SerializeField] private NetworkObject _networkObject;
        [Header("Switchable Objects")]
        [SerializeField] private int _defaultIndex = 0;
        [SerializeField] private GameObject[] _switchableObjects;
        [Header("UI (Optional)")]
        [SerializeField] private Button[] _buttons;
        [Tooltip("Elements that will highlight the selection")]
        [SerializeField] private GameObject[] _highlightElements;
        [Header("Transition Effect (Optional)")]
        [SerializeField] private float _fadeInDuration = 0.5f;
        [SerializeField] private float _fadeOutDuration = 0.5f;
        [SerializeField] private UnityEvent _fadeInEvent;

        private bool _inTransition = false;

        private int _editorIndex = 0; // Simulate network variable in editor mode

        void Start()
        {
            _networkBridge.OnSpawned += OnSpawn;
            _networkBridge.OnMessageToAll += OnMessageToAll;
            for(int i = 0; i < _buttons.Length; i++)
            {
                int index = i; // Capture index for the closure
                if(_buttons[i] != null)
                    _buttons[i].onClick.AddListener(() => EnableSelectedObject(index));
            }
        }

        // Manually select the object from outside
        public void EnableSelectedObject(int index)
        {
            if (_inTransition) // Already in transition, do nothing
                return;

            if (NetGetCurrentIndex() == index) // Do not change for the same one
                return;

            if(index < _switchableObjects.Length)
                _networkBridge.RPC_SendMessageToAll(0, new byte[] { (byte)(index) });
        }

        private void OnSpawn()
        {
            if (Application.isEditor || _networkBridge.Object.HasStateAuthority)
            {
                NetSetCurrentIndex(_defaultIndex);
                SetSelectedObject(_defaultIndex);
            }
            else
            {
                int index = NetGetCurrentIndex();
                SetSelectedObject(index);
                SetUIElementSelected(index);
            }
        }

        private void SetUIElementSelected(int index)
        { 
            if(_highlightElements != null)
            {
                for(int i = 0; i < _highlightElements.Length; i++)
                {
                    if(_highlightElements[i] != null)
                        _highlightElements[i].SetActive(i == index);
                }
            }
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            if (_inTransition) // Already in transition, do nothing
                return;

            _inTransition = true;
            int index = data[0];
            if (Application.isEditor || _networkBridge.Object.HasStateAuthority)
            {
                NetSetCurrentIndex(index);
            }

            SetUIElementSelected(index);

            StartCoroutine(FadeIn());
        }

        private IEnumerator FadeIn()
        {
            _inTransition = true;
            _fadeInEvent?.Invoke();
            yield return new WaitForSeconds(_fadeInDuration);
            SetSelectedObject(NetGetCurrentIndex());
            yield return new WaitForSeconds(_fadeOutDuration);
            _inTransition = false;
        }

        private void SetSelectedObject(int index)
        {
            if (_switchableObjects != null)
            {
                for (int i = 0; i < _switchableObjects.Length; i++)
                {
                    if (_switchableObjects[i] != null)
                        _switchableObjects[i].SetActive(i == index);
                }
            }
        }

        // Network variable accessors
        // Mostly used for late joiners here
        private int NetGetCurrentIndex()
        {
            if(Application.isEditor)
                return _editorIndex;
            else
                return _networkBridge.SyncByteArray.Get(0);
        }

        private void NetSetCurrentIndex(int objectIndex)
        {
            if (Application.isEditor)
                _editorIndex = objectIndex;
            else
                _networkBridge.SyncByteArray.Set(0, (byte)(objectIndex));
        }

        // Editor validation
        private void OnValidate()
        {
            if(_networkBridge == null)
                _networkBridge = GetComponent<NetworkBridge>();
            if(_networkObject == null)
                _networkObject = GetComponent<NetworkObject>();
        }
    }
}
