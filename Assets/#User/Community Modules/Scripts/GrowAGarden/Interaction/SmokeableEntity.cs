using System.Collections;
using Fusion;
using UnityEngine;
using Vulps.GrowAGarden.Ownership;
using Vulps.GrowAGarden.Trip;

namespace Vulps.GrowAGarden.Interaction
{
    /// <summary>
    /// Something smoked by holding it to the mouth. Every few seconds there it is a puff: smoke every
    /// player sees, and a little more of a mild high for the smoker. Used up after a set number of
    /// puffs, and ended by whoever is holding it, who has authority over it.
    /// </summary>
    [RequireComponent(typeof(CollectibleEntity))]
    public class SmokeableEntity : MonoBehaviour
    {
        /// <summary>CollectibleEntity's bridge; CollectibleMessageType reserves 8 for this.</summary>
        private const byte PUFF_ID = 8;

        [SerializeField] private NetworkMessenger _bridge;

        [Tooltip("The smoke a puff gives off.")]
        [SerializeField] private ParticleSystem _smoke;

        [Tooltip("Particles in one puff.")]
        [SerializeField] private int _particlesPerPuff = 14;

        [Tooltip("Puffs before it is used up.")]
        [SerializeField] private int _puffs = 12;

        [Tooltip("Seconds held at the mouth for each puff.")]
        [SerializeField] private float _puffSeconds = 5f;

        [Tooltip("Seconds of high each puff adds.")]
        [SerializeField] private float _secondsPerPuff = 20f;

        [Tooltip("Strength of the high, 0 to 1. Mild.")]
        [SerializeField] private float _strength = 0.25f;

        [Tooltip("How close to the mouth counts as smoking, in metres.")]
        [SerializeField] private float _mouthRadius = 0.12f;

        private CollectibleEntity _item;
        private int _smoked;
        private float _atMouth;
        private bool _finishing;

        private void Awake() => _item = GetComponent<CollectibleEntity>();

        private void OnEnable()
        {
            if (_bridge != null) _bridge.MessageToAll += OnMessageToAll;
        }

        private void OnDisable()
        {
            if (_bridge != null) _bridge.MessageToAll -= OnMessageToAll;
        }

        private void Update()
        {
            if (_finishing || !HeldByMe() || !AtMouth()) { _atMouth = 0f; return; }

            _atMouth += Time.deltaTime;
            if (_atMouth < _puffSeconds) return;
            _atMouth -= _puffSeconds;
            Puff();
        }

        private bool HeldByMe() =>
            !string.IsNullOrEmpty(_item.HolderId) && _item.HolderId == PlayerManager.LocalPlayerId;

        private bool AtMouth()
        {
            Vector3? mouth = PlayerManager.LocalMouthPosition;
            return mouth != null && Vector3.Distance(mouth.Value, transform.position) <= _mouthRadius;
        }

        /// <summary>The smoker's side: count it, feel it, and tell everyone so they see the smoke.</summary>
        private void Puff()
        {
            int puff = _smoked + 1;
            TripManager.Extend(_strength, _secondsPerPuff, $"puff {puff}/{_puffs} on '{gameObject.name}'");
            if (_bridge != null && _bridge.CanSend) _bridge.SendToAll(PUFF_ID, new[] { (byte)Mathf.Min(puff, 255) });
            else OnPuff(puff);

            if (puff >= _puffs) StartCoroutine(UseUp());
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            if (id != PUFF_ID || data == null || data.Length < 1) return;
            OnPuff(data[0]);
        }

        /// <summary>Every client: the smoke, and the count, so whoever holds it next carries on from here.</summary>
        private void OnPuff(int puff)
        {
            _smoked = Mathf.Max(_smoked, puff);
            if (_smoke != null) _smoke.Emit(_particlesPerPuff);
            if (_smoked < _puffs || _smoke == null) return;

            // Spent: the last smoke drifts off on its own while the splif goes.
            _smoke.transform.SetParent(null, true);
            var main = _smoke.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _smoke = null;
        }

        /// <summary>The holder has authority over what it holds, so the holder ends it.</summary>
        private IEnumerator UseUp()
        {
            _finishing = true;
            yield return new WaitForSeconds(0.5f);

            NetworkObject obj = _bridge != null ? _bridge.Object : GetComponent<NetworkObject>();
            bool gone = WorldManager.DespawnIfStateAuthority(obj);
            Logger.Info($"UseUp() '{gameObject.name}' — smoked {_puffs} times; {(gone ? "used up" : "could not end it: no authority")}");
            if (!gone) _finishing = false;
        }

        private void OnValidate()
        {
            if (_bridge == null) _bridge = GetComponent<NetworkMessenger>();
            if (_smoke == null) _smoke = GetComponentInChildren<ParticleSystem>(true);
            if (_puffs < 1) _puffs = 1;
            if (_puffSeconds < 0.5f) _puffSeconds = 0.5f;
        }
    }
}
