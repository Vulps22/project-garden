using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Vulps.GrowAGarden.Islands;
using Vulps.GrowAGarden.Ownership;

namespace Vulps.GrowAGarden.Motion
{
    /// <summary>
    /// The single writer of Rigidbody.isKinematic and useGravity outside a grab. The client simulating the object gives it
    /// the mode its <see cref="IKinematicSource"/> declares; every other client holds it kinematic, because
    /// NetworkTransform moves it there. While a hand holds it, XRI owns the body; on release this applies again, since
    /// XRI restores whatever it snapshotted at the grab. See docs/networking.md → Fusion 2.1.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KinematicController : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private NetworkMessenger _networkBridge;
        [SerializeField] private XRGrabInteractable _grabInteractable;


        private IKinematicSource _source;
        private ILifecycleNotifier _notifier;
        private IAuthoritySource _authority;
        private ReturnableEntity _returnable;
        private bool _stranded;

        private void Awake()
        {
            // Resolved by interface rather than by concrete type: this component knows nothing
            // about seeds, and anything declaring a kinematic intent can use it.
            _source = GetComponent<IKinematicSource>();
            _notifier = GetComponent<ILifecycleNotifier>();
            _authority = GetComponent<IAuthoritySource>();
            _returnable = GetComponent<ReturnableEntity>();

            if (_source == null)
                Logger.Error($"Awake() '{gameObject.name}' — no IKinematicSource on this object, kinematic state will not be driven");
        }

        private void OnEnable()
        {
            if (_notifier != null) _notifier.LifecycleChanged += Apply;
            if (_grabInteractable != null) _grabInteractable.lastSelectExited.AddListener(OnReleased);
            if (_networkBridge != null) _networkBridge.OnStateAuthorityChanged += OnAuthorityChanged;
            if (_returnable != null) _returnable.Arrived += Apply;
            IslandField.GroundChanged += OnGroundChanged;
        }

        private void OnDisable()
        {
            if (_notifier != null) _notifier.LifecycleChanged -= Apply;
            if (_grabInteractable != null) _grabInteractable.lastSelectExited.RemoveListener(OnReleased);
            if (_networkBridge != null) _networkBridge.OnStateAuthorityChanged -= OnAuthorityChanged;
            if (_returnable != null) _returnable.Arrived -= Apply;
            IslandField.GroundChanged -= OnGroundChanged;
        }

        private void OnReleased(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs _) => Apply();

        /// <summary>Islands were placed or removed: an object this client simulates may have lost or found its ground.</summary>
        private void OnGroundChanged()
        {
            var obj = _networkBridge == null ? null : _networkBridge.Object;
            if (obj != null && obj.HasStateAuthority && IsStranded() != _stranded) Apply();
        }

        /// <summary>
        /// This client simulates a sector-ruled object whose ground it has not loaded, so it would
        /// fall through where the island should be. It waits, kinematic, for a player in its sector.
        /// </summary>
        private bool IsStranded() =>
            _authority != null && _authority.Authority == AuthorityRule.Sector && !SectorManager.HasGroundAt(transform.position);

        // Authority may arrive after the grab already ended, so apply on the transition too; losing it makes this a follower.
        private void OnAuthorityChanged(bool hasAuthority) => Apply();

        /// <summary>
        /// Writes the declared mode to the rigidbody. No-op without authority, because the
        /// value would just be replaced by the owner's on the next sync.
        /// </summary>
        public void Apply()
        {
            if (_source == null || _rigidbody == null) return;

            // A hand holding it owns the body until release (XRI makes it kinematic and weightless); release applies again.
            if (_grabInteractable != null && _grabInteractable.isSelected) return;

            bool wantGravity = _source.ShouldUseGravity;
            if (_rigidbody.useGravity != wantGravity) _rigidbody.useGravity = wantGravity;

            // NetworkTransform moves a proxy but replicates nothing about its body: a physical proxy falls between ticks
            // and is snapped back, so it bounces. Only the client simulating it lets it be physical.
            var obj = _networkBridge == null ? null : _networkBridge.Object;
            if (obj == null || !obj.HasStateAuthority)
            {
                if (obj != null && !_rigidbody.isKinematic) _rigidbody.isKinematic = true;
                return;
            }

            bool stranded = IsStranded();
            if (stranded != _stranded)
            {
                _stranded = stranded;
                Logger.Info($"Apply() '{gameObject.name}' — {(stranded ? "no ground loaded here; holding it still" : "ground loaded; physical again")} at {transform.position}");
            }

            bool wantKinematic = _source.ShouldBeKinematic || stranded;
            if (_rigidbody.isKinematic != wantKinematic) _rigidbody.isKinematic = wantKinematic;

            // A body that went to sleep while kinematic will not respond to the tether's
            // forces until something wakes it.
            if (!wantKinematic && _rigidbody.IsSleeping()) _rigidbody.WakeUp();
        }

        private void OnValidate()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkMessenger>();
            if (_grabInteractable == null) _grabInteractable = GetComponent<XRGrabInteractable>();
        }
    }
}
