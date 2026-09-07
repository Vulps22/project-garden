using System;
using UnityEngine;

namespace GrowAGarden
{
    /// <summary>
    /// The world's heartbeat, and the catalogue of what can be stocked.
    ///
    /// Every N seconds it raises StockRecycled and every slot re-rolls, so an exploration area
    /// stops being one-and-done and the map is worth re-walking because it is a different map
    /// now. Roadmap step 4.
    ///
    /// The tick is master-only and is not replicated. Nothing but the master does anything with
    /// the schedule — it decides the roll and spawns the result, and the spawned object is the
    /// announcement — so a client deriving the same deadline would have nothing to do with it.
    /// That is the opposite call from PlayerManager's departure clock, where every client derives
    /// the deadline precisely so a handover mid-countdown loses nothing. Here a handover simply
    /// restarts the cycle, and an extra or a missed re-roll costs nothing.
    /// </summary>
    public class WorldManager : MonoBehaviour
    {
        public static WorldManager Instance { get; private set; }

        /// <summary>Re-roll every slot in the world. Master only; see the class note.</summary>
        public static event Action StockRecycled;

        [Tooltip("Everything a slot can stock. SeedDefinition is a MonoBehaviour, so these are " +
                 "component references — put one of each crop on a child of this object. A plain " +
                 "array of references rather than a [Serializable] wrapper class, because arrays " +
                 "of those arrive empty from the bundle export.")]
        [SerializeField] private SeedDefinition[] _buyables;

        [Tooltip("Seconds between world-wide stock cycles.")]
        [SerializeField] private float _recycleSeconds = 300f;

        public System.Collections.Generic.IReadOnlyList<SeedDefinition> Buyables => _buyables;

        private float _nextRecycleAt = float.PositiveInfinity;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Logger.Warn($"Awake() '{gameObject.name}' — a second WorldManager; destroying this one");
                Destroy(this);
                return;
            }
            Instance = this;

            // First thing this assembly prints, so everything after it belongs to this run.
            Logger.BeginSession();
        }

        private void OnEnable()
        {
            // Deliberately not Awake. The first cycle spawns, and spawning before Fusion has
            // given this peer a player index makes it instantiate the prefab and *then* throw
            // out of Simulation.GetNextId(), leaving an orphan nothing will ever clean up.
            // ShopSlot waits for the same signal for the same reason.
            if (SceneNetworking.IsNetworkReady) BeginCycling();
            else SceneNetworking.OnLocalPlayerJoined += BeginCycling;
        }

        private void OnDisable() => SceneNetworking.OnLocalPlayerJoined -= BeginCycling;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            // Static events outlive the scene that filled them. SceneNetworking clears its own
            // for the same reason; a stale subscriber here would be a destroyed BuyPoint.
            StockRecycled = null;
        }

        /// <summary>
        /// Stocks the world once, immediately, and starts the clock.
        ///
        /// The first cycle is what fills every slot, so a slot needs no starting stock of its
        /// own — its serialized definition is only the fallback for a world with no WorldManager.
        /// </summary>
        private void BeginCycling()
        {
            // Deferred a frame rather than cycled here. StockRecycled is a static event and the
            // BuyPoints subscribe in their own OnEnable, whose order against this one is undefined
            // — and IsNetworkReady is a static that outlives the scene it describes, so on a world
            // transition this can be true before a single stall exists. Firing into no subscribers
            // leaves every stall empty until the next cycle, minutes later.
            //
            // EconomyManager defers its own first broadcast for the same reason.
            StartCoroutine(BeginNextFrame());
        }

        private System.Collections.IEnumerator BeginNextFrame()
        {
            yield return null;
            Recycle();
            _nextRecycleAt = Time.time + _recycleSeconds;
        }

        private void Update()
        {
            if (Time.time < _nextRecycleAt) return;
            _nextRecycleAt = Time.time + _recycleSeconds;
            Recycle();
        }

        private void Recycle()
        {
            if (!SceneNetworking.IsMasterClient) return;

            Logger.Info($"Recycle() '{gameObject.name}' — cycling world stock, {(_buyables == null ? 0 : _buyables.Length)} buyables");
            StockRecycled?.Invoke();
        }

        private void OnValidate()
        {
            if (_recycleSeconds < 1f) _recycleSeconds = 1f;
        }
    }
}
