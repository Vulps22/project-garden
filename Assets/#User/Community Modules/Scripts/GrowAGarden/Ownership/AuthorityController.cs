using UnityEngine;
using Vulps.GrowAGarden.Islands;
using Vulps.GrowAGarden.Motion;

namespace Vulps.GrowAGarden.Ownership
{
    /// <summary>
    /// Moves state authority to the client that should simulate this object, as its
    /// <see cref="IAuthoritySource"/> declares: the master, a player in the object's sector, or
    /// whoever holds it.
    ///
    /// Every client works out the same pick from the sector table, and only the pick asks, so no
    /// message is needed. A held object is never claimed out of a hand.
    /// </summary>
    public class AuthorityController : MonoBehaviour
    {
        private const float CHECK_SECONDS = 1f;
        private const float REQUEST_GAP_SECONDS = 0.5f;

        [SerializeField] private NetworkMessenger _networkBridge;

        private IAuthoritySource _source;
        private ILifecycleNotifier _notifier;
        private float _nextCheck;
        private float _lastRequest = float.NegativeInfinity;

        private void Awake()
        {
            _source = GetComponent<IAuthoritySource>();
            _notifier = GetComponent<ILifecycleNotifier>();

            if (_source == null)
                Logger.Error($"Awake() '{gameObject.name}' — no IAuthoritySource on this object, authority will never be moved");
        }

        private void OnEnable()
        {
            if (_notifier != null) _notifier.LifecycleChanged += Apply;
            PlayerManager.BecameWorldMaster += Apply;
            SectorManager.SectorsChanged += Apply;
            _nextCheck = Time.time + Random.value * CHECK_SECONDS;   // spread the checks across frames
        }

        private void OnDisable()
        {
            if (_notifier != null) _notifier.LifecycleChanged -= Apply;
            PlayerManager.BecameWorldMaster -= Apply;
            SectorManager.SectorsChanged -= Apply;
        }

        /// <summary>Objects move between sectors without any event, so the check also runs on a timer.</summary>
        private void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + CHECK_SECONDS;
            Apply();
        }

        /// <summary>Asks for authority if this client is the one that should have it and does not.</summary>
        public void Apply()
        {
            if (_source == null) return;

            var obj = _networkBridge == null ? null : _networkBridge.Object;
            if (obj == null || obj.HasStateAuthority) return;

            switch (_source.Authority)
            {
                case AuthorityRule.Master:
                    if (!PlayerManager.IsMaster) return;
                    break;

                case AuthorityRule.Sector:
                    Vector2Int sector = SectorManager.SectorOf(transform.position);
                    int me = SectorManager.LocalPeerId;
                    if (me < 0 || SectorManager.PickFor(sector) != me) return;
                    if (!obj.StateAuthority.IsNone && SectorManager.IsPeerIn(obj.StateAuthority.PlayerId, sector)) return;
                    break;

                default:
                    return;
            }

            if (Time.time - _lastRequest < REQUEST_GAP_SECONDS) return;
            _lastRequest = Time.time;
            obj.RequestStateAuthority();
        }

        private void OnValidate()
        {
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkMessenger>();
        }
    }
}
