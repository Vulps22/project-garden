using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vulps.GrowAGarden.Interaction;

namespace Vulps.GrowAGarden.Ownership
{
    /// <summary>
    /// A Plot's claim, and the deed that represents it while it is unclaimed.
    ///
    /// The deed is part of the Garden, not spawned: one per Plot, placed beside it in the BorderedPlot prefab and wired
    /// here, so every client has it from the moment the scene loads. Claiming hides it and relinquishing shows it again,
    /// each client acting on the replicated owner, so no master ever spawns a second and no despawn needs authority.
    ///
    /// OwnerId is a fact, broadcast the Plot's own scene-placed NetworkMessenger, SendToAll,
    /// no authority dance needed for a plain announcement — the same shared bridge PlotStateManager
    /// broadcasts slot occupancy on, coordinated via PlotMessageType so the two never collide. A
    /// first pass here treated Plot having its own NetworkObject as an unsolved problem needing a
    /// global claim-table workaround; it is not unsolved — PlantSlot proved a scene-placed bridge
    /// works fine on this same Plot long before PlotLeaseManager or PlotStateManager existed.
    /// </summary>
    public class PlotLeaseManager : MonoBehaviour
    {
        [SerializeField] private NetworkMessenger _networkBridge;

        [Tooltip("Where the deed sits, roughly a metre above the plot centre.")]
        [SerializeField] private Socket _deedSocket;

        [Tooltip("This Plot's own deed, placed beside it in the BorderedPlot prefab: shown while the Plot is free, hidden " +
                 "while it is claimed. Never spawned or despawned.")]
        [SerializeField] private CollectibleEntity _deed;

        /// <summary>Whose Plot this is, or null when free. A fact, decided by the master and
        /// replicated — see PlantSlot.OwnerId, same shape.</summary>
        public string OwnerId { get; private set; }
        public bool IsClaimed => !string.IsNullOrEmpty(OwnerId);

        /// <summary>Fires on every client after OwnerId changes — claimed or relinquished, this
        /// plot's own broadcast only. Read OwnerId/IsClaimed from the sender; the event itself
        /// carries no payload, same reasoning as ILifecycleNotifier.</summary>
        public event System.Action<PlotLeaseManager> OwnerChanged;

        /// <summary>This Plot's own NetworkId — same shared bridge PlotStateManager already reads
        /// its own copy of, exposed here too since PlotApplication needs it to stamp which Plot an
        /// application scroll was dropped on.</summary>
        public uint NetworkId => _networkBridge?.Object == null ? 0u : _networkBridge.Object.Id.Raw;

        private const int MAX_TEAMMATES = 4;
        private readonly List<string> _teammateIds = new List<string>();

        /// <summary>Who else may plant/harvest here, granted by the owner accepting an
        /// application. Max 4 — see plot-ownership.md.</summary>
        public IReadOnlyList<string> TeammateIds => _teammateIds;

        /// <summary>Fires on every client after the teammate list changes. Same no-payload shape
        /// as OwnerChanged — read TeammateIds from the sender.</summary>
        public event System.Action<PlotLeaseManager> TeammatesChanged;


        private void Start()
        {
            if (_networkBridge == null)
            {
                Logger.Error($"Start() '{gameObject.name}' — _networkBridge is NULL! Ownership will never leave this client!");
                return;
            }
            _networkBridge.MessageToAll += OnMessageToAll;
            PlayerBridge.OtherPlayerJoined += OnOtherPlayerJoined;

            if (_deed == null) { Logger.Error($"Start() '{gameObject.name}' — no deed wired; this Plot can never be claimed"); return; }
            _deed.TakeRequested += OnDeedTakeRequested;
            ShowDeed();
        }

        /// <summary>A late joiner never saw any past OwnerChanged broadcast — Fusion does not
        /// replay RPCs — so without this its shadow copy of OwnerId reads "unclaimed" regardless
        /// of the truth. Harmless while the master stays the master, but a real correctness bug
        /// the moment host migration hands that client the role.</summary>
        private void OnOtherPlayerJoined()
        {
            if (!PlayerManager.IsMaster) return;
            AnnounceOwner(OwnerId);
            AnnounceTeammates();
        }

        private void OnDestroy()
        {
            if (_deed != null) _deed.TakeRequested -= OnDeedTakeRequested;
            if (_networkBridge != null) _networkBridge.MessageToAll -= OnMessageToAll;
            PlayerBridge.OtherPlayerJoined -= OnOtherPlayerJoined;
        }

        /// <summary>
        /// The deed is shown exactly while the Plot is free, on every client, from the replicated owner. Hidden, it lets go
        /// of any hand and is put straight back in its socket; shown, it is held there and called back if it is away.
        /// </summary>
        private void ShowDeed()
        {
            if (_deed == null) return;
            _deed.SetHidden(IsClaimed);
            if (_deedSocket == null) return;
            _deedSocket.Hold(_deed.gameObject);
            if (IsClaimed) _deedSocket.SnapHere();   // the client simulating it; others' auto-recall waits for it
            else if (!_deed.IsHeld && _deedSocket.HasLeft(_deed.gameObject)) _deedSocket.RecallHere();
        }

