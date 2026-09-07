using Fusion;
using SomniumSpace.Network.Bridge;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// A Plot's claim, and the deed that represents it while it is unclaimed.
    ///
    /// The spawn rule is deliberately narrower than a shop or a dispenser's: spawn a fresh deed
    /// only when there is no deed AND no owner. DispensingEntity's "restock whenever the socket is
    /// empty" is wrong here — an empty socket is the *normal* state of a claimed Plot, since the
    /// deed does not live on the Plot once claimed. Reusing that logic would print a duplicate deed
    /// for a Plot that already has an owner, and let a second player claim the same Plot.
    ///
    /// OwnerId is a fact, broadcast the Plot's own scene-placed NetworkBridge, RPC_SendMessageToAll,
    /// no authority dance needed for a plain announcement — the same shared bridge PlotStateManager
    /// broadcasts slot occupancy on, coordinated via PlotMessageType so the two never collide. A
    /// first pass here treated Plot having its own NetworkObject as an unsolved problem needing a
    /// global claim-table workaround; it is not unsolved — PlantSlot proved a scene-placed bridge
    /// works fine on this same Plot long before PlotLeaseManager or PlotStateManager existed.
    /// </summary>
    public class PlotLeaseManager : MonoBehaviour
    {
        [SerializeField] private NetworkBridge _networkBridge;

        [Tooltip("Where the deed sits, roughly a metre above the plot centre.")]
        [SerializeField] private Socket _deedSocket;

        [Tooltip("The Deed prefab. Must also be listed on SceneNetworking.")]
        [SerializeField] private NetworkObject _deedPrefab;

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

        private CollectibleEntity _currentDeed;

        /// <summary>See DispensingEntity.RESTOCK_RETRY_SECONDS — same reasoning.</summary>
        private const float SPAWN_RETRY_SECONDS = 0.5f;
        private float _lastSpawnAttempt = float.NegativeInfinity;

        private void Start()
        {
            if (_networkBridge == null)
            {
                Logger.Error($"Start() '{gameObject.name}' — _networkBridge is NULL! Ownership will never leave this client!");
                return;
            }
            _networkBridge.OnMessageToAll += OnMessageToAll;
            SceneNetworking.OnOtherPlayerJoined += OnOtherPlayerJoined;
        }

        /// <summary>A late joiner never saw any past OwnerChanged broadcast — Fusion does not
        /// replay RPCs — so without this its shadow copy of OwnerId reads "unclaimed" regardless
        /// of the truth. Harmless while the master stays the master, but a real correctness bug
        /// the moment host migration hands that client the role.</summary>
        private void OnOtherPlayerJoined(PlayerRef player)
        {
            if (!SceneNetworking.IsMasterClient) return;
            AnnounceOwner(OwnerId);
            AnnounceTeammates();
        }

        private void OnEnable()
        {
            if (_deedSocket == null) return;
            _deedSocket.Entered += OnEntered;
            _deedSocket.Left += OnLeft;
        }

        private void OnDisable()
        {
            if (_deedSocket == null) return;
            _deedSocket.Entered -= OnEntered;
            _deedSocket.Left -= OnLeft;
        }

        private void OnDestroy()
        {
            SetCurrentDeed(null);
            if (_networkBridge != null) _networkBridge.OnMessageToAll -= OnMessageToAll;
            SceneNetworking.OnOtherPlayerJoined -= OnOtherPlayerJoined;
        }

        private void Update()
        {
            if (!SceneNetworking.IsMasterClient) return;

            // Unity's overloaded null check catches a despawned deed here with no event needed —
            // a destroyed UnityEngine.Object compares equal to null even though the C# reference
            // itself is not, which is exactly the "did it go away" question this is asking.
            if (_currentDeed != null || IsClaimed) return;

            if (Time.time - _lastSpawnAttempt < SPAWN_RETRY_SECONDS) return;
            _lastSpawnAttempt = Time.time;
            SpawnDeed();
        }

        // ── Claiming ──────────────────────────────────────────────────────────────

        /// <summary>
        /// The holder carried the deed into the shed's claim trigger — see DeedClaimZone. Reuses
        /// CollectibleEntity's existing take-request wire-up rather than inventing a second
        /// message channel: "the master takes notice that this holder has this deed" is exactly
        /// what a take-request already is, and it is already built, already replicated, and
        /// already only fires on the holder's own client.
        /// </summary>
        private void OnDeedTakeRequested(CollectibleEntity deed)
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (deed != _currentDeed) return;
            if (deed.IsTaken || !deed.InDispenser) return;

            PlayerBalance holder = deed.GetGrabber();
            if (holder == null)
            {
                Logger.Warn($"OnDeedTakeRequested() '{gameObject.name}' — requested but holder unknown on this client yet; ignoring");
                return;
            }

            Claim(holder.GetID());
        }

        /// <summary>Master only. Records the owner, announces it to everyone, and removes the
        /// deed — it does not go on living in the shed, per plot-ownership.md: the shed's
        /// contents are a per-viewer derivation from this fact, not a real object on a real
        /// shelf.</summary>
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

            if (_currentDeed != null)
            {
                _currentDeed.Discard();
                SetCurrentDeed(null);
            }
        }

        /// <summary>Relinquishes the claim, master only. Reachable today from GardenLease when a
        /// player is gone; the "drag the deed back out" gesture plot-ownership.md describes is
        /// not built.</summary>
        public void Relinquish()
        {
            if (!SceneNetworking.IsMasterClient) return;
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
            if (!SceneNetworking.IsMasterClient) return;
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
            writer.AddString(playerId);
            _networkBridge.RPC_SendMessageToAll((byte)PlotMessageType.TeammateRemoveRequest, writer.Data);
        }

        /// <summary>Master only. Public for GardenLease — a departed player is removed from
        /// every Plot's teammate list, not just one, so this is called directly rather than
        /// through the client-request round trip RequestRemoveTeammate exists for. A no-op if
        /// the id was never a teammate here, same as Relinquish() is a no-op on an unclaimed
        /// Plot — GardenLease loops every Plot without checking membership first.</summary>
        public void RemoveTeammate(string playerId)
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (!_teammateIds.Remove(playerId)) return;

            AnnounceTeammates();
        }

        /// <summary>The only place the teammate list is broadcast — always the full list, same
        /// shape as EconomyManager's balance table. Four short ids is not worth an incremental
        /// format the way 24 occupancy bits was.</summary>
        private void AnnounceTeammates()
        {
            if (!SceneNetworking.IsMasterClient || _networkBridge == null) return;

            int size = BytesWriter.ByteSize;
            foreach (string id in _teammateIds)
                size += sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(id);

            var writer = new BytesWriter(size);
            writer.AddByte((byte)_teammateIds.Count);
            foreach (string id in _teammateIds) writer.AddString(id);

            _networkBridge.RPC_SendMessageToAll((byte)PlotMessageType.TeammatesChanged, writer.Data);
        }

        /// <summary>The only place OwnerId is written. Master only; announced to everyone, same
        /// shape as PlantSlot.AnnounceOccupancy.</summary>
        private void AnnounceOwner(string ownerId)
        {
            if (!SceneNetworking.IsMasterClient || _networkBridge == null) return;

            string owner = ownerId ?? string.Empty;
            int size = sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(owner);
            var writer = new BytesWriter(size);
            writer.AddString(owner);
            _networkBridge.RPC_SendMessageToAll((byte)PlotMessageType.OwnerChanged, writer.Data);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((PlotMessageType)id)
            {
                case PlotMessageType.OwnerChanged:
                    var ownerReader = new BytesReader(data);
                    if (!ownerReader.IsValid) return;
                    string owner = ownerReader.NextString();
                    OwnerId = string.IsNullOrEmpty(owner) ? null : owner;
                    OwnerChanged?.Invoke(this);
                    break;

                case PlotMessageType.TeammatesChanged:
                    var teamReader = new BytesReader(data);
                    if (!teamReader.IsValid) return;
                    int count = teamReader.NextByte();
                    _teammateIds.Clear();
                    for (int i = 0; i < count; i++) _teammateIds.Add(teamReader.NextString());
                    TeammatesChanged?.Invoke(this);
                    break;

                case PlotMessageType.TeammateRemoveRequest:
                    if (!SceneNetworking.IsMasterClient) return;
                    var removeReader = new BytesReader(data);
                    if (!removeReader.IsValid) return;
                    RemoveTeammate(removeReader.NextString());
                    break;

                default:
                    break;   // SlotOccupancyChanged/PlantRequest/Planted/FullStateSync belong to PlotStateManager, on the same shared bridge
            }
        }

        // ── The socket's facts ───────────────────────────────────────────────────

        private void OnEntered(GameObject candidate)
        {
            if (_currentDeed != null) return;
            if (candidate == null || !candidate.TryGetComponent(out CollectibleEntity deed)) return;
            if (deed.IsTaken || !deed.InDispenser) return;

            SetCurrentDeed(deed);
            _deedSocket.Hold(deed.gameObject);
        }

        private void OnLeft(GameObject _) { }   // nothing to restock unless Update decides to

        private void SetCurrentDeed(CollectibleEntity deed)
        {
            if (_currentDeed == deed) return;

            if (_currentDeed != null)
                _currentDeed.TakeRequested -= OnDeedTakeRequested;

            _currentDeed = deed;

            if (_currentDeed != null)
                _currentDeed.TakeRequested += OnDeedTakeRequested;
        }

        // ── Spawning ──────────────────────────────────────────────────────────────

        /// <summary>Mirrors DispensingEntity.SpawnStock/SpawnFreshItem — see those for the Fusion
        /// readiness and orphan-avoidance reasoning, identical here.</summary>
        private void SpawnDeed()
        {
            if (!CanSpawn()) return;

            CollectibleEntity spawned = SpawnFreshDeed();
            if (spawned == null) return;

            Logger.Info($"SpawnDeed() '{gameObject.name}' — spawned deed authority={spawned.HasLocalAuthority}");

            SetCurrentDeed(spawned);
            spawned.PlaceInDispenser(_deedSocket.transform.position, _deedSocket.transform.rotation);
            _deedSocket.Hold(spawned.gameObject);
            StartCoroutine(AnnounceStock(spawned));
        }

        private bool CanSpawn()
        {
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            return runner != null
                && runner.IsRunning
                && runner.LocalPlayer.IsRealPlayer
                && SceneNetworking.IsNetworkReady;
        }

        private CollectibleEntity SpawnFreshDeed()
        {
            SceneNetworking net = SceneNetworking.Instance;
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (net == null || runner == null) return null;

            if (_deedPrefab == null)
            {
                Logger.Error($"SpawnFreshDeed() '{gameObject.name}' — no deed prefab set");
                return null;
            }

            if (!net.NetworkPrefabs.TryGetValue(_deedPrefab, out NetworkPrefabId prefabId))
            {
                Logger.Warn($"SpawnFreshDeed() '{gameObject.name}' — '{_deedPrefab.name}' is not registered on SceneNetworking yet");
                return null;
            }

            NetworkObject spawned;
            try
            {
                spawned = runner.Spawn(prefabId, _deedSocket.transform.position, _deedSocket.transform.rotation,
                                       null, null,
                                       NetworkSpawnFlags.SharedModeStateAuthMasterClient);
            }
            catch (System.Exception e)
            {
                Logger.Error($"SpawnFreshDeed() '{gameObject.name}' — Fusion threw spawning '{_deedPrefab.name}': {e.Message}");
                return null;
            }

            if (spawned == null)
            {
                Logger.Error($"SpawnFreshDeed() '{gameObject.name}' — Fusion refused to spawn '{_deedPrefab.name}'");
                return null;
            }

            CollectibleEntity deed = spawned.GetComponent<CollectibleEntity>();
            if (deed == null)
                Logger.Error($"SpawnFreshDeed() '{gameObject.name}' — spawned '{spawned.name}' has no CollectibleEntity component");

            return deed;
        }

        /// <summary>See BuyPoint.AnnounceStock — same late-registration problem, same crude fix.</summary>
        private IEnumerator AnnounceStock(CollectibleEntity deed)
        {
            float[] gaps = { 0.25f, 0.75f, 1f };
            foreach (float gap in gaps)
            {
                yield return new WaitForSeconds(gap);

                if (deed == null || deed != _currentDeed || !deed.InDispenser) yield break;

                deed.broadcastState();
            }
        }

        private void OnValidate()
        {
            if (_networkBridge == null) _networkBridge = GetComponent<NetworkBridge>();
            if (_deedSocket == null) _deedSocket = GetComponentInChildren<Socket>(true);
        }
    }
}
