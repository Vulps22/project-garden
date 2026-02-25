using Fusion;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace GrowAGarden
{
    [HelpURL("https://incrediworlds.gitbook.io/somnium-space-dendoc/worlds-creation/community-modules/community-networking")]
    public class NetworkAnimator : MonoBehaviour
    {
        [Header("Alpha V2.0 [2025, 10, 07]")]
        [SerializeField] protected NetworkObject _networkObject;
        [SerializeField][FormerlySerializedAs("_networkedAnimator")] private NetworkMecanimAnimator _networkMecanimeAnimator;

        private const float TIMEOUT = 2.0f;

        private Animator _animator;

        private const NetworkObjectFlags FLAGS = NetworkObjectFlags.V1 | NetworkObjectFlags.AllowStateAuthorityOverride;

        private void Awake()
        {
            if (_networkObject == null)
            { 
                Debug.LogError($"[{nameof(NetworkAnimator)}] NetworkObject is not assigned in the inspector.");
                return;
            }

            ChecNetworkObjectFlags();
            _animator = _networkMecanimeAnimator.Animator;
        }

        /// <summary>
        /// Set a trigger on the animator. This should be quite reliable
        /// </summary>
        /// <param name="name"></param>
        public void SetTrigger(string name)
        {
            if (Application.isEditor) // Local debug
            {
                _networkMecanimeAnimator.Animator.SetTrigger(name);
                return;
            }

            if (_networkObject.HasStateAuthority)
            {
                //DebugUI.Log(nameof(NetworkAnimator), "Setting trigger direct " + _triggerName);
                _networkMecanimeAnimator.SetTrigger(name);
            }
            else
            {
                //DebugUI.Log(nameof(NetworkAnimator), "Setting trigger, waiting for authority " + _triggerName);
                StartCoroutine(GetStateAuthority(() => _networkMecanimeAnimator.SetTrigger(name)));
            }
        }

        public void SetBool(bool value, string name)
        {
            if (Application.isEditor) // Local debug
            {
                _networkMecanimeAnimator.Animator.SetBool(name, value);
                return;
            }

            if (_networkObject.HasStateAuthority)
            {
                _animator.SetBool(name, value);
            }
            else
            {
                StartCoroutine(GetStateAuthority(() => _animator.SetBool(name, value)));
            }
        }

        public void SetInt(int value, string name)
        {
            if (Application.isEditor) // Local debug
            {
                _networkMecanimeAnimator.Animator.SetInteger(name, value);
                return;
            }

            if (_networkObject.HasStateAuthority)
            {
                _animator.SetInteger(name, value);
            }
            else
            {
                StartCoroutine(GetStateAuthority(() => _animator.SetInteger(name, value)));
            }
        }

        public void SetFloat(float value, string name)
        {
            if (Application.isEditor) // Local debug
            {
                _networkMecanimeAnimator.Animator.SetFloat(name, value);
                return;
            }

            if (_networkObject.HasStateAuthority)
            {
                _animator.SetFloat(name, value);
            }
            else
            {
                StartCoroutine(GetStateAuthority(() => _animator.SetFloat(name, value)));
            }
        }

        private IEnumerator GetStateAuthority(Action callback)
        {
            if (Application.isEditor)
            {
                callback.Invoke();
                yield break;
            }

            _networkObject.RequestStateAuthority();
            float startTime = Time.time;
            while (!_networkObject.HasStateAuthority)
            {
                if (Time.time - startTime > TIMEOUT)
                {
                    Debug.LogWarning($"[{nameof(NetworkAnimator)}] Timeout, can't get object control");
                    yield break;
                }
                yield return null; // Wait for the next frame
            }
            callback.Invoke();
        }

        private void OnValidate()
        {
            if (_networkObject == null)
                _networkObject = GetComponent<NetworkObject>();
            if (_networkObject == null)
                _networkObject = gameObject.AddComponent<NetworkObject>();
        
            if(_networkMecanimeAnimator == null)
                _networkMecanimeAnimator = GetComponent<NetworkMecanimAnimator>();

            ChecNetworkObjectFlags();
        }

        private void ChecNetworkObjectFlags()
        {
            if (_networkObject != null && _networkObject.Flags != FLAGS)
            {
                _networkObject.Flags = FLAGS;
            }
        }
    }
}