        // ── Claiming ──────────────────────────────────────────────────────────────

        /// <summary>See ShopKeeper's own TAKE_RETRY_SECONDS/TAKE_RETRY_TIMEOUT_SECONDS — same
        /// reasoning: the holder's grabber RPC and Fusion's own state-authority transfer for the
        /// deed are separate network events with no ordering guarantee against the take-request
        /// itself, and over real distance arriving after it is the common case. This one also
        /// waits on HasKnownAuthority, which the original one-shot version never checked at all —
        /// Claim()'s Discard() call needs state authority to actually despawn the deed, so a claim
        /// that announced itself without authority would leave the deed's own object stuck lying
        /// around, claimed or not.</summary>
        private const float DEED_RETRY_SECONDS = 0.15f;
        private const float DEED_RETRY_TIMEOUT_SECONDS = 1.5f;
        private bool _resolvingClaim;

        /// <summary>
        /// The holder carried the deed into the shed's claim trigger — see DeedClaimZone. Reuses
        /// CollectibleEntity's existing take-request wire-up rather than inventing a second
        /// message channel: "the master takes notice that this holder has this deed" is exactly
        /// what a take-request already is, and it is already built, already replicated, and
        /// already only fires on the holder's own client.
        /// </summary>
        private void OnDeedTakeRequested(CollectibleEntity deed)
        {
            if (!PlayerManager.IsMaster) return;
            if (deed != _deed || IsClaimed) return;
            if (deed.IsTaken || !deed.InDispenser) return;
            if (_resolvingClaim) return;   // already retrying this exact request

            StartCoroutine(ResolveDeedTakeRequest(deed));
        }

        /// <summary>
        /// Retries quietly for a short window rather than giving up on the first miss — see
        /// ShopKeeper.ResolvePurchaseRequest for the full reasoning. Here a miss was previously
        /// silent (no bounce-back exists for a deed the way ReturnToSocket exists for shop stock),
        /// which read as "carried it in and nothing happened" — this gives the holder RPC and the
        /// authority transfer a real chance to arrive before settling for that.
        /// </summary>
        private IEnumerator ResolveDeedTakeRequest(CollectibleEntity deed)
        {
            _resolvingClaim = true;

            float waited = 0f;
            PlayerIdentity holder = PlayerIdentity.None;
            while (waited < DEED_RETRY_TIMEOUT_SECONDS)
            {
                if (deed == null || IsClaimed || deed.IsTaken || !deed.InDispenser)
                {
                    _resolvingClaim = false;
                    yield break;
                }

                holder = deed.GetGrabber();
                if (holder.Exists && deed.HasKnownAuthority) break;

                yield return new WaitForSeconds(DEED_RETRY_SECONDS);
                waited += DEED_RETRY_SECONDS;
            }

            _resolvingClaim = false;

            if (!holder.Exists)
            {
                Logger.Warn($"OnDeedTakeRequested() '{gameObject.name}' — requested but holder unknown on this client after {DEED_RETRY_TIMEOUT_SECONDS}s; ignoring");
                yield break;
            }

            if (!deed.HasKnownAuthority)
            {
                Logger.Warn($"OnDeedTakeRequested() '{gameObject.name}' — no known state authority on this client after {DEED_RETRY_TIMEOUT_SECONDS}s; ignoring");
                yield break;
            }

            Claim(holder.Id);
        }

        /// <summary>Master only. Records the owner and announces it; every client hides the deed on hearing it
        /// (OnMessageToAll → ShowDeed), so nothing is despawned and nobody's authority matters.</summary>
        private void Claim(string ownerId)
        {
            if (IsClaimed)
            {
                Logger.Warn($"Claim() '{gameObject.name}' — already owned by '{OwnerId}'; ignoring claim from '{ownerId}'");
                return;
            }
            if (string.IsNullOrEmpty(ownerId)) return;

            AnnounceOwner(ownerId);
            Logger.Info($"Claim() '{gameObject.name}' — claimed by '{ownerId}'");

            // Defensive — a freshly claimed Plot should never carry the previous owner's
            // teammates, though normally Relinquish() already cleared this list.
            if (_teammateIds.Count > 0)
            {
                _teammateIds.Clear();
                AnnounceTeammates();
            }

        }

        /// <summary>Relinquishes the claim, master only. Reachable today from PlotCleanup when a
        /// player is gone; the "drag the deed back out" gesture plot-ownership.md describes is
        /// not built.</summary>
        public void Relinquish()
        {
            if (!PlayerManager.IsMaster) return;
            AnnounceOwner(null);

            // Load-bearing, not defensive: without this a later claimant would silently inherit
            // the departed owner's teammates.
            if (_teammateIds.Count > 0)
            {
                _teammateIds.Clear();
                AnnounceTeammates();
            }
        }

