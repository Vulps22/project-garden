using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Vulps.GrowAGarden.Islands;
using Vulps.GrowAGarden.Motion;

namespace Vulps.GrowAGarden.Ownership
{
    /// <summary>
    /// Moves state authority to the client that should simulate this object, as its
    /// <see cref="IAuthoritySource"/> declares: the master, a player in the object's sector, or
    /// whoever holds it.
    ///
    /// Every client works out the same pick from the sector table, and only the pick asks. A held object is never claimed
    /// out of a hand. Event driven: the sector table changing, the master changing, the object spawning or changing hands,
    /// and the object itself coming to rest somewhere new, which the client simulating it reports (released, landed,
    /// recalled) through <see cref="SectorManager.ReportObjectSector"/>.
    /// </summary>
    public class AuthorityController : MonoBehaviour
    {
        [SerializeField] private NetworkMessenger _networkBridge;

        private IAuthoritySource _source;
        private ILifecycleNotifier _notifier;
        private XRGrabInteractable _grab;
        private ReturnableEntity _returnable;
        private uint _registeredId;
        private Vector2Int _sector;       // where the simulating client last said it is; used everywhere over the lagging transform
        private bool _hasSector;

        private void Awake()
        {
            _source = GetComponent<IAuthoritySource>();
            _notifier = GetComponent<ILifecycleNotifier>();
            _grab = GetComponent<XRGrabInteractable>();
            _returnable = GetComponent<ReturnableEntity>();

            if (_source == null)
                Logger.Error($"Awake() '{gameObject.name}' — no IAuthoritySource on this object, authority will never be moved");
        }

        private void OnEnable()
        {
            if (_notifier != null) _notifier.LifecycleChanged += Apply;
            PlayerManager.BecameWorldMaster += Apply;
            SectorManager.SectorsChanged += Apply;
            if (_networkBridge != null)
            {
                _networkBridge.OnSpawned += OnSpawned;
                _networkBridge.OnStateAuthorityChanged += OnAuthorityChanged;
            }
            if (_grab != null) _grab.lastSelectExited.AddListener(OnReleased);
            if (_returnable != null) _returnable.Arrived += ReportSector;
            OnSpawned();
        }

        private void OnDisable()
        {
            if (_notifier != null) _notifier.LifecycleChanged -= Apply;
            PlayerManager.BecameWorldMaster -= Apply;
            SectorManager.SectorsChanged -= Apply;
            if (_networkBridge != null)
            {
                _networkBridge.OnSpawned -= OnSpawned;
                _networkBridge.OnStateAuthorityChanged -= OnAuthorityChanged;
            }
            if (_grab != null) _grab.lastSelectExited.RemoveListener(OnReleased);
            if (_returnable != null) _returnable.Arrived -= ReportSector;
            if (_registeredId != 0) SectorManager.UnregisterObject(_registeredId);
            _registeredId = 0;
        }

        /// <summary>Spawned (or already spawned when enabled): listen for its sector reports and decide once.</summary>
        private void OnSpawned()
        {
            var obj = _networkBridge == null ? null : _networkBridge.Object;
            if (obj == null || obj.Id.Raw == 0) return;
            if (_registeredId != obj.Id.Raw)
            {
                if (_registeredId != 0) SectorManager.UnregisterObject(_registeredId);
                _registeredId = obj.Id.Raw;
                SectorManager.RegisterObject(_registeredId, OnSectorReported);
            }
            // Every client sees the same spawn pose, so the starting sector needs no message.
            if (!_hasSector) { _sector = SectorManager.SectorOf(transform.position); _hasSector = true; }
            Apply();
        }

        private void OnAuthorityChanged(bool hasAuthority)
        {
            if (hasAuthority) ReportSector();
            else Apply();
        }

        private void OnReleased(SelectExitEventArgs _) => ReportSector();

        // Landing is a physics event, and physics runs only where the object is simulated.
        private void OnCollisionEnter(Collision _) => ReportSector();

        /// <summary>The client simulating it says where it is now, if that is a new sector.</summary>
        private void ReportSector()
        {
            if (_source == null || _source.Authority != AuthorityRule.Sector) return;
            var obj = _networkBridge == null ? null : _networkBridge.Object;
            if (obj == null || !obj.HasStateAuthority || obj.Id.Raw == 0) return;
            Vector2Int sector = SectorManager.SectorOf(transform.position);
            if (_hasSector && sector == _sector) return;
            _sector = sector;
            _hasSector = true;
            SectorManager.ReportObjectSector(obj.Id.Raw, sector);
        }

        private void OnSectorReported(Vector2Int sector)
        {
            _sector = sector;
            _hasSector = true;
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
                    Vector2Int sector = _hasSector ? _sector : SectorManager.SectorOf(transform.position);
                    int me = SectorManager.LocalPeerId;
                    int pick = SectorManager.PickFor(sector);
                    // Nobody in its sector and nobody simulating it: the master keeps it, so it still has state to send joiners.
                    if (pick < 0) { if (PlayerManager.IsMaster && obj.StateAuthority.IsNone) break; return; }
                    if (me < 0 || pick != me) return;
                    if (!obj.StateAuthority.IsNone && SectorManager.IsPeerIn(obj.StateAuthority.PlayerId, sector)) return;
                    // Never take it from a player who has not said where they are yet: they may be holding it.
                    if (!obj.StateAuthority.IsNone && !SectorManager.IsKnownPeer(obj.StateAuthority.PlayerId)) return;
                    break;

                default:
                    return;
            }

            obj.RequestStateAuthority();
        }

        private void OnValidate()
        {
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkMessenger>();
        }
    }
}
