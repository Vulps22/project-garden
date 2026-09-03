using Fusion;
using Fusion.Addons.Physics;
using SomniumSpace.Network.Bridge;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// A crop in the ground. Spawned by the master into a PlantSlot, grows there, and is never
    /// touched by a player.
    ///
    /// That last point is what makes this class small. A plant has no XRGrabInteractable, no
    /// NetworkGrabbable, no SingleHolderFilter, no ReturnableEntity, AlignableEntity, HoveringEntity
    /// or KinematicController — six components that exist only for things a hand can reach, and
    /// nothing can reach a plant. What players take is Produce.
    ///
    /// It keeps AuthorityController, because a plant does belong to the world and a new master
    /// client has to be able to claim one.
    ///
    /// Growth is derived, never sent: every client compares now against _plantedTimestamp and
    /// arrives at the same scale, so syncing one timestamp syncs the whole thing forever.
    /// </summary>
    public abstract class Plant : MonoBehaviour, IAuthoritySource, ILifecycleNotifier, IPlotOccupant
    {
        [SerializeField] public SeedDefinition seedDefinition;
        [SerializeField] public NetworkBridge networkBridge;

        [Tooltip("The body that scales as the plant grows. Usually the root.")]
        [SerializeField] protected Transform _bodyToScale;

        [Tooltip("Full size of the body once grown.")]
        [SerializeField] protected float _maxScale = 1f;

        public uint NetworkId => networkBridge?.Object == null ? 0u : networkBridge.Object.Id.Raw;

        /// <summary>A plant belongs to the world. Nobody holds it, so the master always should.</summary>
        public bool ShouldMasterOwn => true;

        public event System.Action LifecycleChanged;

        /// <summary>Unix seconds when this was planted. The only growth state there is.</summary>
        protected long _plantedTimestamp;

        /// <summary>Unix seconds when withering began, or 0 while the plant is still alive.</summary>
        protected long _witherTimestamp;

        /// <summary>The plot this occupies, on every client — it is also where the plant is.</summary>
        protected PlantSlot _slot;

        private bool _hasBorne;

        public bool HasLocalAuthority =>
            networkBridge != null && networkBridge.Object != null && networkBridge.Object.HasStateAuthority;

        /// <summary>Growth completion [0,1], derived from the clock on every client.</summary>
        public float GetGrowthCompletion()
        {
            if (_plantedTimestamp == 0) return 0f;
            float duration = Mathf.Max(0.01f, seedDefinition.growthDuration);
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return Mathf.Clamp01((now - _plantedTimestamp) / duration);
        }

        /// <summary>Wither completion [0,1]. Zero while the plant is still alive.</summary>
        public float GetWitherCompletion()
        {
            if (_witherTimestamp == 0) return 0f;
            float duration = Mathf.Max(0.01f, seedDefinition.witherDuration);
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return Mathf.Clamp01((now - _witherTimestamp) / duration);
        }

        protected virtual void Awake()
        {
            // Awake, not Start: Fusion raises Spawned() while instantiating a runtime-spawned
            // prefab, before Unity reaches Start. See runtime-spawn.md.
            networkBridge.OnSpawned += OnSpawned;
            networkBridge.OnStateAuthorityChanged += OnStateAuthorityChanged;
            networkBridge.OnMessageToProxies += OnMessageToProxies;
            SceneNetworking.OnOtherPlayerJoined += OnOtherPlayerJoined;
        }

        protected virtual void OnDestroy()
        {
            if (networkBridge != null)
            {
                networkBridge.OnSpawned -= OnSpawned;
                networkBridge.OnStateAuthorityChanged -= OnStateAuthorityChanged;
                networkBridge.OnMessageToProxies -= OnMessageToProxies;
            }
            SceneNetworking.OnOtherPlayerJoined -= OnOtherPlayerJoined;
        }

        private void OnSpawned()
        {
            ApplyBodyScale();
            LifecycleChanged?.Invoke();
        }

        private void OnOtherPlayerJoined(PlayerRef player) => broadcastState();

        /// <summary>
        /// Only the master re-asserts. A client that has just gained authority knows least about
        /// the object, and asserting its stale copy over everyone else is how the 2026-09-02 bugs
        /// happened.
        /// </summary>
        private void OnStateAuthorityChanged(bool hasAuthority)
        {
            if (hasAuthority && SceneNetworking.IsMasterClient) broadcastState();
        }

        /// <summary>
        /// Called by the master immediately after spawning, before anyone else has this object.
        /// Sets the facts that define the plant so the state broadcast has something true to say.
        /// </summary>
        public void Init(PlantSlot slot, long plantedTimestamp)
        {
            _slot = slot;
            _plantedTimestamp = plantedTimestamp;
            ApplyBodyScale();
            broadcastState();
        }

        private void Update()
        {
            ApplyBodyScale();

            // Every decision below belongs to the master. Growth itself is derived, so proxies
            // stay in step without being told anything.
            if (!HasLocalAuthority || !SceneNetworking.IsMasterClient) return;

            if (!_hasBorne && GetGrowthCompletion() >= 1f)
            {
                _hasBorne = true;
                OnFullyGrown();
                return;
            }

            if (_witherTimestamp != 0 && GetWitherCompletion() >= 1f) End();
        }

        /// <summary>
        /// Scales the body from the clock. Runs on every client, every frame, and sends nothing —
        /// this is the derivation tier working as intended.
        /// </summary>
        protected virtual void ApplyBodyScale()
        {
            if (_bodyToScale == null) return;

            float scale = GetGrowthCompletion() * _maxScale;
            if (_witherTimestamp != 0) scale *= 1f - GetWitherCompletion();
            _bodyToScale.localScale = Vector3.one * scale;
        }

        /// <summary>The body has finished growing. Master only. Bear, or become the crop.</summary>
        protected abstract void OnFullyGrown();

        /// <summary>Starts the plant dying. Master only; the clock does the rest on every client.</summary>
        protected void BeginWithering()
        {
            if (_witherTimestamp != 0) return;
            _witherTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            broadcastState();
        }

        /// <summary>
        /// Torn out before its time, because the garden lease on this plot expired. Everything the
        /// plant put into the world goes with it.
        /// </summary>
        public void Uproot()
        {
            if (!SceneNetworking.IsMasterClient) return;
            OnUprooted();
            _slot = null;          // the lease clears the plot itself; End() must not fight it
            End();
        }

        /// <summary>Last chance to take down anything this plant spawned. Master only.</summary>
        protected virtual void OnUprooted() { }

        /// <summary>
        /// The plant's life is over: let the plot go and stop existing. Despawn destroys this on
        /// every client, so it needs no announcement of its own.
        /// </summary>
        protected void End()
        {
            if (_slot != null) _slot.Release();

            NetworkObject obj = networkBridge == null ? null : networkBridge.Object;
            if (obj != null && obj.HasStateAuthority) SceneNetworking.NetworkRunnerRef.Despawn(obj);
        }

        public void broadcastState()
        {
            if (networkBridge == null || networkBridge.Object == null) return;
            if (!networkBridge.Object.HasStateAuthority) return;

            BytesWriter writer = new BytesWriter(BytesWriter.IntSize * 5 + GetExtraBroadcastStateSize());
            writer.AddInt((int)SlotNetworkId());
            writer.AddInt((int)(_plantedTimestamp >> 32));
            writer.AddInt((int)(_plantedTimestamp & 0xFFFFFFFFL));
            writer.AddInt((int)(_witherTimestamp >> 32));
            writer.AddInt((int)(_witherTimestamp & 0xFFFFFFFFL));
            OnWriteBroadcastState(writer);
            networkBridge.RPC_SendMessageToProxies((byte)PlantMessageType.stateSync, writer.Data);
        }

        private void OnMessageToProxies(byte id, byte[] data)
        {
            if ((PlantMessageType)id != PlantMessageType.stateSync) return;

            BytesReader reader = new BytesReader(data);
            AdoptSlot((uint)reader.NextInt());
            long high = reader.NextInt();
            long low = (uint)reader.NextInt();
            _plantedTimestamp = (high << 32) | low;
            high = reader.NextInt();
            low = (uint)reader.NextInt();
            _witherTimestamp = (high << 32) | low;

            OnReadBroadcastState(reader);
            ApplyBodyScale();
            LifecycleChanged?.Invoke();
        }

        /// <summary>
        /// Which plot this stands in — and therefore where it is.
        ///
        /// Spawn position is not networked: Fusion uses it only for the local instantiation, so a
        /// proxy would otherwise build the plant at the world origin and wait for a transform
        /// component to correct it. A plant never moves, so paying for a NetworkTransform on every
        /// plant in the garden to replicate a value that changes once would be absurd. Naming the
        /// plot instead is a derivation: every client already has the plot, so one id places the
        /// plant exactly, forever, with nothing ticking.
        /// </summary>
        private uint SlotNetworkId()
        {
            if (_slot == null) return 0u;
            NetworkObject obj = _slot.GetComponent<NetworkObject>();
            return obj == null ? 0u : obj.Id.Raw;
        }

        private void AdoptSlot(uint rawId)
        {
            if (_slot != null || rawId == 0) return;

            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (runner == null) return;
            if (!runner.TryFindObject(new NetworkId { Raw = rawId }, out NetworkObject obj) || obj == null) return;

            _slot = obj.GetComponent<PlantSlot>();
            if (_slot == null) return;

            transform.position = _slot.transform.position;
            transform.rotation = _slot.transform.rotation;
        }

        protected virtual int GetExtraBroadcastStateSize() => 0;
        protected virtual void OnWriteBroadcastState(BytesWriter writer) { }
        protected virtual void OnReadBroadcastState(BytesReader reader) { }

        /// <summary>
        /// Spawns a produce prefab master-owned. Shared by both branches because "make the crop
        /// exist" is the same act whether it replaces the plant or hangs off it.
        /// </summary>
        protected Produce SpawnProduce(NetworkObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Logger.Error($"SpawnProduce() '{gameObject.name}' — no produce prefab assigned");
                return null;
            }

            SceneNetworking net = SceneNetworking.Instance;
            NetworkRunner runner = SceneNetworking.NetworkRunnerRef;
            if (net == null || runner == null) return null;

            if (!net.NetworkPrefabs.TryGetValue(prefab, out NetworkPrefabId prefabId))
            {
                Logger.Error($"SpawnProduce() '{gameObject.name}' — '{prefab.name}' is not registered on SceneNetworking");
                return null;
            }

            NetworkObject spawned;
            try
            {
                spawned = runner.Spawn(prefabId, position, rotation, null, null,
                                       NetworkSpawnFlags.SharedModeStateAuthMasterClient);
            }
            catch (System.Exception e)
            {
                Logger.Error($"SpawnProduce() '{gameObject.name}' — Fusion threw spawning '{prefab.name}': {e.Message}");
                return null;
            }

            if (spawned == null) return null;

            PlaceSpawned(spawned, position, rotation);

            Produce produce = spawned.GetComponent<Produce>();
            if (produce == null) Logger.Error($"SpawnProduce() '{gameObject.name}' — spawned '{spawned.name}' has no Produce component");
            else Logger.Info($"SpawnProduce() '{gameObject.name}' — bore '{spawned.name}' at {spawned.transform.position} (asked {position})");
            return produce;
        }

        /// <summary>
        /// Puts a freshly spawned object where it is meant to be.
        ///
        /// Two steps, because the pose handed to Runner.Spawn() is used only for the local
        /// instantiation and is not networked at all.
        ///
        /// The second step is the one that took two uploads to find. A kinematic networked
        /// rigidbody is driven *by* its network state, so writing transform.position on it is
        /// overwritten on the next tick and the object reappears wherever its state says — which
        /// for something spawned a moment ago is the origin. Seeds never showed this because a
        /// seed is non-kinematic and physics-driven, so a transform write flows through. Produce
        /// is kinematic until it is harvested, and it is the first thing this game ever spawned
        /// that was. Teleport() is Fusion's own answer: it moves the body *and* its state.
        /// </summary>
        protected static void PlaceSpawned(NetworkObject spawned, Vector3 position, Quaternion rotation)
        {
            spawned.transform.SetPositionAndRotation(position, rotation);

            var body = spawned.GetComponent<NetworkRigidbody3D>();
            if (body != null) body.Teleport(position, rotation);
        }

        protected virtual void OnValidate()
        {
            if (networkBridge == null) networkBridge = GetComponent<NetworkBridge>();
            if (_bodyToScale == null) _bodyToScale = transform;
        }
    }

    /// <summary>Scoped to the plant's own NetworkBridge, so starting at 0 collides with nothing.</summary>
    enum PlantMessageType : byte
    {
        stateSync = 0
    }
}
