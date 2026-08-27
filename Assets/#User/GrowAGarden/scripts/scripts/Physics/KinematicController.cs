using SomniumSpace.Network.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    /// <summary>
    /// The single writer of Rigidbody.isKinematic. Its only job is to keep the rigidbody in
    /// the mode its <see cref="IKinematicSource"/> declares.
    ///
    /// Fusion replicates isKinematic as part of the network state (NetworkRigidbodyFlags),
    /// owned by the state authority and pushed onto every proxy — so a write on a client that
    /// does not own the object is overwritten on the next sync and achieves nothing. This only
    /// writes when it holds authority; every other client receives the result for free.
    ///
    /// Re-asserting after a grab is the point. XRGrabInteractable snapshots isKinematic when a
    /// grab starts and restores that snapshot on release, and during the window between
    /// requesting authority and receiving it the snapshot it takes is whatever the previous
    /// owner happened to be broadcasting. Applying again on release means the last word belongs
    /// to something that knows the seed's actual state.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KinematicController : MonoBehaviour
    {
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private NetworkBridge _networkBridge;
        [SerializeField] private XRGrabInteractable _grabInteractable;

        private IKinematicSource _source;
        private ILifecycleNotifier _notifier;

        private void Awake()
        {
            // Resolved by interface rather than by concrete type: this component knows nothing
            // about seeds, and anything declaring a kinematic intent can use it.
            _source = GetComponent<IKinematicSource>();
            _notifier = GetComponent<ILifecycleNotifier>();

            if (_source == null)
                Logger.Error($"Awake() '{gameObject.name}' — no IKinematicSource on this object, kinematic state will not be driven");
        }

        private void OnEnable()
        {
            if (_notifier != null) _notifier.LifecycleChanged += Apply;
            if (_grabInteractable != null) _grabInteractable.lastSelectExited.AddListener(OnReleased);
            if (_networkBridge != null) _networkBridge.OnStateAuthorityChanged += OnAuthorityChanged;
        }

        private void OnDisable()
        {
            if (_notifier != null) _notifier.LifecycleChanged -= Apply;
            if (_grabInteractable != null) _grabInteractable.lastSelectExited.RemoveListener(OnReleased);
            if (_networkBridge != null) _networkBridge.OnStateAuthorityChanged -= OnAuthorityChanged;
        }

        private void OnReleased(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs _) => Apply();

        // Authority may arrive after the grab already ended, so apply on the transition too.
        private void OnAuthorityChanged(bool hasAuthority)
        {
            if (hasAuthority) Apply();
        }

        /// <summary>
        /// Writes the declared mode to the rigidbody. No-op without authority, because the
        /// value would just be replaced by the owner's on the next sync.
        /// </summary>
        public void Apply()
        {
            if (_source == null || _rigidbody == null) return;

            // Gravity is applied on EVERY client, unlike isKinematic.
            //
            // NetworkRigidbody3D.GetFlags() packs useGravity into NetworkRigidbodyFlags and
            // sends it, but nothing on the receiving side ever reads it back out -- isKinematic
            // is applied to proxies, useGravity is not. So a proxy keeps whatever the prefab
            // shipped with, which is true. Once shop stock became non-kinematic that left every
            // non-master client with a seed that had weight and no tether to hold it, and the
            // stock fell out of the barrows on load.
            //
            // The lifecycle flags this derives from are replicated (broadcastState), and the
            // proxy state-sync path raises LifecycleChanged, so every client can work out the
            // right answer for itself.
            bool wantGravity = _source.ShouldUseGravity;
            if (_rigidbody.useGravity != wantGravity) _rigidbody.useGravity = wantGravity;

            // isKinematic IS replicated from the authority, so writing it on a proxy would be
            // undone on the next sync. Only the owner sets it.
            var obj = _networkBridge == null ? null : _networkBridge.Object;
            if (obj == null || !obj.HasStateAuthority) return;

            bool wantKinematic = _source.ShouldBeKinematic;
            if (_rigidbody.isKinematic != wantKinematic) _rigidbody.isKinematic = wantKinematic;

            // A body that went to sleep while kinematic will not respond to the tether's
            // forces until something wakes it.
            if (!wantKinematic && _rigidbody.IsSleeping()) _rigidbody.WakeUp();
        }

        private void OnValidate()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkBridge>();
            if (_grabInteractable == null) _grabInteractable = GetComponent<XRGrabInteractable>();
        }
    }
}
