using SomniumSpace.Network.Bridge;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Returns state authority to the master client for objects that belong to the world rather
    /// than to a player — stock sitting in a shop slot, and seeds parked in the pool.
    ///
    /// Only the master ever requests, and only when its <see cref="IAuthoritySource"/> says the
    /// object is unowned by any player. That holder check is what keeps this from fighting
    /// NetworkGrabbable: a seed in someone's hand is never claimed out from under them.
    ///
    /// Re-evaluates on lifecycle changes and on becoming master, so authority that was stranded
    /// with another player is reclaimed after a master client transfer rather than waiting for
    /// them to disconnect (SceneNetworking only reassigns authority that is None).
    /// </summary>
    public class AuthorityController : MonoBehaviour
    {
        [SerializeField] private NetworkBridge _networkBridge;

        private IAuthoritySource _source;
        private ILifecycleNotifier _notifier;

        private void Awake()
        {
            _source = GetComponent<IAuthoritySource>();
            _notifier = GetComponent<ILifecycleNotifier>();

            if (_source == null)
                Logger.Error($"Awake() '{gameObject.name}' — no IAuthoritySource on this object, authority will never return to the master");
        }

        private void OnEnable()
        {
            if (_notifier != null) _notifier.LifecycleChanged += Apply;
            SceneNetworking.OnBecomeWorldMaster += Apply;
        }

        private void OnDisable()
        {
            if (_notifier != null) _notifier.LifecycleChanged -= Apply;
            SceneNetworking.OnBecomeWorldMaster -= Apply;
        }

        /// <summary>
        /// Claims authority if the master should hold it and does not. Requesting is
        /// asynchronous; PlantSeed re-broadcasts its state once the transfer lands, so the
        /// state that could not be sent while unowned is not lost.
        /// </summary>
        public void Apply()
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (_source == null || !_source.ShouldMasterOwn) return;

            var obj = _networkBridge == null ? null : _networkBridge.Object;
            if (obj == null || obj.HasStateAuthority) return;

            obj.RequestStateAuthority();
        }

        private void OnValidate()
        {
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkBridge>();
        }
    }
}