        // ── Teammates ─────────────────────────────────────────────────────────────

        /// <summary>Master only. No-ops rather than warns for the ordinary cases (already a
        /// teammate, is the owner, already full) — none of these are attacker input, just timing,
        /// and PlotApplication already logs the outcome of the application that led here.</summary>
        public void AddTeammate(string playerId)
        {
            if (!PlayerManager.IsMaster) return;
            if (string.IsNullOrEmpty(playerId)) return;
            if (playerId == OwnerId) return;
            if (_teammateIds.Contains(playerId)) return;
            if (_teammateIds.Count >= MAX_TEAMMATES)
            {
                Logger.Warn($"AddTeammate() '{gameObject.name}' — already at {MAX_TEAMMATES} teammates; '{playerId}' not added");
                return;
            }

            _teammateIds.Add(playerId);
            AnnounceTeammates();
        }

        /// <summary>Called by the local owner's own client — see the wall roster's revoke
        /// gesture. Broadcast rather than a direct call because only the owner's client acts on
        /// this locally; the master (who may be a different machine) is the one allowed to
        /// actually remove it.</summary>
        public void RequestRemoveTeammate(string playerId)
        {
            if (_networkBridge == null || string.IsNullOrEmpty(playerId)) return;

            int size = sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(playerId);
            var writer = new BytesWriter(size);
            writer.AddAutoString(playerId);
            _networkBridge.SendToAll((byte)PlotMessageType.TeammateRemoveRequest, writer.Data);
        }

        /// <summary>Master only. Public for PlotCleanup — a departed player is removed from
        /// every Plot's teammate list, not just one, so this is called directly rather than
        /// through the client-request round trip RequestRemoveTeammate exists for. A no-op if
        /// the id was never a teammate here, same as Relinquish() is a no-op on an unclaimed
        /// Plot — PlotCleanup loops every Plot without checking membership first.</summary>
        public void RemoveTeammate(string playerId)
        {
            if (!PlayerManager.IsMaster) return;
            if (!_teammateIds.Remove(playerId)) return;

            AnnounceTeammates();
        }

        /// <summary>The only place the teammate list is broadcast — always the full list, same
        /// shape as EconomyManager's balance table. Four short ids is not worth an incremental
        /// format the way 24 occupancy bits was.</summary>
        private void AnnounceTeammates()
        {
            if (!PlayerManager.IsMaster || _networkBridge == null) return;

            int size = BytesWriter.ByteSize;
            foreach (string id in _teammateIds)
                size += sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(id);

            var writer = new BytesWriter(size);
            writer.AddByte((byte)_teammateIds.Count);
            foreach (string id in _teammateIds) writer.AddAutoString(id);

            _networkBridge.SendToAll((byte)PlotMessageType.TeammatesChanged, writer.Data);
        }

        /// <summary>The only place OwnerId is written. Master only; announced to everyone, same
        /// shape as PlantSlot.AnnounceOccupancy.</summary>
        private void AnnounceOwner(string ownerId)
        {
            if (!PlayerManager.IsMaster || _networkBridge == null) return;

            string owner = ownerId ?? string.Empty;
            int size = sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(owner);
            var writer = new BytesWriter(size);
            writer.AddAutoString(owner);
            _networkBridge.SendToAll((byte)PlotMessageType.OwnerChanged, writer.Data);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((PlotMessageType)id)
            {
                case PlotMessageType.OwnerChanged:
                    var ownerReader = new BytesReader(data);
                    if (!ownerReader.IsValid) return;
                    string owner = ownerReader.NextAutoString();
                    OwnerId = string.IsNullOrEmpty(owner) ? null : owner;
                    ShowDeed();
                    OwnerChanged?.Invoke(this);
                    break;

                case PlotMessageType.TeammatesChanged:
                    var teamReader = new BytesReader(data);
                    if (!teamReader.IsValid) return;
                    int count = teamReader.NextByte();
                    _teammateIds.Clear();
                    for (int i = 0; i < count; i++) _teammateIds.Add(teamReader.NextAutoString());
                    TeammatesChanged?.Invoke(this);
                    break;

                case PlotMessageType.TeammateRemoveRequest:
                    if (!PlayerManager.IsMaster) return;
                    var removeReader = new BytesReader(data);
                    if (!removeReader.IsValid) return;
                    RemoveTeammate(removeReader.NextAutoString());
                    break;

                default:
                    break;   // SlotOccupancyChanged/PlantRequest/Planted/FullStateSync belong to PlotStateManager, on the same shared bridge
            }
        }

        private void OnValidate()
        {
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkMessenger>();
            if (_deedSocket == null) _deedSocket = GetComponentInChildren<Socket>(true);
        }
    }
}
