using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// Turns a plain CollectibleEntity ("blank_scroll") into a Plot application: which Plot it was
    /// dropped on, and the accept decision when the owner carries it back into the hut.
    ///
    /// A sibling component, not a subclass — CollectibleEntity's Awake is private, so a derived
    /// class's own Awake would not override it (a fragile, non-obvious trap), and this codebase's
    /// own convention is small components reading a host's small public API rather than
    /// subclassing MonoBehaviours. This shares CollectibleEntity's own NetworkBridge (each
    /// dispensed scroll is its own NetworkObject) and adds message ids 5-7, appended to
    /// CollectibleMessageType rather than a second enum — see that enum's own note.
    ///
    /// The accept decision lives here, not in an external manager, because there is no fixed
    /// external owner to pre-subscribe to: unlike a Deed (always PlotLeaseManager's _currentDeed)
    /// or dispenser stock (always DispensingEntity's _currentItem), a loose application scroll's
    /// target Plot is only known once it has actually been dropped somewhere. Same shape as
    /// GardenLease.OnPlayerDestroyed: resolve the manager dynamically, master-gate, call a public
    /// mutator directly.
    /// </summary>
    public class PlotApplication : MonoBehaviour
    {
        [SerializeField] private CollectibleEntity _collectible;
        [SerializeField] private NetworkBridge _networkBridge;

        /// <summary>Which Plot this scroll was last dropped on. 0 = not yet dropped anywhere.</summary>
        public uint AssociatedPlotId { get; private set; }

        private void Awake()
        {
            _networkBridge.OnMessageToAll += OnMessageToAll;
        }

        private void OnDestroy()
        {
            if (_networkBridge != null) _networkBridge.OnMessageToAll -= OnMessageToAll;
        }

        /// <summary>The holder walked this scroll onto a Plot — see PlotApplicationZone. Every
        /// client hears this; only the master commits it, same as every other fact here.</summary>
        public void RequestAssociatePlot(uint plotId)
        {
            var writer = new BytesWriter(BytesWriter.IntSize);
            writer.AddInt((int)plotId);
            _networkBridge.RPC_SendMessageToAll((byte)CollectibleMessageType.associateRequest, writer.Data);
        }

        /// <summary>The holder walked this scroll into the hut's accept zone — see
        /// ApplicationAcceptZone. No payload: the master reads the target Plot and the current
        /// holder off state everyone already has.</summary>
        public void RequestAccept()
        {
            _networkBridge.RPC_SendMessageToAll((byte)CollectibleMessageType.acceptRequest, new byte[0]);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((CollectibleMessageType)id)
            {
                case CollectibleMessageType.associateRequest:
                    if (!SceneNetworking.IsMasterClient) return;
                    var reader = new BytesReader(data);
                    if (!reader.IsValid) return;
                    Associate((uint)reader.NextInt());
                    break;

                case CollectibleMessageType.associated:
                    var aReader = new BytesReader(data);
                    if (!aReader.IsValid) return;
                    AssociatedPlotId = (uint)aReader.NextInt();
                    break;

                case CollectibleMessageType.acceptRequest:
                    if (!SceneNetworking.IsMasterClient) return;
                    OnAcceptRequested();
                    break;
            }
        }

        /// <summary>Master only. Announces which Plot this scroll now sits on.</summary>
        private void Associate(uint plotId)
        {
            var writer = new BytesWriter(BytesWriter.IntSize);
            writer.AddInt((int)plotId);
            _networkBridge.RPC_SendMessageToAll((byte)CollectibleMessageType.associated, writer.Data);
        }

        /// <summary>
        /// Master only. Adds the applicant as a teammate if the carrier is genuinely the Plot's
        /// owner, then discards the scroll regardless of outcome — once taken, ApplyTaken already
        /// disabled its ReturnableEntity auto-recall permanently, so leaving a rejected scroll
        /// lying around would just be litter with no way home. See PlotApplication's class note.
        /// </summary>
        private void OnAcceptRequested()
        {
            PlotLeaseManager plot = ResolvePlot();
            if (plot == null)
            {
                Logger.Warn($"OnAcceptRequested() '{gameObject.name}' — AssociatedPlotId={AssociatedPlotId} did not resolve to a live Plot; discarding");
                _collectible.Discard();
                return;
            }

            PlayerBalance holder = _collectible.GetGrabber();
            string applicant = _collectible.TakenBy;

            if (holder != null && !string.IsNullOrEmpty(applicant) && holder.GetID() == plot.OwnerId)
            {
                plot.AddTeammate(applicant);
                Logger.Info($"OnAcceptRequested() '{gameObject.name}' — '{applicant}' accepted onto plot owned by '{plot.OwnerId}'");
            }
            else
            {
                Logger.Warn($"OnAcceptRequested() '{gameObject.name}' — holder={(holder == null ? "unknown" : holder.GetID())} is not owner '{plot.OwnerId}'; not accepted");
            }

            _collectible.Discard();
        }

        private PlotLeaseManager ResolvePlot()
        {
            if (AssociatedPlotId == 0) return null;

            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (runner == null) return null;
            if (!runner.TryFindObject(new NetworkId { Raw = AssociatedPlotId }, out NetworkObject obj) || obj == null) return null;

            return obj.GetComponent<PlotLeaseManager>();
        }

        private void OnValidate()
        {
            if (_collectible == null) _collectible = GetComponent<CollectibleEntity>();
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkBridge>();
        }
    }
}
