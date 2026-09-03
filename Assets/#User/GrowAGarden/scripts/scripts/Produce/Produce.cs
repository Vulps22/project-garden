using Fusion;
using SomniumSpace.Network.Bridge;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GrowAGarden
{
    /// <summary>
    /// The thing a player actually takes: a carrot pulled from the ground, a pumpkin cut from its
    /// vine. Along with Seed it is one of only two things in the garden a hand can touch.
    ///
    /// It ripens on its own clock, derived from a timestamp exactly as plant growth is, and it is
    /// not grabbable until it is ripe. Once harvested it becomes a loose object in the world and
    /// behaves like a dropped seed — that is when its HoveringEntity is allowed to start, and not
    /// a moment before. Until then it belongs to a plant or to a plot and should sit perfectly
    /// still, which is rule 2 applied at the right seam.
    /// </summary>
    public class Produce : MonoBehaviour, IHeldObject, IKinematicSource, IAuthoritySource,
                           ILifecycleNotifier, ISellValueSource, IPlotOccupant
    {
        [SerializeField] public SeedDefinition seedDefinition;
        [SerializeField] public NetworkBridge networkBridge;
        [SerializeField] protected XRGrabInteractable _grabInteractable;
        [SerializeField] private SellableEntity _sellable;

        [Tooltip("The body that scales as the produce ripens. Usually the root.")]
        [SerializeField] protected Transform _bodyToScale;

        [SerializeField] protected float _maxScale = 1f;

        public uint NetworkId => networkBridge?.Object == null ? 0u : networkBridge.Object.Id.Raw;

        /// <summary>True once a player has taken this. Loose in the world from here on.</summary>
        public bool IsHarvested { get; private set; }

        public int SellValue => seedDefinition != null ? seedDefinition.sellValue : 0;

        /// <summary>
        /// Raised on the master when this is taken. A bearing plant listens so it can empty the
        /// socket and decide whether to wither or bear again.
        /// </summary>
        public event System.Action Harvested;

        public event System.Action LifecycleChanged;

        /// <summary>
        /// Anchored until it is picked. A produce hanging on a vine or sitting in the earth must
        /// not fall, drift or be pushed; once it is in a hand it is an ordinary physical object.
        /// </summary>
        public bool ShouldBeKinematic => !IsHarvested;
        public bool ShouldUseGravity => IsHarvested;

        /// <summary>Unharvested produce belongs to the world, and the master should hold it.</summary>
        public bool ShouldMasterOwn => !IsHarvested && string.IsNullOrEmpty(HolderId);

        public string HolderId => _grabber?.GetID();
        public bool IsHeld => _grabInteractable != null && _grabInteractable.isSelected;

        /// <summary>
        /// True while there is a live NetworkObject to send on. Fusion throws
        /// "Behaviour not initialized: Object not set." from an RPC on a detached object, and an
        /// XRI teardown deselect arrives *after* Fusion has detached this one — a produce is
        /// despawned by the sale that happens while it is still in the player's hand. The throw
        /// escaped UnregisterInteractable before it dropped this interactable from the manager's
        /// list, leaving XRInteractionManager to throw every frame for the rest of the session.
        /// </summary>
        private bool CanSendRpc => networkBridge != null && networkBridge.Object != null;

        public bool HasLocalAuthority =>
            networkBridge != null && networkBridge.Object != null && networkBridge.Object.HasStateAuthority;

        protected long _ripenTimestamp;
        private PlayerBalance _grabber;
        private HoveringEntity _hovering;

        /// <summary>The plot this holds, if any. Only a RootedProduce has one. Master-side.</summary>
        protected PlantSlot _slot;

        /// <summary>Ripeness [0,1], derived from the clock on every client.</summary>
        public float GetRipeness()
        {
            if (_ripenTimestamp == 0) return 0f;
            float duration = Mathf.Max(0.01f, seedDefinition == null ? 1f : seedDefinition.ripenDuration);
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return Mathf.Clamp01((now - _ripenTimestamp) / duration);
        }

        public bool IsRipe => GetRipeness() >= 1f;

        protected virtual void Awake()
        {
            _hovering = GetComponent<HoveringEntity>();

            // Awake, not Start: Fusion raises Spawned() during instantiation of a runtime-spawned
            // prefab, before Unity reaches Start. See runtime-spawn.md.
            networkBridge.OnSpawned += OnSpawned;
            networkBridge.OnStateAuthorityChanged += OnStateAuthorityChanged;
            networkBridge.OnMessageToAll += OnMessageToAll;
            networkBridge.OnMessageToProxies += OnMessageToProxies;
            SceneNetworking.OnOtherPlayerJoined += OnOtherPlayerJoined;
            GardenLease.PlayerGone += OnPlayerGone;
            _grabInteractable.selectEntered.AddListener(OnGrabSelected);
            _grabInteractable.selectExited.AddListener(OnGrabDeselected);

            SetGrabbable(false);          // nothing is grabbable before it is ripe
            if (_sellable != null) _sellable.enabled = false;
        }

        protected virtual void OnDestroy()
        {
            if (networkBridge != null)
            {
                networkBridge.OnSpawned -= OnSpawned;
                networkBridge.OnStateAuthorityChanged -= OnStateAuthorityChanged;
                networkBridge.OnMessageToAll -= OnMessageToAll;
                networkBridge.OnMessageToProxies -= OnMessageToProxies;
            }
            SceneNetworking.OnOtherPlayerJoined -= OnOtherPlayerJoined;
            GardenLease.PlayerGone -= OnPlayerGone;
            _grabInteractable.selectEntered.RemoveListener(OnGrabSelected);
            _grabInteractable.selectExited.RemoveListener(OnGrabDeselected);
        }

        /// <summary>
        /// A departed player is holding nothing. A correction rather than cleanup — leaving their
        /// id set makes SingleHolderFilter refuse this to everyone for the rest of the session.
        /// </summary>
        private void OnPlayerGone(string playerId)
        {
            if (_grabber == null || _grabber.GetID() != playerId) return;
            _grabber = null;
            LifecycleChanged?.Invoke();
        }

        private void OnSpawned()
        {
            ApplyRipeness();
            LifecycleChanged?.Invoke();
        }

        private void OnOtherPlayerJoined(PlayerRef player) => broadcastState();

        private void OnStateAuthorityChanged(bool hasAuthority)
        {
            if (hasAuthority && SceneNetworking.IsMasterClient) broadcastState();
        }

        /// <summary>
        /// Called by whatever bore this, on the master, immediately after spawning — before any
        /// other client has the object — so the first state broadcast describes something true.
        ///
        /// Deliberately does *not* take a SeedDefinition. A produce carries its own on its prefab,
        /// and the obvious-looking alternative — letting the plant hand over the one it is holding
        /// — is a trap: a RootedPlant despawns itself in the very next statement, so the produce
        /// would spend its life pointing at a component on a destroyed GameObject. Unity's
        /// overloaded null then makes every `seedDefinition != null` read false, which is how a
        /// carrot came to sell for nothing while its ripening still looked correct by coincidence.
        /// </summary>
        public void Init(PlantSlot slot, long ripenTimestamp)
        {
            if (seedDefinition == null)
                Logger.Error($"Init() '{gameObject.name}' — no SeedDefinition on this prefab; it will be worthless and ripen instantly");

            _slot = slot;
            _ripenTimestamp = ripenTimestamp;
            ApplyRipeness();
            broadcastState();
        }

        private void Update()
        {
            ApplyRipeness();
            ReportPlacementOnce();
        }

        /// <summary>
        /// Says where this actually ended up, once, a second after it was born.
        ///
        /// Temporary, and worth the noise: two uploads were spent on "the produce is missing" when
        /// it had in fact spawned every time, because the only position in the log was the one we
        /// *asked* for. A second later is after any network tick has had its say, so this
        /// distinguishes "never placed" from "placed, then moved". Delete once the loop is green.
        /// </summary>
        private void ReportPlacementOnce()
        {
            if (_placementReported || _ripenTimestamp == 0) return;
            if (System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _ripenTimestamp < 1) return;

            _placementReported = true;
            Logger.Info($"ReportPlacement() '{gameObject.name}' — at {transform.position} scale={transform.localScale.x:F2} " +
                        $"bodyScale={(_bodyToScale == null ? -1f : _bodyToScale.localScale.x):F2} ripe={IsRipe} " +
                        $"kinematic={ShouldBeKinematic} authority={HasLocalAuthority} worth={SellValue}");
        }

        private bool _placementReported;

        /// <summary>
        /// Scales from the clock and opens the grab the moment it is ripe. Runs on every client and
        /// sends nothing — ripeness is a derivation, not an event, so a proxy needs no message to
        /// agree about when the fruit became takeable.
        /// </summary>
        private void ApplyRipeness()
        {
            ApplyScale();

            // A sale in progress owns the grab and the renderers, and this runs every frame — so
            // without this the next frame would hand the grab straight back and the object would
            // reappear in the seller's hand halfway through being bought.
            if (_sellable != null && _sellable.IsPending) return;

            bool ripe = IsRipe;
            if (_grabInteractable != null && _grabInteractable.enabled != ripe) SetGrabbable(ripe);
            if (_sellable != null && _sellable.enabled != ripe) _sellable.enabled = ripe;
        }

        /// <summary>
        /// How big this is right now. Separate from the grab and sell gating above so a subclass
        /// can change the shape of its growth without having to remember to re-run the rest.
        /// </summary>
        protected virtual void ApplyScale()
        {
            if (_bodyToScale == null || IsHarvested) return;
            _bodyToScale.localScale = Vector3.one * Mathf.Max(0.001f, GetRipeness() * _maxScale);
        }

        private void SetGrabbable(bool grabbable)
        {
            if (_grabInteractable != null) _grabInteractable.enabled = grabbable;
        }

        // ── Harvest ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Asks the master to let this be taken.
        ///
        /// The holder asks because only the holder knows its own hand closed on the fruit — and it
        /// must be asked for explicitly, never inferred from authority: NetworkGrabbable takes
        /// authority on *hover*, so a player who merely waves a hand near a ripe pumpkin owns it,
        /// having harvested nothing.
        /// </summary>
        public void RequestHarvest()
        {
            if (IsHarvested || !IsRipe) return;
            networkBridge.RPC_SendMessageToAll((byte)ProduceMessageType.harvestRequest, new byte[0]);
        }

        /// <summary>The master decides. Everyone else ignores the request.</summary>
        private void OnHarvestRequested()
        {
            if (!SceneNetworking.IsMasterClient) return;
            if (IsHarvested || !IsRipe) return;

            networkBridge.RPC_SendMessageToAll((byte)ProduceMessageType.harvested, new byte[0]);
        }

        /// <summary>
        /// Applied on every client including the sender, which is why the decider announces rather
        /// than writing flags locally: broadcastState() self-gates on authority and the master
        /// usually is not this object's authority by the time it decides.
        /// </summary>
        private void ApplyHarvested()
        {
            if (IsHarvested) return;
            IsHarvested = true;

            // Loose in the world now, so it may float. Not before: while it is on the plant it has
            // to sit exactly where it was put.
            if (_hovering != null && !_hovering.IsHovering) _hovering.BeginHovering(shouldFallFirst: false);

            LifecycleChanged?.Invoke();

            // The plot, and the plant, only care on the master — they make decisions, and decisions
            // happen in one place.
            if (!SceneNetworking.IsMasterClient) return;

            if (_slot != null)
            {
                _slot.Release();
                _slot = null;
            }

            Harvested?.Invoke();
            broadcastState();
        }

        /// <summary>
        /// Cleared away with the garden it grew in, because its owner's lease expired. Only ever
        /// reaches unharvested produce — once a player has taken it, it is theirs and is no longer
        /// standing in anybody's plot.
        /// </summary>
        public void Uproot()
        {
            if (!SceneNetworking.IsMasterClient) return;
            _slot = null;

            NetworkObject obj = networkBridge == null ? null : networkBridge.Object;
            if (obj != null && obj.HasStateAuthority) SceneNetworking.NetworkRunnerRef.Despawn(obj);
        }

        // ── State ─────────────────────────────────────────────────────────────────

        public void broadcastState()
        {
            if (networkBridge == null || networkBridge.Object == null) return;
            if (!networkBridge.Object.HasStateAuthority) return;

            BytesWriter writer = new BytesWriter(BytesWriter.IntSize * 2 + BytesWriter.ByteSize);
            writer.AddInt((int)(_ripenTimestamp >> 32));
            writer.AddInt((int)(_ripenTimestamp & 0xFFFFFFFFL));
            writer.AddByte(IsHarvested ? (byte)1 : (byte)0);
            networkBridge.RPC_SendMessageToProxies((byte)ProduceMessageType.stateSync, writer.Data);
        }

        private void OnMessageToAll(byte id, byte[] data)
        {
            switch ((ProduceMessageType)id)
            {
                case ProduceMessageType.grabber:
                    BytesReader grabReader = new BytesReader(data);
                    bool hasGrabber = grabReader.NextByte() == 1;
                    _grabber = hasGrabber ? EconomyManager.Instance.GetPlayer(grabReader.NextString()) : null;
                    LifecycleChanged?.Invoke();
                    break;
                case ProduceMessageType.harvestRequest:
                    OnHarvestRequested();
                    break;
                case ProduceMessageType.harvested:
                    ApplyHarvested();
                    break;
                default:
                    Logger.Warn($"OnMessageToAll() '{gameObject.name}' — unknown message id={id}");
                    break;
            }
        }

        private void OnMessageToProxies(byte id, byte[] data)
        {
            if ((ProduceMessageType)id != ProduceMessageType.stateSync) return;

            BytesReader reader = new BytesReader(data);
            long high = reader.NextInt();
            long low = (uint)reader.NextInt();
            _ripenTimestamp = (high << 32) | low;

            bool harvested = reader.NextByte() == 1;
            if (harvested && !IsHarvested) ApplyHarvested();

            ApplyRipeness();
            LifecycleChanged?.Invoke();
        }

        public PlayerBalance GetGrabber() => _grabber;

        /// <summary>
        /// Taking it is the harvest. The request goes out from the machine whose hand closed on it;
        /// the fruit is already in that hand, and the master confirms a couple of hundred
        /// milliseconds later. A decision may be slow. A hand must not be.
        /// </summary>
        public void OnGrabSelected(SelectEnterEventArgs args)
        {
            _grabber = EconomyManager.Instance == null ? null : EconomyManager.Instance.GetLocalPlayer();

            if (_grabber == null)
            {
                Logger.Warn($"OnGrabSelected() '{gameObject.name}' — local balance unavailable, grabber not broadcast");
            }
            else if (CanSendRpc)
            {
                string id = _grabber.GetID();
                int size = BytesWriter.ByteSize + sizeof(short) + System.Text.Encoding.UTF8.GetByteCount(id);
                var writer = new BytesWriter(size);
                writer.AddByte(1);
                writer.AddString(id);
                networkBridge.RPC_SendMessageToAll((byte)ProduceMessageType.grabber, writer.Data);
            }

            if (!IsHarvested) RequestHarvest();
        }

        public void OnGrabDeselected(SelectExitEventArgs args)
        {
            // A teardown deselect is not a player letting go, and there is nothing to announce:
            // the despawn already tells every peer this produce is gone. See CanSendRpc.
            if (!CanSendRpc) return;

            var writer = new BytesWriter(BytesWriter.ByteSize);
            writer.AddByte(0);
            networkBridge.RPC_SendMessageToAll((byte)ProduceMessageType.grabber, writer.Data);
        }

        protected virtual void OnValidate()
        {
            if (networkBridge == null) networkBridge = GetComponent<NetworkBridge>();
            if (_grabInteractable == null) _grabInteractable = GetComponent<XRGrabInteractable>();
            if (_sellable == null) _sellable = GetComponent<SellableEntity>();
            if (_bodyToScale == null) _bodyToScale = transform;
        }
    }

    /// <summary>Scoped to the produce's own NetworkBridge, so starting at 0 collides with nothing.</summary>
    enum ProduceMessageType : byte
    {
        stateSync = 0,
        grabber = 1,
        harvestRequest = 2,
        harvested = 3
    }
}
