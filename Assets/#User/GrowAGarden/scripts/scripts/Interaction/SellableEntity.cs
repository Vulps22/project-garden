using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Marks something as sellable, and owns the object's half of a sale.
    ///
    /// Selling is not a fact about being a crop. Produce is sellable; a watering can, a bucket of
    /// milk or a sword plausibly are too. So this is a trait you add, in the same family as
    /// KinematicController, SingleHolderFilter, ReturnableEntity, AlignableEntity and
    /// HoveringEntity — components that attach to anything and know nothing about what they are
    /// attached to.
    ///
    /// It carries behaviour rather than only marking, for the same reason the others do: a marker
    /// interface would leave the work in SellPoint, keyed by type, which is exactly the coupling
    /// this removes.
    ///
    /// The sale itself — deciding, paying — belongs to SellPoint and the master. This owns only
    /// what happens to the object: leave the world when offered, come back if refused, end when
    /// accepted.
    /// </summary>
    public class SellableEntity : MonoBehaviour
    {
        [Tooltip("What this is worth, when nothing on this object implements ISellValueSource.")]
        [SerializeField] private int _value;

        [SerializeField] private NetworkBridge _networkBridge;

        private ISellValueSource _source;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private XRGrabInteractableRef _grab;

        /// <summary>
        /// Raised on every client once the shop has accepted, before <see cref="OnSold"/> ends the
        /// object. Anything that needs to react to the sale — a plot letting go, a plant counting
        /// its harvest — hangs off this rather than off SellPoint.
        /// </summary>
        public event System.Action Sold;

        /// <summary>
        /// What this fetches. Live from the source when there is one, so a produce that ripens into
        /// a better price does not need anything to push the new number here.
        /// </summary>
        public int SellValue => _source != null ? _source.SellValue : _value;

        /// <summary>
        /// Whether the shop will take this right now.
        ///
        /// The component's own enabled flag is the switch: an unripe produce has it off and turns
        /// it on when it ripens. Idle means idle — there is nothing to derive and nothing to keep
        /// in step.
        /// </summary>
        public bool CanBeSold => enabled;

        /// <summary>True between being offered over the counter and the shop answering.</summary>
        public bool IsPending { get; private set; }

        /// <summary>True only on the machine holding it. See XRGrabInteractableRef.IsHeld.</summary>
        public bool IsHeld => _grab.IsHeld;

        private void Awake()
        {
            _source = GetComponent<ISellValueSource>();
            _grab = new XRGrabInteractableRef(gameObject);
        }

        /// <summary>
        /// Takes the object out of the world, or puts it back.
        ///
        /// Called on every client from SellPoint's announcement, so the thing leaves the seller's
        /// hands at once rather than hanging in mid-air for the round trips the sale takes. A
        /// refusal reverses it exactly.
        /// </summary>
        public void SetPending(bool pending)
        {
            if (IsPending == pending) return;
            IsPending = pending;

            _renderers ??= GetComponentsInChildren<Renderer>(true);
            _colliders ??= GetComponentsInChildren<Collider>(true);

            foreach (Renderer r in _renderers) if (r != null) r.enabled = !pending;
            foreach (Collider c in _colliders) if (c != null) c.enabled = !pending;
            _grab.SetEnabled(!pending);
        }

        /// <summary>
        /// The shop has accepted. Runs on every client from SellPoint's announcement.
        ///
        /// Raises Sold for anything watching, then ends the object. Despawn needs state authority,
        /// so only the owner acts — which by this point is the shop, because SellPoint takes
        /// ownership before it completes.
        /// </summary>
        public void Sell()
        {
            Sold?.Invoke();
            OnSold();
        }

        /// <summary>
        /// What being sold does to this object. Despawning is right for a crop and is the default
        /// rather than the rule — something that should go behind the counter instead of out of
        /// existence overrides this.
        /// </summary>
        protected virtual void OnSold()
        {
            NetworkObject obj = _networkBridge == null ? null : _networkBridge.Object;
            if (obj == null || !obj.HasStateAuthority) return;
            SceneNetworking.NetworkRunnerRef.Despawn(obj);
        }

        private void OnValidate()
        {
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkBridge>();
        }
    }
}
